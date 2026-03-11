namespace PoFace.Api.Features.Scoring;

/// <summary>
/// Transient result from <see cref="IFaceAnalysisService.AnalyzeFrameAsync"/>.
/// Never persisted to storage — consumed by <see cref="ScoreRoundHandler"/>.
/// </summary>
public sealed record AnalysisResult
{
    /// <summary>The highest-confidence emotion label returned by the Face API.</summary>
    public string EmotionLabel { get; init; } = string.Empty;

    /// <summary>Confidence [0–1] for the <em>target</em> emotion specifically.</summary>
    public double TargetEmotionConfidence { get; init; }

    /// <summary>Human-readable quality bucket: High, Medium, Low, or Unknown.</summary>
    public string QualityLabel { get; init; } = string.Empty;

    // ── Head pose ──────────────────────────────────────────────────────────
    public double HeadPoseYaw   { get; init; }
    public double HeadPosePitch { get; init; }
    public double HeadPoseRoll  { get; init; }

    /// <summary>False when Abs(Yaw) > 20 or Abs(Pitch) > 20.</summary>
    public bool HeadPoseValid { get; init; }

    // ── Google Cloud Vision attributes ─────────────────────────────────────
    /// <summary>Face detection confidence [0–1] returned by Google Vision.</summary>
    public double DetectionConfidence   { get; init; }
    /// <summary>Landmark placement confidence [0–1] returned by Google Vision.</summary>
    public double LandmarkingConfidence { get; init; }

    // Emotion likelihoods (None / Low / Medium / High)
    public string JoyLikelihood       { get; init; } = string.Empty;
    public string SorrowLikelihood    { get; init; } = string.Empty;
    public string AngerLikelihood     { get; init; } = string.Empty;
    public string SurpriseLikelihood  { get; init; } = string.Empty;
    public string HeadwearLikelihood  { get; init; } = string.Empty;

    // Image quality signals
    public string BlurLevel     { get; init; } = string.Empty;
    public string ExposureLevel { get; init; } = string.Empty;

    /// <summary>Final score: HeadPoseValid ? Round(Confidence * 10) : 0</summary>
    public int Score { get; init; }

    /// <summary>False when the Face API returned an empty detection list.</summary>
    public bool FaceDetected { get; init; }
}
