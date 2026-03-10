using Azure.AI.Vision.Face;
using Azure;
using PoFace.Api.Features.Scoring;

namespace PoFace.Api.Features.Scoring;

// ── Interface ─────────────────────────────────────────────────────────────────

/// <summary>Abstracts the Azure Face API call so <see cref="ScoreRoundHandler"/> can be unit-tested.</summary>
public interface IFaceAnalysisService
{
    Task<AnalysisResult> AnalyzeFrameAsync(
        byte[] imageBytes, string targetEmotion, CancellationToken cancellationToken = default);
}

// ── Implementation ────────────────────────────────────────────────────────────

/// <summary>
/// Calls Azure Face API (Detection01 model) to detect head pose and face quality.
///
/// NOTE — Emotion detection requires Azure Face API "Limited Access" approval.
/// See: https://learn.microsoft.com/en-us/azure/ai-services/face/overview#limited-access
/// Until approved this service falls back to <see cref="QualityForRecognition"/> as a
/// confidence proxy so scoring is always non-zero for detected faces.
/// Replace <see cref="MapQualityToConfidence"/> with real emotion logic once approved.
/// </summary>
public sealed class FaceAnalysisService : IFaceAnalysisService
{
    private readonly FaceClient _faceClient;

    public FaceAnalysisService(FaceClient faceClient) => _faceClient = faceClient;

    public async Task<AnalysisResult> AnalyzeFrameAsync(
        byte[] imageBytes, string targetEmotion, CancellationToken cancellationToken = default)
    {
        Response<IReadOnlyList<FaceDetectionResult>> response;

        try
        {
            response = await _faceClient.DetectAsync(
                BinaryData.FromBytes(imageBytes),
                FaceDetectionModel.Detection01,
                FaceRecognitionModel.Recognition04,
                returnFaceId: false,
                returnFaceAttributes: new[]
                {
                    FaceAttributeType.HeadPose,
                    FaceAttributeType.QualityForRecognition
                },
                returnFaceLandmarks: false,
                returnRecognitionModel: false,
                faceIdTimeToLive: null,
                cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 400)
        {
            // Corrupt / non-JPEG data supplied; return no-face result.
            return new AnalysisResult
            {
                FaceDetected = false,
                EmotionLabel = targetEmotion,
                TargetEmotionConfidence = 0,
                HeadPoseYaw = 0,
                HeadPosePitch = 0,
                HeadPoseValid = false,
                Score = 0
            };
        }

        var faces = response.Value;

        if (faces.Count == 0)
        {
            return new AnalysisResult
            {
                FaceDetected = false,
                EmotionLabel = targetEmotion,
                TargetEmotionConfidence = 0,
                HeadPoseYaw = 0,
                HeadPosePitch = 0,
                HeadPoseValid = false,
                Score = 0
            };
        }

        // Use the largest face (first by API convention when no ID requested).
        var face = faces[0];
        var headPose = face.FaceAttributes?.HeadPose;

        double yaw   = headPose?.Yaw   ?? 0;
        double pitch = headPose?.Pitch ?? 0;
        bool   valid = HeadPoseValidator.Validate(yaw, pitch);

        double confidence = MapQualityToConfidence(face.FaceAttributes?.QualityForRecognition);
        int    score      = valid ? (int)Math.Round(confidence * 10) : 0;

        return new AnalysisResult
        {
            FaceDetected              = true,
            EmotionLabel              = targetEmotion,
            TargetEmotionConfidence   = confidence,
            HeadPoseYaw               = yaw,
            HeadPosePitch             = pitch,
            HeadPoseValid             = valid,
            Score                     = score
        };
    }

    // ── Limited-Access fallback ───────────────────────────────────────────────
    //
    // Maps face recognition quality to a representative confidence score.
    // Replace with real emotion property access once Limited Access is granted:
    //   var emotionConfidence = face.FaceAttributes?.Emotion?.<TargetProperty>;
    //
    private static double MapQualityToConfidence(QualityForRecognition? quality)
        => quality switch
        {
            var q when q == QualityForRecognition.High   => 0.80,
            var q when q == QualityForRecognition.Medium => 0.50,
            var q when q == QualityForRecognition.Low    => 0.20,
            _                                            => 0.0
        };
}

/// <summary>
/// Dev/test stub used when <c>AzureFace:Endpoint</c> is absent or set to the
/// placeholder value. Returns a deterministic no-face result without making
/// any outbound HTTP calls, keeping local development and E2E tests self-contained.
/// </summary>
internal sealed class StubFaceAnalysisService : IFaceAnalysisService
{
    public Task<AnalysisResult> AnalyzeFrameAsync(
        byte[] imageBytes, string targetEmotion, CancellationToken cancellationToken = default)
        => Task.FromResult(new AnalysisResult
        {
            FaceDetected            = false,
            EmotionLabel            = targetEmotion,
            TargetEmotionConfidence = 0,
            HeadPoseYaw             = 0,
            HeadPosePitch           = 0,
            HeadPoseValid           = false,
            Score                   = 0
        });
}
