using Azure;
using Azure.Data.Tables;
using MediatR;
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
    public string PartitionKey { get; set; } = "Player"; // Constant shard key
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

/// <summary>Starts a new game session for the authenticated user.</summary>
public sealed record StartSessionCommand(string UserId, string DisplayName, string DeviceType)
    : IRequest<StartSessionResult>;

/// <summary>Returns the new session ID and the 5 fixed target-emotion rounds (FR-009).</summary>
public sealed record StartSessionResult(
    string SessionId,
    IReadOnlyList<RoundInfo> Rounds);

public sealed record RoundInfo(int RoundNumber, string TargetEmotion);

public sealed class StartSessionHandler : IRequestHandler<StartSessionCommand, StartSessionResult>
{
    // FR-009: canonical order — NEVER shuffled.
    private static readonly string[] EmotionOrder =
        ["Happiness", "Surprise", "Anger", "Sadness", "Fear"];

    private readonly ITableStorageService _tableStorage;

    public StartSessionHandler(ITableStorageService tableStorage)
        => _tableStorage = tableStorage;

    public async Task<StartSessionResult> Handle(
        StartSessionCommand command, CancellationToken cancellationToken)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var now       = DateTimeOffset.UtcNow;

        // Upsert the Player record (update LastSeenAt).
        var player = new PlayerEntity
        {
            PartitionKey = "Player",
            RowKey       = command.UserId,
            UserId       = command.UserId,
            DisplayName  = command.DisplayName,
            LastSeenAt   = now
        };
        await _tableStorage.UpsertEntityAsync("Players", player, cancellationToken);

        // Create the new GameSession.
        var session = new GameSessionEntity
        {
            PartitionKey = command.UserId,
            RowKey       = sessionId,
            UserId       = command.UserId,
            SessionId    = sessionId,
            DisplayName  = command.DisplayName,
            DeviceType   = command.DeviceType,
            IsCompleted  = false,
            TotalScore   = 0,
            StartedAt    = now
        };
        await _tableStorage.UpsertEntityAsync("GameSessions", session, cancellationToken);

        var rounds = EmotionOrder
            .Select((emotion, i) => new RoundInfo(i + 1, emotion))
            .ToArray();

        return new StartSessionResult(sessionId, rounds);
    }
}
