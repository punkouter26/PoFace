using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PoFace.Api.Features.GameSession;
using PoFace.Api.Features.Scoring;
using PoFace.Api.Infrastructure.Storage;
using PoFace.IntegrationTests.Infrastructure;

namespace PoFace.IntegrationTests.Recap;

/// <summary>
/// Integration tests for GET /api/recap/{sessionId}.
/// Seeds data directly via <see cref="ITableStorageService"/> (Azurite-backed) and
/// asserts the endpoint response contract per contracts/api-endpoints.md.
/// </summary>
public sealed class RecapEndpointTests : IClassFixture<AzuriteFixture>
{
    private readonly AzuriteFixture _azurite;

    public RecapEndpointTests(AzuriteFixture azurite) => _azurite = azurite;

    private PoFaceWebAppFactory CreateFactory() => new(_azurite.ConnectionString);

    // ── Seed Helpers ──────────────────────────────────────────────────────────

    private static readonly string[] _emotions =
        ["Happiness", "Surprise", "Anger", "Sadness", "Fear"];

    private static async Task SeedSessionAsync(
        ITableStorageService store,
        string userId,
        string sessionId,
        bool   isPersonalBest = true,
        DateTimeOffset? expiresAt = null)
    {
        var now = DateTimeOffset.UtcNow;
        await store.UpsertEntityAsync("GameSessions", new GameSessionEntity
        {
            PartitionKey   = userId,
            RowKey         = sessionId,
            UserId         = userId,
            SessionId      = sessionId,
            DisplayName    = "Test Player",
            DeviceType     = "Desktop",
            IsCompleted    = true,
            TotalScore     = 30,
            IsPersonalBest = isPersonalBest,
            StartedAt      = now.AddMinutes(-3),
            CompletedAt    = now,
            ExpiresAt      = expiresAt,
        });
    }

    private static async Task SeedRoundsAsync(
        ITableStorageService store,
        string sessionId,
        int    roundCount = 5)
    {
        for (int i = 1; i <= roundCount; i++)
        {
            await store.UpsertEntityAsync("RoundCaptures", new RoundCaptureEntity
            {
                PartitionKey  = sessionId,
                RowKey        = $"{sessionId}_{i}",
                SessionId     = sessionId,
                RoundNumber   = i,
                TargetEmotion = _emotions[i - 1],
                Score         = i * 2,
                RawConfidence = i * 0.2,
                HeadPoseYaw   = 0,
                HeadPosePitch = 0,
                HeadPoseValid = true,
                ImageBlobUrl  = string.Empty,       // blob won't exist in Azurite → placeholder URL
                CapturedAt    = DateTimeOffset.UtcNow.AddSeconds(-i * 5),
            });
        }
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRecap_CompletedSession_Returns200With5Rounds()
    {
        await using var factory = CreateFactory();
        var store = factory.Services.GetRequiredService<ITableStorageService>();

        var userId    = "recap-user-1";
        var sessionId = Guid.NewGuid().ToString("N");

        await SeedSessionAsync(store, userId, sessionId, isPersonalBest: true);
        await SeedRoundsAsync(store, sessionId);

        var client   = factory.CreateClient();
        var response = await client.GetAsync($"/api/recap/{sessionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("sessionId").GetString().Should().Be(sessionId);
        json.GetProperty("displayName").GetString().Should().Be("Test Player");
        json.GetProperty("totalScore").GetInt32().Should().Be(30);
        json.GetProperty("isPersonalBest").GetBoolean().Should().BeTrue();

        var rounds = json.GetProperty("rounds");
        rounds.GetArrayLength().Should().Be(5);
        rounds[0].GetProperty("roundNumber").GetInt32().Should().Be(1);
        rounds[0].GetProperty("targetEmotion").GetString().Should().Be("Happiness");
        rounds[0].GetProperty("score").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task GetRecap_ExpiredNonBestSession_Returns410()
    {
        await using var factory = CreateFactory();
        var store = factory.Services.GetRequiredService<ITableStorageService>();

        var userId    = "recap-user-2";
        var sessionId = Guid.NewGuid().ToString("N");

        await SeedSessionAsync(store, userId, sessionId,
            isPersonalBest: false,
            expiresAt: DateTimeOffset.UtcNow.AddSeconds(-1)); // already expired

        var client   = factory.CreateClient();
        var response = await client.GetAsync($"/api/recap/{sessionId}");

        response.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task GetRecap_UnknownSessionId_Returns404()
    {
        await using var factory = CreateFactory();
        var client   = factory.CreateClient();

        var response = await client.GetAsync($"/api/recap/{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetRecap_RequiresNoAuthHeader()
    {
        await using var factory = CreateFactory();
        var store = factory.Services.GetRequiredService<ITableStorageService>();

        var userId    = "recap-user-3";
        var sessionId = Guid.NewGuid().ToString("N");

        await SeedSessionAsync(store, userId, sessionId, isPersonalBest: true);
        await SeedRoundsAsync(store, sessionId);

        // Deliberately no X-Test-User-Id header — recap must be publicly accessible.
        var client   = factory.CreateClient();
        var response = await client.GetAsync($"/api/recap/{sessionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetRecap_PersonalBest_HasPublicCacheControl()
    {
        await using var factory = CreateFactory();
        var store = factory.Services.GetRequiredService<ITableStorageService>();

        var userId    = "recap-user-4";
        var sessionId = Guid.NewGuid().ToString("N");

        await SeedSessionAsync(store, userId, sessionId, isPersonalBest: true);
        await SeedRoundsAsync(store, sessionId);

        var client   = factory.CreateClient();
        var response = await client.GetAsync($"/api/recap/{sessionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl?.ToString().Should().Contain("public");
        response.Headers.CacheControl?.ToString().Should().Contain("max-age=3600");
    }
}
