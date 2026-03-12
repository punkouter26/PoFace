using System.Text.RegularExpressions;
using Azure.Data.Tables;
using MediatR;
using PoFace.Api.Features.GameSession;
using PoFace.Api.Features.Scoring;
using PoFace.Api.Infrastructure.Storage;

namespace PoFace.Api.Features.Recap;

// ── Query ─────────────────────────────────────────────────────────────────────

public sealed record GetRecapQuery(string SessionId) : IRequest<GetRecapResult>;

// ── Result ────────────────────────────────────────────────────────────────────

public sealed record GetRecapResult(RecapDto? Recap, RecapStatus Status);

public enum RecapStatus { Found, NotFound, Gone, Incomplete }

public sealed record RecapDto(
    string SessionId,
    string UserId,
    string DisplayName,
    int    TotalScore,
    bool   IsPersonalBest,
    DateTimeOffset                CompletedAt,
    IReadOnlyList<RecapRoundDto>  Rounds);

public sealed record RecapRoundDto(
    int            RoundNumber,
    string         TargetEmotion,
    int            Score,
    bool           HeadPoseValid,
    string         ImageUrl,
    DateTimeOffset CapturedAt);

// ── Handler ───────────────────────────────────────────────────────────────────

public sealed class GetRecapHandler : IRequestHandler<GetRecapQuery, GetRecapResult>
{
    private const int RoundCount = 5;

    // SessionId is always Guid.NewGuid().ToString("N") — 32 hex chars. Validates
    // before embedding in an OData filter string to prevent injection (SC-008).
    private static readonly Regex SessionIdPattern =
        new(@"^[0-9a-f]{32}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IGameSessionLookupService _sessionLookup;
    private readonly ITableStorageService _tableStorage;
    private readonly IBlobImageRepository _blobImages;

    public GetRecapHandler(
        IGameSessionLookupService sessionLookup,
        ITableStorageService tableStorage,
        IBlobImageRepository blobImages)
    {
        _sessionLookup = sessionLookup;
        _tableStorage = tableStorage;
        _blobImages   = blobImages;
    }

    public async Task<GetRecapResult> Handle(GetRecapQuery query, CancellationToken ct)
    {
        // Guard: reject any SessionId that doesn't match the expected hex format
        // to prevent OData filter injection in the cross-partition scan below.
        if (!SessionIdPattern.IsMatch(query.SessionId))
            return new GetRecapResult(null, RecapStatus.NotFound);

        // ── Step 1: Find the GameSession by RowKey (cross-partition query) ─────
        var session = await _sessionLookup.GetBySessionIdAsync(query.SessionId, ct);

        if (session is null)
            return new GetRecapResult(null, RecapStatus.NotFound);
        // Guard: incomplete sessions have no valid recap yet (SC-031).
        if (!session.IsCompleted)
            return new GetRecapResult(null, RecapStatus.Incomplete);
        // ── Step 2: Check expiry (FR-030) ─────────────────────────────────────
        if (session.ExpiresAt.HasValue && session.ExpiresAt.Value < DateTimeOffset.UtcNow)
            return new GetRecapResult(null, RecapStatus.Gone);

        // ── Step 3: Load blob URLs (placeholder for missing blobs) ────────────
        var imageUrls = await _blobImages.GetRoundImageUrlsAsync(
            session.UserId, query.SessionId, RoundCount, ct);

        // ── Step 4: Load round captures ───────────────────────────────────────
        var rounds = new List<RecapRoundDto>(RoundCount);
        for (int i = 1; i <= RoundCount; i++)
        {
            var rowKey  = $"{query.SessionId}_{i}";
            var capture = await _tableStorage.GetEntityAsync<RoundCaptureEntity>(
                "RoundCaptures", query.SessionId, rowKey, ct);

            if (capture is not null)
            {
                rounds.Add(new RecapRoundDto(
                    RoundNumber:   capture.RoundNumber,
                    TargetEmotion: capture.TargetEmotion,
                    Score:         capture.Score,
                    HeadPoseValid: capture.HeadPoseValid,
                    ImageUrl:      imageUrls[i - 1],
                    CapturedAt:    capture.CapturedAt));
            }
        }

        var recap = new RecapDto(
            SessionId:      query.SessionId,
            UserId:         session.UserId,
            DisplayName:    session.DisplayName,
            TotalScore:     session.TotalScore,
            IsPersonalBest: session.IsPersonalBest,
            CompletedAt:    session.CompletedAt ?? DateTimeOffset.UtcNow,
            Rounds:         rounds);

        return new GetRecapResult(recap, RecapStatus.Found);
    }
}

public interface IGameSessionLookupService
{
    Task<GameSessionEntity?> GetBySessionIdAsync(string sessionId, CancellationToken ct = default);
}

public sealed class GameSessionLookupService : IGameSessionLookupService
{
    // Validates sessionId before OData filter interpolation to prevent injection (SC-008, OWASP A03).
    private static readonly Regex LookupIdPattern =
        new(@"^[0-9a-f]{32}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly TableServiceClient _tableService;

    public GameSessionLookupService(TableServiceClient tableService) => _tableService = tableService;

    public async Task<GameSessionEntity?> GetBySessionIdAsync(string sessionId, CancellationToken ct = default)
    {
        if (!LookupIdPattern.IsMatch(sessionId))
            return null;

        var sessionTable = _tableService.GetTableClient("GameSessions");
        await sessionTable.CreateIfNotExistsAsync(ct);

        await foreach (var e in sessionTable.QueryAsync<GameSessionEntity>(
            filter: $"RowKey eq '{sessionId}'",
            cancellationToken: ct))
        {
            return e;
        }

        return null;
    }
}
