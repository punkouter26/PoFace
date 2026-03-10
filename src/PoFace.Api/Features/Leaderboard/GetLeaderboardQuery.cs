using MediatR;

namespace PoFace.Api.Features.Leaderboard;

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Returns the top-N leaderboard entries for the current calendar year,
/// sorted by TotalScore descending with AchievedAt as the recency tie-break (FR-023).
/// </summary>
public sealed record GetLeaderboardQuery(int Top = 100) : IRequest<LeaderboardResult>;

public sealed record LeaderboardResult(
    string                              Year,
    IReadOnlyList<LeaderboardEntryDto>  Entries,
    int                                 Count);

public sealed record LeaderboardEntryDto(
    int    Rank,
    string UserId,
    string DisplayName,
    int    TotalScore,
    string DeviceType,
    string RecapUrl,
    string AchievedAt);

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetLeaderboardHandler : IRequestHandler<GetLeaderboardQuery, LeaderboardResult>
{
    private const int MaxTop = 500;
    private readonly ILeaderboardTableRepository _repo;

    public GetLeaderboardHandler(ILeaderboardTableRepository repo) => _repo = repo;

    public async Task<LeaderboardResult> Handle(GetLeaderboardQuery query, CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year.ToString();
        var top  = Math.Min(query.Top, MaxTop);

        var raw = await _repo.GetEntriesForYearAsync(year, cancellationToken);

        // FR-023: TotalScore desc; AchievedAt desc as recency tie-break.
        var entries = raw
            .OrderByDescending(e => e.TotalScore)
            .ThenByDescending(e => e.AchievedAt)
            .Take(top)
            .Select((e, i) => new LeaderboardEntryDto(
                Rank:        i + 1,
                UserId:      e.RowKey,
                DisplayName: e.DisplayName,
                TotalScore:  e.TotalScore,
                DeviceType:  e.DeviceType,
                RecapUrl:    e.RecapUrl,
                AchievedAt:  e.AchievedAt.ToString("O")))
            .ToList();

        return new LeaderboardResult(year, entries, entries.Count);
    }
}
