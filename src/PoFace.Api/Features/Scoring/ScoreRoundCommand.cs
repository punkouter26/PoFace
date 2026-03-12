using Azure;
using Azure.Data.Tables;
using MediatR;

namespace PoFace.Api.Features.Scoring;

// ── Command ──────────────────────────────────────────────────────────────────

/// <summary>Sent by ScoringEndpoints when a client POSTs a captured JPEG frame.</summary>
public sealed record ScoreRoundCommand(
    string SessionId,
    string UserId,
    int RoundNumber,
    string TargetEmotion,
    byte[] ImageBytes
) : IRequest<ScoreRoundResult>;

// ── Result ───────────────────────────────────────────────────────────────────

/// <summary>Raw Google Cloud Vision face attributes, returned as a nested object.</summary>
public sealed record ScoreRoundDiagnostics(
    double DetectionConfidence,
    double LandmarkingConfidence,
    string HeadwearLikelihood,
    string JoyLikelihood,
    string SorrowLikelihood,
    string AngerLikelihood,
    string SurpriseLikelihood,
    string BlurLevel,
    string ExposureLevel
);

/// <summary>Serialized directly as the HTTP 200 JSON response body.</summary>
public sealed record ScoreRoundResult(
    int     RoundNumber,
    string  TargetEmotion,
    int     Score,
    double  RawConfidence,
    string  QualityLabel,
    bool    HeadPoseValid,
    double? HeadPoseYaw,
    double? HeadPosePitch,
    double? HeadPoseRoll,
    bool    FaceDetected,
    string  ImageUrl,
    ScoreRoundDiagnostics Diagnostics
);

// ── Table Entity ─────────────────────────────────────────────────────────────

/// <summary>
/// Persisted to Azure Table Storage table <c>RoundCaptures</c>.
/// PartitionKey = SessionId, RowKey = {SessionId}_{RoundNumber}.
/// </summary>
public sealed class RoundCaptureEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string SessionId { get; set; } = string.Empty;
    public int RoundNumber { get; set; }
    public string TargetEmotion { get; set; } = string.Empty;
    public int Score { get; set; }
    public double RawConfidence { get; set; }
    public double HeadPoseYaw { get; set; }
    public double HeadPosePitch { get; set; }
    public bool HeadPoseValid { get; set; }
    public string ImageBlobUrl { get; set; } = string.Empty;
    public DateTimeOffset CapturedAt { get; set; }
}
