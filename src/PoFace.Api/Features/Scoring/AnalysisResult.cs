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

    public double HeadPoseYaw { get; init; }
    public double HeadPosePitch { get; init; }

    /// <summary>False when Abs(Yaw) > 20 or Abs(Pitch) > 20.</summary>
    public bool HeadPoseValid { get; init; }

    /// <summary>Final score: HeadPoseValid ? Round(Confidence * 10) : 0</summary>
    public int Score { get; init; }

    /// <summary>False when the Face API returned an empty detection list.</summary>
    public bool FaceDetected { get; init; }
}
