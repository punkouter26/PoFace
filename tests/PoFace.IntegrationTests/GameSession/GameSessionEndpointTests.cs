using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PoFace.IntegrationTests.Infrastructure;

namespace PoFace.IntegrationTests.GameSession;

/// <summary>
/// Integration tests for the game-session lifecycle:
///   POST /api/sessions → POST /api/sessions/{id}/rounds/{n}/score (×5) → POST /api/sessions/{id}/complete
///
/// Uses a real ASP.NET Core TestServer backed by Azurite.
/// The FaceAnalysisService is stubbed: all rounds score 0, FaceDetected = false.
/// </summary>
public sealed class GameSessionEndpointTests : IClassFixture<AzuriteFixture>
{
    private readonly AzuriteFixture _azurite;

    private static readonly string[] CanonicalEmotions =
        ["Happiness", "Surprise", "Anger", "Sadness", "Fear"];

    public GameSessionEndpointTests(AzuriteFixture azurite) => _azurite = azurite;

    private PoFaceWebAppFactory CreateFactory() => new(_azurite.ConnectionString);

    // ── POST /api/sessions ────────────────────────────────────────────────────

    [Fact]
    public async Task PostSession_Returns201WithSessionIdAndFiveRoundsInCanonicalOrder()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", "sess-test-start-001");
        client.DefaultRequestHeaders.Add("X-Test-Display-Name", "StartUser");

        var response = await client.PostAsync("/api/sessions", null);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<StartSessionDto>();
        body.Should().NotBeNull();
        body!.SessionId.Should().NotBeNullOrWhiteSpace();
        body.Rounds.Should().HaveCount(5);
        body.Rounds.Select(r => r.RoundNumber).Should().Equal(1, 2, 3, 4, 5);
        body.Rounds.Select(r => r.TargetEmotion).Should().Equal(CanonicalEmotions);
    }

    // ── Full lifecycle ────────────────────────────────────────────────────────

    [Fact]
    public async Task CompleteSession_AfterScoringAllRounds_Returns200WithRecapUrl()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", "sess-test-complete-001");
        client.DefaultRequestHeaders.Add("X-Test-Display-Name", "CompleteUser");

        // 1. Start a session.
        var startResp = await client.PostAsync("/api/sessions", null);
        startResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var start = await startResp.Content.ReadFromJsonAsync<StartSessionDto>();
        start.Should().NotBeNull();

        // 2. Score all 5 rounds (stub always returns score = 0).
        for (int round = 1; round <= 5; round++)
        {
            using var content = BuildJpegContent();
            var scoreResp = await client.PostAsync(
                $"/api/sessions/{start!.SessionId}/rounds/{round}/score", content);
            scoreResp.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // 3. Complete the session.
        var completeResp = await client.PostAsync(
            $"/api/sessions/{start!.SessionId}/complete", null);
        completeResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await completeResp.Content.ReadFromJsonAsync<CompleteSessionDto>();
        result.Should().NotBeNull();
        result!.SessionId.Should().Be(start.SessionId);
        result.TotalScore.Should().BeInRange(0, 50);                   // stub scores 0 per round
        result.RecapUrl.Should().Be($"/recap/{start.SessionId}");      // SPA URL, not API URL
        result.IsPersonalBest.Should().BeTrue();                       // first run is always PB
    }

    // ── Partial scoring → complete must fail ─────────────────────────────────

    [Fact]
    public async Task CompleteSession_WithUncompletedRounds_ReturnsNonSuccess()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", "sess-test-partial-001");
        client.DefaultRequestHeaders.Add("X-Test-Display-Name", "PartialUser");

        // Start session.
        var startResp = await client.PostAsync("/api/sessions", null);
        var start = await startResp.Content.ReadFromJsonAsync<StartSessionDto>();

        // Score only 3 of 5 rounds.
        for (int round = 1; round <= 3; round++)
        {
            using var content = BuildJpegContent();
            await client.PostAsync(
                $"/api/sessions/{start!.SessionId}/rounds/{round}/score", content);
        }

        // Attempt to complete — must NOT return 200 (round 4 & 5 are missing).
        var completeResp = await client.PostAsync(
            $"/api/sessions/{start!.SessionId}/complete", null);

        completeResp.IsSuccessStatusCode.Should().BeFalse();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static MultipartFormDataContent BuildJpegContent()
    {
        // Minimal 512-byte payload with JPEG SOI marker — accepted by the stub service.
        var bytes = new byte[512];
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;

        var byteContent = new ByteArrayContent(bytes);
        byteContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");

        var form = new MultipartFormDataContent();
        form.Add(byteContent, "image", "frame.jpg");
        return form;
    }

    // ── DTOs ──────────────────────────────────────────────────────────────────

    private sealed record StartSessionDto(
        string SessionId,
        IReadOnlyList<RoundDto> Rounds);

    private sealed record RoundDto(
        int    RoundNumber,
        string TargetEmotion);

    private sealed record CompleteSessionDto(
        string SessionId,
        int    TotalScore,
        bool   IsPersonalBest,
        string RecapUrl);
}
