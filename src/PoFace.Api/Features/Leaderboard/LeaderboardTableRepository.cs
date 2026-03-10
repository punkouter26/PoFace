using Azure;
using Azure.Data.Tables;

namespace PoFace.Api.Features.Leaderboard;

// ── Entity ────────────────────────────────────────────────────────────────────

/// <summary>
/// Azure Table Storage row in the <c>Leaderboard</c> table.
/// PartitionKey = Year (e.g. "2026"), RowKey = UserId — one entry per user per year.
/// </summary>
public sealed class LeaderboardEntity : ITableEntity
{
    public string         PartitionKey { get; set; } = string.Empty; // Year
    public string         RowKey       { get; set; } = string.Empty; // UserId
    public ETag           ETag         { get; set; }
    public DateTimeOffset? Timestamp   { get; set; }

    public string         DisplayName { get; set; } = string.Empty;
    public int            TotalScore  { get; set; }
    public string         SessionId   { get; set; } = string.Empty;
    public string         RecapUrl    { get; set; } = string.Empty;
    public string         DeviceType  { get; set; } = string.Empty;
    public DateTimeOffset AchievedAt  { get; set; }
}

// ── Repository Contract ───────────────────────────────────────────────────────

public interface ILeaderboardTableRepository
{
    Task<LeaderboardEntity?> GetEntryAsync(
        string year, string userId, CancellationToken ct = default);

    Task UpsertEntryAsync(LeaderboardEntity entry, CancellationToken ct = default);

    Task<IReadOnlyList<LeaderboardEntity>> GetEntriesForYearAsync(
        string year, CancellationToken ct = default);
}

// ── Repository Implementation ─────────────────────────────────────────────────

public sealed class LeaderboardTableRepository : ILeaderboardTableRepository
{
    private const string TableName = "Leaderboard";
    private readonly TableServiceClient _tableService;

    public LeaderboardTableRepository(TableServiceClient tableService)
        => _tableService = tableService;

    public async Task<LeaderboardEntity?> GetEntryAsync(
        string year, string userId, CancellationToken ct = default)
    {
        var table = _tableService.GetTableClient(TableName);
        await table.CreateIfNotExistsAsync(ct);

        try
        {
            var response = await table.GetEntityAsync<LeaderboardEntity>(
                year, userId, cancellationToken: ct);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task UpsertEntryAsync(LeaderboardEntity entry, CancellationToken ct = default)
    {
        var table = _tableService.GetTableClient(TableName);
        await table.CreateIfNotExistsAsync(ct);
        await table.UpsertEntityAsync(entry, TableUpdateMode.Replace, ct);
    }

    public async Task<IReadOnlyList<LeaderboardEntity>> GetEntriesForYearAsync(
        string year, CancellationToken ct = default)
    {
        var table = _tableService.GetTableClient(TableName);
        await table.CreateIfNotExistsAsync(ct);

        var results = new List<LeaderboardEntity>();
        await foreach (var entity in table.QueryAsync<LeaderboardEntity>(
            filter: $"PartitionKey eq '{year}'",
            cancellationToken: ct))
        {
            results.Add(entity);
        }

        return results;
    }
}
