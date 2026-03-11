using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
    private readonly ILogger<CompleteSessionHandler> _logger;

    public CompleteSessionHandler(
        ITableStorageService tableStorage,
        ISender sender,
        ILogger<CompleteSessionHandler>? logger = null)
    {
        _tableStorage = tableStorage;
        _sender       = sender;
        _logger = logger ?? NullLogger<CompleteSessionHandler>.Instance;
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
        var roundBreakdown = new List<string>(RoundCount);

        for (int round = 1; round <= RoundCount; round++)
        {
            var rowKey = $"{command.SessionId}_{round}";
            var capture = await _tableStorage.GetEntityAsync<RoundCaptureEntity>(
                "RoundCaptures", command.SessionId, rowKey, cancellationToken);

            if (capture is null)
                throw new InvalidOperationException(
                    $"Round {round} of session {command.SessionId} has not been scored.");

            totalScore += capture.Score;
            roundBreakdown.Add($"R{round}:{capture.Score}");
        }

        _logger.LogInformation(
            "Session total calculation -> SessionId={SessionId}, UserId={UserId}, RoundScores={RoundScores}, TotalScore={TotalScore}",
            command.SessionId,
            command.UserId,
            string.Join(", ", roundBreakdown),
            totalScore);

        // Upsert leaderboard via BestMatchUpsertStrategy and get IsPersonalBest.
        var now = DateTimeOffset.UtcNow;
        var isPersonalBest = false;
        if (session.IsAuthenticatedUser)
        {
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

            isPersonalBest = leaderboardResult.IsPersonalBest;
            _logger.LogInformation(
                "Leaderboard evaluation -> SessionId={SessionId}, UserId={UserId}, Eligible=true, IsPersonalBest={IsPersonalBest}",
                command.SessionId,
                command.UserId,
                isPersonalBest);
        }
        else
        {
            _logger.LogInformation(
                "Leaderboard evaluation -> SessionId={SessionId}, UserId={UserId}, Eligible=false (anonymous session), IsPersonalBest=false",
                command.SessionId,
                command.UserId);
        }

        // Mark completed.
        session.IsCompleted    = true;
        session.TotalScore     = totalScore;
        session.IsPersonalBest = isPersonalBest;
        session.CompletedAt    = now;

        if (!isPersonalBest)
            session.ExpiresAt = now.Add(NonBestExpiry);

        await _tableStorage.UpsertEntityAsync("GameSessions", session, cancellationToken);

        _logger.LogInformation(
            "Session completion persisted -> SessionId={SessionId}, UserId={UserId}, TotalScore={TotalScore}, IsPersonalBest={IsPersonalBest}, ExpiresAt={ExpiresAt}",
            command.SessionId,
            command.UserId,
            totalScore,
            isPersonalBest,
            session.ExpiresAt);

        // Increment OTel counter.
        OtelMetrics.SessionCompletionCount.Add(1,
            new System.Collections.Generic.KeyValuePair<string, object?>("userId", command.UserId));

        return new CompleteSessionResult(command.SessionId, totalScore, isPersonalBest, $"/recap/{command.SessionId}");
    }
}
