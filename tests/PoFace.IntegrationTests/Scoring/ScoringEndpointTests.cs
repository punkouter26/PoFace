using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PoFace.IntegrationTests.Infrastructure;

namespace PoFace.IntegrationTests.Scoring;

/// <summary>
/// Integration tests for POST /api/sessions/{sessionId}/rounds/{roundNumber}/score.
/// Uses a real ASP.NET Core TestServer backed by Azurite storage.
///
/// NOTE: The Face API call is made against a placeholder / unauthenticated endpoint in the
/// test environment (config value "AzureFace:Endpoint" is absent), so the service returns
/// FaceDetected=false. Tests assert the structural contract (status codes, JSON shape)
/// rather than scoring accuracy.
/// </summary>
public sealed class ScoringEndpointTests : IClassFixture<AzuriteFixture>
{
    private readonly AzuriteFixture _azurite;

    public ScoringEndpointTests(AzuriteFixture azurite) => _azurite = azurite;

    private PoFaceWebAppFactory CreateFactory()
        => new(_azurite.ConnectionString);

    // ── 200 — valid multipart JPEG ────────────────────────────────────────────

    [Fact]
    public async Task PostScore_WithValidJpeg_Returns200WithScoreRoundResult()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", "test-user-001");

        // First create a session so SessionId exists.
        var startResp = await client.PostAsync("/api/sessions", null);
        startResp.EnsureSuccessStatusCode();
        var start = await startResp.Content.ReadFromJsonAsync<StartSessionDto>();
        var sessionId = start!.SessionId;

        using var content = BuildJpegContent(512); // tiny valid sub-500KB payload
        var response = await client.PostAsync(
            $"/api/sessions/{sessionId}/rounds/1/score", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<ScoreRoundResultDto>();
        json.Should().NotBeNull();
        json!.RoundNumber.Should().Be(1);
        json.TargetEmotion.Should().Be("Happiness");
        json.ImageUrl.Should().NotBeNullOrWhiteSpace();
    }

    // ── 413 — oversized payload ───────────────────────────────────────────────

    [Fact]
    public async Task PostScore_OversizedJpeg_Returns413()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", "test-user-001");

        using var content = BuildJpegContent(600 * 1024); // 600 KB — over the 500 KB limit
        var response = await client.PostAsync(
            "/api/sessions/test-session-2/rounds/1/score", content);

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    // ── 415 — non-JPEG content type ───────────────────────────────────────────

    [Fact]
    public async Task PostScore_NonJpegContentType_Returns415()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", "test-user-001");

        using var content = BuildContent(512, "image/png");
        var response = await client.PostAsync(
            "/api/sessions/test-session-3/rounds/1/score", content);

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }

    // ── 422 — round number out of range ──────────────────────────────────────

    [Fact]
    public async Task PostScore_RoundNumberZero_Returns422()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User-Id", "test-user-001");

        using var content = BuildJpegContent(512);
        var response = await client.PostAsync(
            "/api/sessions/test-session-4/rounds/0/score", content);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static MultipartFormDataContent BuildJpegContent(int sizeBytes)
        => BuildContent(sizeBytes, "image/jpeg");

    private static MultipartFormDataContent BuildContent(int sizeBytes, string mimeType)
    {
        var bytes = new byte[sizeBytes];
        // Write minimal JPEG SOI marker so the payload is semi-valid.
        if (sizeBytes >= 2) { bytes[0] = 0xFF; bytes[1] = 0xD8; }

        var byteContent = new ByteArrayContent(bytes);
        byteContent.Headers.ContentType = MediaTypeHeaderValue.Parse(mimeType);

        var form = new MultipartFormDataContent();
        form.Add(byteContent, "image", "frame.jpg");
        return form;
    }

    // ── DTO for deserialization ───────────────────────────────────────────────

    private sealed record StartSessionDto(
        string SessionId,
        IReadOnlyList<object> Rounds);

    private sealed record ScoreRoundResultDto(
        int RoundNumber,
        string TargetEmotion,
        int Score,
        double RawConfidence,
        bool HeadPoseValid,
        double? HeadPoseYaw,
        double? HeadPosePitch,
        bool FaceDetected,
        string ImageUrl);
}
