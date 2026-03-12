using Azure;
using Azure.Data.Tables;
using MediatR;
using PoFace.Api.Features.Scoring;
using PoFace.Api.Infrastructure.Storage;

namespace PoFace.Api.Features.GameSession;

// ═══════════════════════════════════════════════════════════════════════════════
// Table Entities
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Persisted to Azure Table Storage table <c>GameSessions</c>.
/// PartitionKey = UserId, RowKey = SessionId.
/// </summary>
public sealed class GameSessionEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty; // UserId
    public string RowKey       { get; set; } = string.Empty; // SessionId
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string UserId      { get; set; } = string.Empty;
    public string SessionId   { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string DeviceType  { get; set; } = string.Empty;
    public bool   IsAuthenticatedUser { get; set; }
    public bool   IsCompleted { get; set; }
    public int    TotalScore  { get; set; }
    public bool   IsPersonalBest { get; set; }
    public DateTimeOffset StartedAt  { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? ExpiresAt   { get; set; }
}

/// <summary>
/// Persisted to Azure Table Storage table <c>Players</c>.
/// PartitionKey = "Player", RowKey = UserId.
/// </summary>
public sealed class PlayerEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty; // Single-char hex shard (first char of UserId) to avoid hot partition
    public string RowKey       { get; set; } = string.Empty; // UserId
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string UserId      { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset LastSeenAt { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════════
// Start Session
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Starts a new game session for either an authenticated or anonymous player.</summary>
public sealed record StartSessionCommand(string? UserId, string? DisplayName, string DeviceType, bool IsAuthenticatedUser)
    : IRequest<StartSessionResult>;

/// <summary>Returns the new session ID and the 5 fixed target-emotion rounds (FR-009).</summary>
public sealed record StartSessionResult(
    string SessionId,
    IReadOnlyList<RoundInfo> Rounds);

public sealed record RoundInfo(int RoundNumber, string TargetEmotion);

public sealed class StartSessionHandler : IRequestHandler<StartSessionCommand, StartSessionResult>
{
    private readonly ITableStorageService _tableStorage;

    public StartSessionHandler(ITableStorageService tableStorage)
        => _tableStorage = tableStorage;

    public async Task<StartSessionResult> Handle(
        StartSessionCommand command, CancellationToken cancellationToken)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var now       = DateTimeOffset.UtcNow;
        var isAuthenticatedUser = command.IsAuthenticatedUser && !string.IsNullOrWhiteSpace(command.UserId);
        var effectiveUserId = isAuthenticatedUser ? command.UserId! : $"anon-{sessionId}";
        var effectiveDisplayName = isAuthenticatedUser
            ? (string.IsNullOrWhiteSpace(command.DisplayName) ? effectiveUserId : command.DisplayName!)
            : "Anonymous";

        if (isAuthenticatedUser)
        {
            var player = new PlayerEntity
            {
                PartitionKey = effectiveUserId.Length > 0 ? effectiveUserId[..1].ToUpperInvariant() : "0",
                RowKey       = effectiveUserId,
                UserId       = effectiveUserId,
                DisplayName  = effectiveDisplayName,
                LastSeenAt   = now
            };
            await _tableStorage.UpsertEntityAsync("Players", player, cancellationToken);
        }

        // Create the new GameSession.
        var session = new GameSessionEntity
        {
            PartitionKey = effectiveUserId,
            RowKey       = sessionId,
            UserId       = effectiveUserId,
            SessionId    = sessionId,
            DisplayName  = effectiveDisplayName,
            DeviceType   = command.DeviceType,
            IsAuthenticatedUser = isAuthenticatedUser,
            IsCompleted  = false,
            TotalScore   = 0,
            StartedAt    = now
        };
        await _tableStorage.UpsertEntityAsync("GameSessions", session, cancellationToken);

        // FR-009: canonical order — NEVER shuffled (single source of truth in RoundEmotions).
        var rounds = Enumerable.Range(1, 5)
            .Select(i => new RoundInfo(i, RoundEmotions.ForRound(i)))
            .ToArray();

        return new StartSessionResult(sessionId, rounds);
    }
}
