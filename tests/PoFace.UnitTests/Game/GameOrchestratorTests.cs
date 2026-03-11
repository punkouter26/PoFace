using System.Net;
using System.Text;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using PoFace.Client.Services;

namespace PoFace.UnitTests.Game;

/// <summary>
/// Unit tests for <see cref="GameOrchestrator"/>.
///
/// The game loop contains hardcoded Task.Delay calls (2 s GetReady + 3×1 s countdown + 2 s
/// ScoreReveal per round). Tests that exercise deep into the loop use a short-lived
/// <see cref="CancellationTokenSource"/> to cancel before the first delay fires, allowing
/// observation of the state transitions that occur synchronously before each delay.
///
/// For full round-trip coverage (5-round completion) see PoFace.IntegrationTests and E2E tests.
/// A future improvement would be to inject a delay provider so unit tests can run without
/// real-time waiting.
/// </summary>
public sealed class GameOrchestratorTests
{
    // ── Helper factories ──────────────────────────────────────────────────────

    private static Mock<IJSRuntime> BuildJsMock()
    {
        var js = new Mock<IJSRuntime>();

        // Void invocations (audio, flashShutter, releaseCamera) — both overloads
        js.Setup(j => j.InvokeAsync<IJSVoidResult>(
                It.IsAny<string>(), It.IsAny<object?[]?>()))
          .Returns(new ValueTask<IJSVoidResult>(Mock.Of<IJSVoidResult>()));

        js.Setup(j => j.InvokeAsync<IJSVoidResult>(
                It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<object?[]?>()))
          .Returns(new ValueTask<IJSVoidResult>(Mock.Of<IJSVoidResult>()));

        // captureFrame returns a minimal JPEG data URL
        js.Setup(j => j.InvokeAsync<string>(
                "webcamInterop.captureFrame", It.IsAny<CancellationToken>(), It.IsAny<object?[]?>()))
          .Returns(new ValueTask<string>("data:image/jpeg;base64,/9j/4AAQSkZJRgABAQ=="));

        return js;
    }

    private const string DefaultStartJson =
        @"{""sessionId"":""test-session-abc"",""rounds"":[" +
        @"{""roundNumber"":1,""targetEmotion"":""Happiness""}," +
        @"{""roundNumber"":2,""targetEmotion"":""Surprise""}," +
        @"{""roundNumber"":3,""targetEmotion"":""Anger""}," +
        @"{""roundNumber"":4,""targetEmotion"":""Sadness""}," +
        @"{""roundNumber"":5,""targetEmotion"":""Fear""}]}";

    private const string DefaultScoreJson =
        @"{""roundNumber"":1,""targetEmotion"":""Happiness"",""score"":0,""rawConfidence"":0," +
        @"""headPoseValid"":false,""headPoseYaw"":null,""headPosePitch"":null," +
        @"""faceDetected"":false,""imageUrl"":""https://example.com/img.jpg""}";

    private const string DefaultCompleteJson =
        @"{""sessionId"":""test-session-abc"",""totalScore"":0,""isPersonalBest"":true," +
        @"""recapUrl"":""/recap/test-session-abc""}";

    private static ApiClient BuildApiClient(
        HttpStatusCode startStatus = HttpStatusCode.Created,
        string? startJson = null)
    {
        var resolvedStartJson = startJson ?? DefaultStartJson;

        var handler = new StubHttpMessageHandler(req =>
        {
            var path = req.RequestUri!.PathAndQuery;

            if (path.EndsWith("/api/sessions") && req.Method == HttpMethod.Post)
                return new HttpResponseMessage(startStatus)
                {
                    Content = new StringContent(resolvedStartJson, Encoding.UTF8, "application/json")
                };

            if (path.Contains("/rounds/") && req.Method == HttpMethod.Post)
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(DefaultScoreJson, Encoding.UTF8, "application/json")
                };

            if (path.Contains("/complete") && req.Method == HttpMethod.Post)
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(DefaultCompleteJson, Encoding.UTF8, "application/json")
                };

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        return new ApiClient(http);
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void InitialState_IsIdle()
    {
        var js    = BuildJsMock();
        var audio = new AudioService(js.Object);
        var api   = BuildApiClient();

        var sut = new GameOrchestrator(api, audio, js.Object);

        sut.State.Should().Be(GameState.Idle);
        sut.CurrentRound.Should().Be(1);
        sut.TotalScore.Should().Be(0);
        sut.Results.Should().BeEmpty();
        sut.SessionId.Should().BeNull();
        sut.CurrentTargetEmotion.Should().BeNull();
    }

    [Fact]
    public async Task StartGameAsync_TransitionsToGetReady_BeforeFirstDelay()
    {
        var js    = BuildJsMock();
        var audio = new AudioService(js.Object);
        var api   = BuildApiClient();

        var sut    = new GameOrchestrator(api, audio, js.Object);
        var states = new List<GameState>();
        sut.StateChanged += () => states.Add(sut.State);

        // Cancel after 1 500 ms — long enough for the HTTP stub to respond and the
        // GetReady state to be set synchronously before Task.Delay(2 000, ct) fires,
        // but well within the 2 s GetReady delay so cancellation terminates early.
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1_500));
        try { await sut.StartGameAsync(cts.Token); }
        catch (OperationCanceledException) { /* expected */ }

        // GetReady is set synchronously before each delay, so it must appear.
        states.Should().Contain(GameState.GetReady);
        // Session ID is assigned before RunRoundAsync is entered.
        sut.SessionId.Should().Be("test-session-abc");
    }

    [Fact]
    public async Task StartGameAsync_WhenStartSessionFails_PropagatesAndRemainsIdle()
    {
        var js    = BuildJsMock();
        var audio = new AudioService(js.Object);
        var api   = BuildApiClient(startStatus: HttpStatusCode.InternalServerError);

        var sut = new GameOrchestrator(api, audio, js.Object);

        await Assert.ThrowsAsync<HttpRequestException>(() => sut.StartGameAsync());

        // State machine must not have advanced.
        sut.State.Should().Be(GameState.Idle);
        sut.SessionId.Should().BeNull();
    }

    [Fact]
    public async Task StartGameAsync_SetsRoundDefsFromApiResponse()
    {
        var js    = BuildJsMock();
        var audio = new AudioService(js.Object);
        var api   = BuildApiClient();

        var sut = new GameOrchestrator(api, audio, js.Object);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        try { await sut.StartGameAsync(cts.Token); }
        catch (OperationCanceledException) { /* expected */ }

        // CurrentTargetEmotion for round 1 must be "Happiness" (canonical order FR-009).
        sut.CurrentTargetEmotion.Should().Be("Happiness");
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(handler(request));
    }
}
