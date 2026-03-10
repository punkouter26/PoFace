using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PoFace.Client.Services;

/// <summary>Typed HTTP client for all PoFace API calls.</summary>
public sealed class ApiClient
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ApiClient(HttpClient http) => _http = http;

    // ── Session ───────────────────────────────────────────────────────────────

    /// <summary>POST /api/sessions → returns sessionId and 5 round definitions.</summary>
    public async Task<StartSessionResponse> StartSessionAsync(CancellationToken ct = default)
    {
        var response = await _http.PostAsync("/api/sessions", content: null, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StartSessionResponse>(JsonOptions, ct)
               ?? throw new InvalidOperationException("Null response from POST /api/sessions.");
    }

    /// <summary>POST /api/sessions/{id}/complete → returns total score.</summary>
    public async Task<CompleteSessionResponse> CompleteSessionAsync(
        string sessionId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync($"/api/sessions/{sessionId}/complete", content: null, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CompleteSessionResponse>(JsonOptions, ct)
               ?? throw new InvalidOperationException("Null response from POST /api/sessions/{id}/complete.");
    }

    /// <summary>DELETE /api/sessions/{id} — discard an in-progress session.</summary>
    public async Task DiscardSessionAsync(string sessionId, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"/api/sessions/{sessionId}", ct);
        // 404 is acceptable — session may already have been cleaned up.
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
            response.EnsureSuccessStatusCode();
    }

    // ── Scoring ───────────────────────────────────────────────────────────────

    /// <summary>
    /// POST /api/sessions/{sessionId}/rounds/{roundNumber}/score — uploads the JPEG frame.
    /// <paramref name="jpegBytes"/> must already be stripped of the "data:image/jpeg;base64," prefix.
    /// </summary>
    public async Task<ScoreRoundResponse> ScoreRoundAsync(
        string sessionId, int roundNumber, byte[] jpegBytes, CancellationToken ct = default)
    {
        using var content    = new MultipartFormDataContent();
        using var byteContent = new ByteArrayContent(jpegBytes);
        byteContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        content.Add(byteContent, "image", "frame.jpg");

        var response = await _http.PostAsync(
            $"/api/sessions/{sessionId}/rounds/{roundNumber}/score", content, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ScoreRoundResponse>(JsonOptions, ct)
               ?? throw new InvalidOperationException("Null response from score endpoint.");
    }
}

// ── Response DTOs ─────────────────────────────────────────────────────────────

public sealed record RoundDefinition(
    [property: JsonPropertyName("roundNumber")]   int    RoundNumber,
    [property: JsonPropertyName("targetEmotion")] string TargetEmotion);

public sealed record StartSessionResponse(
    [property: JsonPropertyName("sessionId")] string SessionId,
    [property: JsonPropertyName("rounds")]    IReadOnlyList<RoundDefinition> Rounds);

public sealed record CompleteSessionResponse(
    [property: JsonPropertyName("sessionId")]      string SessionId,
    [property: JsonPropertyName("totalScore")]     int    TotalScore,
    [property: JsonPropertyName("isPersonalBest")] bool   IsPersonalBest,
    [property: JsonPropertyName("recapUrl")]       string RecapUrl);

public sealed record ScoreRoundResponse(
    [property: JsonPropertyName("roundNumber")]    int     RoundNumber,
    [property: JsonPropertyName("targetEmotion")]  string  TargetEmotion,
    [property: JsonPropertyName("score")]          int     Score,
    [property: JsonPropertyName("rawConfidence")]  double  RawConfidence,
    [property: JsonPropertyName("headPoseValid")]  bool    HeadPoseValid,
    [property: JsonPropertyName("headPoseYaw")]    double? HeadPoseYaw,
    [property: JsonPropertyName("headPosePitch")]  double? HeadPosePitch,
    [property: JsonPropertyName("faceDetected")]   bool    FaceDetected,
    [property: JsonPropertyName("imageUrl")]       string  ImageUrl);
