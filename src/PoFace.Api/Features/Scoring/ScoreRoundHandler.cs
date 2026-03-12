using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PoFace.Api.Infrastructure.Storage;
using PoFace.Api.Infrastructure.Telemetry;

namespace PoFace.Api.Features.Scoring;

/// <summary>
/// MediatR handler for <see cref="ScoreRoundCommand"/>.
/// Orchestrates: Face API analysis → blob upload → table persistence → OTel recording.
/// </summary>
public sealed class ScoreRoundHandler : IRequestHandler<ScoreRoundCommand, ScoreRoundResult>
{
    private readonly IFaceAnalysisService _faceAnalysis;
    private readonly IBlobStorageService _blobStorage;
    private readonly ITableStorageService _tableStorage;
    private readonly ILogger<ScoreRoundHandler> _logger;

    public ScoreRoundHandler(
        IFaceAnalysisService faceAnalysis,
        IBlobStorageService blobStorage,
        ITableStorageService tableStorage,
        ILogger<ScoreRoundHandler>? logger = null)
    {
        _faceAnalysis = faceAnalysis;
        _blobStorage  = blobStorage;
        _tableStorage = tableStorage;
        _logger = logger ?? NullLogger<ScoreRoundHandler>.Instance;
    }

    public async Task<ScoreRoundResult> Handle(
        ScoreRoundCommand command, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Round scoring start -> SessionId={SessionId}, RoundNumber={RoundNumber}, UserId={UserId}, TargetEmotion={TargetEmotion}, PayloadBytes={PayloadBytes}",
            command.SessionId,
            command.RoundNumber,
            command.UserId,
            command.TargetEmotion,
            command.ImageBytes.Length);

        // 0. Idempotency guard — block re-scoring an already-scored round.
        var existingCapture = await _tableStorage.GetEntityAsync<RoundCaptureEntity>(
            "RoundCaptures", command.SessionId, $"{command.SessionId}_{command.RoundNumber}", cancellationToken);
        if (existingCapture is not null)
        {
            _logger.LogWarning(
                "Round re-score attempt blocked -> SessionId={SessionId}, RoundNumber={RoundNumber}",
                command.SessionId, command.RoundNumber);
            return new ScoreRoundResult(
                RoundNumber:   existingCapture.RoundNumber,
                TargetEmotion: existingCapture.TargetEmotion,
                Score:         existingCapture.Score,
                RawConfidence: existingCapture.RawConfidence,
                QualityLabel:  "Unknown",
                HeadPoseValid: existingCapture.HeadPoseValid,
                HeadPoseYaw:   existingCapture.HeadPoseValid ? existingCapture.HeadPoseYaw   : null,
                HeadPosePitch: existingCapture.HeadPoseValid ? existingCapture.HeadPosePitch : null,
                HeadPoseRoll:  null,
                FaceDetected:  existingCapture.RawConfidence > 0,
                ImageUrl:      existingCapture.ImageBlobUrl,
                Diagnostics:   new ScoreRoundDiagnostics(0, 0, "", "", "", "", "", "", ""));
        }

        // 1. Analyse the captured frame via Azure Face API.
        var analysis = await _faceAnalysis.AnalyzeFrameAsync(
            command.ImageBytes, command.TargetEmotion, cancellationToken);

        // 2. Upload image to Blob Storage regardless of face detection outcome.
        using var imageStream = new MemoryStream(command.ImageBytes);
        var imageUrl = await _blobStorage.UploadRoundImageAsync(
            command.UserId, command.SessionId, command.RoundNumber,
            imageStream, cancellationToken);

        // 3. Persist the RoundCapture entity to Table Storage.
        var entity = new RoundCaptureEntity
        {
            PartitionKey  = command.SessionId,
            RowKey        = $"{command.SessionId}_{command.RoundNumber}",
            SessionId     = command.SessionId,
            RoundNumber   = command.RoundNumber,
            TargetEmotion = command.TargetEmotion,
            Score         = analysis.Score,
            RawConfidence = analysis.TargetEmotionConfidence,
            HeadPoseYaw   = analysis.HeadPoseYaw,
            HeadPosePitch = analysis.HeadPosePitch,
            HeadPoseValid = analysis.HeadPoseValid,
            ImageBlobUrl  = imageUrl,
            CapturedAt    = DateTimeOffset.UtcNow,
            LandmarksJson = analysis.Landmarks.Count > 0
                ? JsonSerializer.Serialize(analysis.Landmarks)
                : null
        };

        await _tableStorage.UpsertEntityAsync("RoundCaptures", entity, cancellationToken);

        // 4. Record OTel gauge (emotion intensity).
        OtelMetrics.RecordEmotionIntensity(
            command.TargetEmotion.ToLowerInvariant(),
            analysis.TargetEmotionConfidence);

        _logger.LogInformation(
            "Round scoring complete -> SessionId={SessionId}, RoundNumber={RoundNumber}, TargetEmotion={TargetEmotion}, FaceDetected={FaceDetected}, RawConfidence={RawConfidence}, HeadPoseValid={HeadPoseValid}, HeadPoseYaw={HeadPoseYaw}, HeadPosePitch={HeadPosePitch}, FinalRoundScore={FinalRoundScore}, ImageUrl={ImageUrl}",
            command.SessionId,
            command.RoundNumber,
            command.TargetEmotion,
            analysis.FaceDetected,
            analysis.TargetEmotionConfidence,
            analysis.HeadPoseValid,
            analysis.HeadPoseYaw,
            analysis.HeadPosePitch,
            analysis.Score,
            imageUrl);

        // 5. Build the HTTP response record.
        return new ScoreRoundResult(
            RoundNumber   : command.RoundNumber,
            TargetEmotion : command.TargetEmotion,
            Score         : analysis.Score,
            RawConfidence : analysis.TargetEmotionConfidence,
            QualityLabel  : analysis.QualityLabel,
            HeadPoseValid : analysis.HeadPoseValid,
            HeadPoseYaw   : analysis.FaceDetected ? analysis.HeadPoseYaw   : null,
            HeadPosePitch : analysis.FaceDetected ? analysis.HeadPosePitch : null,
            HeadPoseRoll  : analysis.FaceDetected ? analysis.HeadPoseRoll  : null,
            FaceDetected  : analysis.FaceDetected,
            ImageUrl      : imageUrl,
            Diagnostics   : new ScoreRoundDiagnostics(
                DetectionConfidence   : analysis.DetectionConfidence,
                LandmarkingConfidence : analysis.LandmarkingConfidence,
                HeadwearLikelihood    : analysis.HeadwearLikelihood,
                JoyLikelihood         : analysis.JoyLikelihood,
                SorrowLikelihood      : analysis.SorrowLikelihood,
                AngerLikelihood       : analysis.AngerLikelihood,
                SurpriseLikelihood    : analysis.SurpriseLikelihood,
                BlurLevel             : analysis.BlurLevel,
                ExposureLevel         : analysis.ExposureLevel,
                Landmarks             : analysis.Landmarks)
        );
    }
}
