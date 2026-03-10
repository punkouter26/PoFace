using MediatR;
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

    public ScoreRoundHandler(
        IFaceAnalysisService faceAnalysis,
        IBlobStorageService blobStorage,
        ITableStorageService tableStorage)
    {
        _faceAnalysis = faceAnalysis;
        _blobStorage  = blobStorage;
        _tableStorage = tableStorage;
    }

    public async Task<ScoreRoundResult> Handle(
        ScoreRoundCommand command, CancellationToken cancellationToken)
    {
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
            CapturedAt    = DateTimeOffset.UtcNow
        };

        await _tableStorage.UpsertEntityAsync("RoundCaptures", entity, cancellationToken);

        // 4. Record OTel gauge (emotion intensity).
        OtelMetrics.RecordEmotionIntensity(
            command.TargetEmotion.ToLowerInvariant(),
            analysis.TargetEmotionConfidence);

        // 5. Build the HTTP response record.
        return new ScoreRoundResult(
            RoundNumber   : command.RoundNumber,
            TargetEmotion : command.TargetEmotion,
            Score         : analysis.Score,
            RawConfidence : analysis.TargetEmotionConfidence,
            HeadPoseValid : analysis.HeadPoseValid,
            HeadPoseYaw   : analysis.FaceDetected ? analysis.HeadPoseYaw   : null,
            HeadPosePitch : analysis.FaceDetected ? analysis.HeadPosePitch : null,
            FaceDetected  : analysis.FaceDetected,
            ImageUrl      : imageUrl
        );
    }
}
