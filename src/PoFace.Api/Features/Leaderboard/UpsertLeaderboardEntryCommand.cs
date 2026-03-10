using MediatR;

namespace PoFace.Api.Features.Leaderboard;

// ── Command ───────────────────────────────────────────────────────────────────

// [BestMatchUpsertStrategy] — writes the leaderboard entry only when the
// incoming score strictly exceeds the player's current best-match score.
public sealed record UpsertLeaderboardEntryCommand(
    string         UserId,
    string         DisplayName,
    int            TotalScore,
    string         SessionId,
    string         RecapUrl,
    string         DeviceType,
    DateTimeOffset AchievedAt)
    : IRequest<UpsertLeaderboardEntryResult>;

public sealed record UpsertLeaderboardEntryResult(bool IsPersonalBest);

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class UpsertLeaderboardEntryHandler
    : IRequestHandler<UpsertLeaderboardEntryCommand, UpsertLeaderboardEntryResult>
{
    private readonly ILeaderboardTableRepository _repo;

    public UpsertLeaderboardEntryHandler(ILeaderboardTableRepository repo)
        => _repo = repo;

    public async Task<UpsertLeaderboardEntryResult> Handle(
        UpsertLeaderboardEntryCommand command, CancellationToken cancellationToken)
    {
        var year     = DateTime.UtcNow.Year.ToString();
        var existing = await _repo.GetEntryAsync(year, command.UserId, cancellationToken);

        // BestMatchUpsertStrategy: no-op if new score is not strictly higher.
        if (existing is not null && command.TotalScore <= existing.TotalScore)
            return new UpsertLeaderboardEntryResult(IsPersonalBest: false);

        var entry = new LeaderboardEntity
        {
            PartitionKey = year,
            RowKey       = command.UserId,
            DisplayName  = command.DisplayName,
            TotalScore   = command.TotalScore,
            SessionId    = command.SessionId,
            RecapUrl     = command.RecapUrl,
            DeviceType   = command.DeviceType,
            AchievedAt   = command.AchievedAt,
        };

        await _repo.UpsertEntryAsync(entry, cancellationToken);
        return new UpsertLeaderboardEntryResult(IsPersonalBest: true);
    }
}
