using System.Security.Cryptography;
using Google.Cloud.Vision.V1;
using Google.Apis.Auth.OAuth2;
using Grpc.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace PoFace.Api.Features.Scoring;

// ── Interface ─────────────────────────────────────────────────────────────────

/// <summary>Abstracts the face analysis API call so <see cref="ScoreRoundHandler"/> can be unit-tested.</summary>
public interface IFaceAnalysisService
{
    Task<AnalysisResult> AnalyzeFrameAsync(
        byte[] imageBytes, string targetEmotion, CancellationToken cancellationToken = default);
}

// ── Implementation ────────────────────────────────────────────────────────────

/// <summary>
/// Calls Google Cloud Vision API (FACE_DETECTION feature) to detect emotion, head pose,
/// and image quality attributes.
///
/// Emotion mapping:
///   Happiness → joyLikelihood
///   Surprise  → surpriseLikelihood
///   Anger     → angerLikelihood
///   Sadness   → sorrowLikelihood
///   Fear      → max(sorrow, surprise) as proxy (no direct Fear in Vision API)
/// </summary>
public sealed class GoogleVisionFaceAnalysisService : IFaceAnalysisService
{
    private readonly ImageAnnotatorClient _client;
    private readonly ILogger<GoogleVisionFaceAnalysisService> _logger;

    public GoogleVisionFaceAnalysisService(ImageAnnotatorClient client, ILogger<GoogleVisionFaceAnalysisService>? logger = null)
    {
        _client = client;
        _logger = logger ?? NullLogger<GoogleVisionFaceAnalysisService>.Instance;
    }

