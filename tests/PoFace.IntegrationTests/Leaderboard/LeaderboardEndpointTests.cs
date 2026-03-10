using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Azure.Data.Tables;
using Microsoft.Extensions.DependencyInjection;
using PoFace.Api.Features.Leaderboard;
using PoFace.IntegrationTests.Infrastructure;

namespace PoFace.IntegrationTests.Leaderboard;

/// <summary>
/// Integration tests for GET /api/leaderboard.
/// Seeds the Leaderboard table directly via <see cref="ILeaderboardTableRepository"/>
/// and asserts the endpoint response contract.
/// </summary>
public sealed class LeaderboardEndpointTests : IClassFixture<AzuriteFixture>
{
    private readonly AzuriteFixture _azurite;

    public LeaderboardEndpointTests(AzuriteFixture azurite) => _azurite = azurite;

    private PoFaceWebAppFactory CreateFactory() => new(_azurite.ConnectionString);

    private static string Year => DateTime.UtcNow.Year.ToString();

    private static async Task ResetLeaderboardAsync(IServiceProvider services)
    {
        var tableService = services.GetRequiredService<TableServiceClient>();
        var table = tableService.GetTableClient("Leaderboard");
        try
        {
            await table.DeleteAsync();
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            // First run: table does not exist yet.
        }

        await table.CreateIfNotExistsAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task SeedEntryAsync(
        ILeaderboardTableRepository repo,
        string userId,
        string displayName,
        int score,
        DateTimeOffset achievedAt)
    {
        await repo.UpsertEntryAsync(new LeaderboardEntity
        {
            PartitionKey = Year,
            RowKey       = userId,
            DisplayName  = displayName,
            TotalScore   = score,
            SessionId    = $"session-{userId}",
            RecapUrl     = $"/recap/session-{userId}",
            DeviceType   = "Desktop",
            AchievedAt   = achievedAt,
        });
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetLeaderboard_ReturnsSortedByScoreDescending()
    {
        await using var factory = CreateFactory();
        await ResetLeaderboardAsync(factory.Services);
        var repo = factory.Services.GetRequiredService<ILeaderboardTableRepository>();

        var now = DateTimeOffset.UtcNow;
        await SeedEntryAsync(repo, "user-a", "Alice", 40, now.AddMinutes(-10));
        await SeedEntryAsync(repo, "user-b", "Bob",   30, now);

        var client   = factory.CreateClient();
        var response = await client.GetAsync("/api/leaderboard");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json    = await response.Content.ReadFromJsonAsync<JsonElement>();
        var entries = json.GetProperty("entries");

        entries.GetArrayLength().Should().Be(2);
        entries[0].GetProperty("displayName").GetString().Should().Be("Alice");
        entries[0].GetProperty("rank").GetInt32().Should().Be(1);
        entries[0].GetProperty("totalScore").GetInt32().Should().Be(40);
        entries[1].GetProperty("displayName").GetString().Should().Be("Bob");
        entries[1].GetProperty("rank").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task GetLeaderboard_OneEntryPerUser()
    {
        await using var factory = CreateFactory();
        await ResetLeaderboardAsync(factory.Services);
        var repo = factory.Services.GetRequiredService<ILeaderboardTableRepository>();

        var now = DateTimeOffset.UtcNow;
        // Seed only one entry per user (as the upsert strategy guarantees).
        await SeedEntryAsync(repo, "user-x", "Xavier", 28, now);

        var client   = factory.CreateClient();
        var response = await client.GetAsync("/api/leaderboard");
        var json     = await response.Content.ReadFromJsonAsync<JsonElement>();

        json.GetProperty("count").GetInt32().Should().Be(1);
        json.GetProperty("entries").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task GetLeaderboard_TieBreak_MoreRecentRanksHigher()
    {
        await using var factory = CreateFactory();
        await ResetLeaderboardAsync(factory.Services);
        var repo = factory.Services.GetRequiredService<ILeaderboardTableRepository>();

        var older  = DateTimeOffset.UtcNow.AddMinutes(-30);
        var recent = DateTimeOffset.UtcNow;

        await SeedEntryAsync(repo, "user-p", "Pat",  25, older);     // same score, older
        await SeedEntryAsync(repo, "user-q", "Quinn", 25, recent);   // same score, newer

        var client   = factory.CreateClient();
        var response = await client.GetAsync("/api/leaderboard");
        var json     = await response.Content.ReadFromJsonAsync<JsonElement>();

        var entries = json.GetProperty("entries");
        entries[0].GetProperty("displayName").GetString().Should().Be("Quinn"); // more recent wins
    }

    [Fact]
    public async Task GetLeaderboard_ResponseContainsOnlyContractFields_AndNoSensitiveData()
    {
        await using var factory = CreateFactory();
        await ResetLeaderboardAsync(factory.Services);
        var repo = factory.Services.GetRequiredService<ILeaderboardTableRepository>();

        await SeedEntryAsync(repo, "user-z", "Zed", 33, DateTimeOffset.UtcNow);

        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/leaderboard");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var entry = json.GetProperty("entries")[0];

        var names = entry.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray();
        names.Should().BeEquivalentTo(["rank", "userId", "displayName", "totalScore", "deviceType", "recapUrl", "achievedAt"]);

        var payload = json.ToString();
        payload.Should().NotContain("@", "email addresses must not be emitted");
        payload.ToLowerInvariant().Should().NotContain("oid");
        payload.ToLowerInvariant().Should().NotContain("access_token");
    }
}
