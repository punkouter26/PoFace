using MediatR;
using PoFace.Api.Infrastructure.Storage;
using PoFace.Api.Infrastructure.Telemetry;
using PoFace.Api.Features.Scoring;
using PoFace.Api.Features.Leaderboard;

namespace PoFace.Api.Features.GameSession;

// ── Command ───────────────────────────────────────────────────────────────────

/// <summary>Marks the session complete, computes the total score, and sets IsPersonalBest.</summary>
public sealed record CompleteSessionCommand(string SessionId, string UserId)
    : IRequest<CompleteSessionResult>;

public sealed record CompleteSessionResult(
    string SessionId,
    int    TotalScore,
    bool   IsPersonalBest,
    string RecapUrl);

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class CompleteSessionHandler : IRequestHandler<CompleteSessionCommand, CompleteSessionResult>
{
    private const int RoundCount = 5;
    private static readonly TimeSpan NonBestExpiry = TimeSpan.FromDays(1);

    private readonly ITableStorageService _tableStorage;
    private readonly ISender              _sender;

    public CompleteSessionHandler(ITableStorageService tableStorage, ISender sender)
    {
        _tableStorage = tableStorage;
        _sender       = sender;
    }

    public async Task<CompleteSessionResult> Handle(
        CompleteSessionCommand command, CancellationToken cancellationToken)
    {
        // Load the session.
        var session = await _tableStorage.GetEntityAsync<GameSessionEntity>(
            "GameSessions", command.UserId, command.SessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Session {command.SessionId} not found.");

        // Sum scores from the 5 RoundCapture entities.
        int totalScore = 0;
        for (int round = 1; round <= RoundCount; round++)
        {
            var rowKey = $"{command.SessionId}_{round}";
            var capture = await _tableStorage.GetEntityAsync<RoundCaptureEntity>(
                "RoundCaptures", command.SessionId, rowKey, cancellationToken);

            if (capture is null)
                throw new InvalidOperationException(
                    $"Round {round} of session {command.SessionId} has not been scored.");

            totalScore += capture.Score;
        }

        // Upsert leaderboard via BestMatchUpsertStrategy and get IsPersonalBest.
        var now = DateTimeOffset.UtcNow;
        var leaderboardResult = await _sender.Send(
            new UpsertLeaderboardEntryCommand(
                UserId:      command.UserId,
                DisplayName: session.DisplayName,
                TotalScore:  totalScore,
                SessionId:   command.SessionId,
                RecapUrl:    $"/recap/{command.SessionId}",
                DeviceType:  session.DeviceType,
                AchievedAt:  now),
            cancellationToken);

        bool isPersonalBest = leaderboardResult.IsPersonalBest;

        // Mark completed.
        session.IsCompleted    = true;
        session.TotalScore     = totalScore;
        session.IsPersonalBest = isPersonalBest;
        session.CompletedAt    = now;

        if (!isPersonalBest)
            session.ExpiresAt = now.Add(NonBestExpiry);

        await _tableStorage.UpsertEntityAsync("GameSessions", session, cancellationToken);

        // Increment OTel counter.
        OtelMetrics.SessionCompletionCount.Add(1,
            new System.Collections.Generic.KeyValuePair<string, object?>("userId", command.UserId));

        return new CompleteSessionResult(command.SessionId, totalScore, isPersonalBest, $"/recap/{command.SessionId}");
    }
}