    public async Task<AnalysisResult> AnalyzeFrameAsync(
        byte[] imageBytes, string targetEmotion, CancellationToken cancellationToken = default)
    {
        var payloadHash = ComputePayloadHash(imageBytes);
        _logger.LogInformation(
            "Vision API request -> TargetEmotion={TargetEmotion}, PayloadBytes={PayloadBytes}, PayloadSha256={PayloadSha256}",
            targetEmotion, imageBytes.Length, payloadHash);

        Image image;
        try
        {
            image = Image.FromBytes(imageBytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Vision API request <- Invalid image data for TargetEmotion={TargetEmotion}. Returning score 0.",
                targetEmotion);
            return NoFaceResult(targetEmotion);
        }

        IReadOnlyList<FaceAnnotation> faces;
        try
        {
            faces = await _client.DetectFacesAsync(image,
                callSettings: Google.Api.Gax.Grpc.CallSettings.FromCancellationToken(cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Vision API request <- Exception for TargetEmotion={TargetEmotion}, PayloadSha256={PayloadSha256}. Returning score 0.",
                targetEmotion, payloadHash);
            return NoFaceResult(targetEmotion);
        }

        _logger.LogInformation(
            "Vision API response <- TargetEmotion={TargetEmotion}, PayloadSha256={PayloadSha256}, FaceCount={FaceCount}",
            targetEmotion, payloadHash, faces.Count);

        if (faces.Count == 0)
        {
            _logger.LogInformation(
                "Score calculation -> No face detected. TargetEmotion={TargetEmotion}, FinalRoundScore=0",
                targetEmotion);
            return NoFaceResult(targetEmotion);
        }

        // Use the highest-confidence face.
        var face = faces.OrderByDescending(f => f.DetectionConfidence).First();

        double yaw   = face.PanAngle;
        double pitch = face.TiltAngle;
        double roll  = face.RollAngle;
        bool   valid = HeadPoseValidator.Validate(yaw, pitch);

        double emotionConfidence = GetEmotionConfidence(face, targetEmotion);
        string emotionLabel      = targetEmotion;
        var rawScore             = (int)Math.Round(emotionConfidence * 10);
        int score                = valid ? rawScore : 0;

        string blurLevel     = LikelihoodLabel(face.BlurredLikelihood);
        string exposureLevel = LikelihoodLabel(face.UnderExposedLikelihood) == "None" ? "GoodExposure" : "UnderExposure";

        _logger.LogInformation(
            "Score calculation -> TargetEmotion={TargetEmotion}, EmotionConfidence={EmotionConfidence:0.00}, HeadPoseYaw={HeadPoseYaw:0.0}, HeadPosePitch={HeadPosePitch:0.0}, HeadPoseValid={HeadPoseValid}, DetectionConfidence={DetectionConfidence:0.000}, RawScore={RawScore}, FinalRoundScore={FinalRoundScore}",
            targetEmotion, emotionConfidence, yaw, pitch, valid, face.DetectionConfidence, rawScore, score);

        return new AnalysisResult
        {
            FaceDetected            = true,
            EmotionLabel            = emotionLabel,
            TargetEmotionConfidence = emotionConfidence,
            QualityLabel            = blurLevel == "None" ? "High" : "Low",
            HeadPoseYaw             = yaw,
            HeadPosePitch           = pitch,
            HeadPoseRoll            = roll,
            HeadPoseValid           = valid,
            Score                   = score,
            DetectionConfidence     = face.DetectionConfidence,
            LandmarkingConfidence   = face.LandmarkingConfidence,
            HeadwearLikelihood      = LikelihoodLabel(face.HeadwearLikelihood),
            JoyLikelihood           = LikelihoodLabel(face.JoyLikelihood),
            SorrowLikelihood        = LikelihoodLabel(face.SorrowLikelihood),
            AngerLikelihood         = LikelihoodLabel(face.AngerLikelihood),
            SurpriseLikelihood      = LikelihoodLabel(face.SurpriseLikelihood),
            BlurLevel               = blurLevel,
            ExposureLevel           = exposureLevel,
        };
    }

    // ── Emotion mapping ───────────────────────────────────────────────────────

    private static double GetEmotionConfidence(FaceAnnotation face, string targetEmotion)
        => targetEmotion.ToLowerInvariant() switch
        {
            "happiness" => LikelihoodToConfidence(face.JoyLikelihood),
            "surprise"  => LikelihoodToConfidence(face.SurpriseLikelihood),
            "anger"     => LikelihoodToConfidence(face.AngerLikelihood),
            "sadness"   => LikelihoodToConfidence(face.SorrowLikelihood),
            // Fear has no direct match: use max(sorrow, surprise) as proxy.
            "fear"      => Math.Max(
                               LikelihoodToConfidence(face.SorrowLikelihood),
                               LikelihoodToConfidence(face.SurpriseLikelihood)),
            _           => 0.0
        };

    /// <summary>Maps Google Likelihood enum (1–5) to a [0–1] confidence value.</summary>
    private static double LikelihoodToConfidence(Likelihood likelihood)
        => likelihood switch
        {
            Likelihood.VeryLikely   => 1.00,
            Likelihood.Likely       => 0.75,
            Likelihood.Possible     => 0.45,
            Likelihood.Unlikely     => 0.15,
            Likelihood.VeryUnlikely => 0.0,
            _                       => 0.0   // UNKNOWN
        };

    private static string LikelihoodLabel(Likelihood likelihood)
        => likelihood switch
        {
            Likelihood.VeryLikely   => "High",
            Likelihood.Likely       => "Medium",
            Likelihood.Possible     => "Low",
            _                       => "None"
        };

    private static AnalysisResult NoFaceResult(string targetEmotion) => new()
    {
        FaceDetected            = false,
        EmotionLabel            = targetEmotion,
        TargetEmotionConfidence = 0,
        HeadPoseYaw             = 0,
        HeadPosePitch           = 0,
        HeadPoseValid           = false,
        Score                   = 0
    };

    private static string ComputePayloadHash(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash)[..12];
    }
}

/// <summary>
/// Dev/test stub used when <c>GoogleVision:CredentialJson</c> is absent.
/// Returns a deterministic no-face result without making any outbound HTTP calls.
/// </summary>
internal sealed class StubFaceAnalysisService : IFaceAnalysisService
{
    private readonly ILogger<StubFaceAnalysisService> _logger;

    public StubFaceAnalysisService(ILogger<StubFaceAnalysisService>? logger = null)
        => _logger = logger ?? NullLogger<StubFaceAnalysisService>.Instance;

    public Task<AnalysisResult> AnalyzeFrameAsync(
        byte[] imageBytes, string targetEmotion, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Face analysis stub in use -> TargetEmotion={TargetEmotion}, PayloadBytes={PayloadBytes}. Returning deterministic score 0.",
            targetEmotion,
            imageBytes.Length);

        return Task.FromResult(new AnalysisResult
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
}
