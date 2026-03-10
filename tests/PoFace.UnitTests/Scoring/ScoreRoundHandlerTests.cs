using PoFace.Api.Features.Scoring;
using PoFace.Api.Infrastructure.Storage;

namespace PoFace.UnitTests.Scoring;

public sealed class ScoreRoundHandlerTests
{
    private readonly Mock<IFaceAnalysisService> _faceAnalysis = new();
    private readonly Mock<IBlobStorageService>  _blobStorage  = new();
    private readonly Mock<ITableStorageService> _tableStorage = new();

    private ScoreRoundHandler CreateHandler()
        => new(_faceAnalysis.Object, _blobStorage.Object, _tableStorage.Object);

    private static ScoreRoundCommand BuildCommand(string emotion = "Happiness")
        => new("session-1", "user-1", 1, emotion, new byte[] { 0xFF, 0xD8, 0xFF }); // minimal JPEG header

    private void SetupBlob(string url = "https://blob.example.com/round-1.jpg")
        => _blobStorage
            .Setup(b => b.UploadRoundImageAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(url);

    private void SetupTable()
        => _tableStorage
            .Setup(t => t.UpsertEntityAsync(
                It.IsAny<string>(), It.IsAny<RoundCaptureEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

    [Fact]
    public async Task WhenHeadPoseValid_ScoreEqualsRoundedConfidenceTimesTen()
    {
        _faceAnalysis
            .Setup(f => f.AnalyzeFrameAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnalysisResult
            {
                FaceDetected            = true,
                EmotionLabel            = "Happiness",
                TargetEmotionConfidence = 0.74,
                HeadPoseYaw             = 5,
                HeadPosePitch           = 3,
                HeadPoseValid           = true,
                Score                   = 7   // Round(0.74 * 10) = 7
            });

        SetupBlob();
        SetupTable();

        var result = await CreateHandler().Handle(BuildCommand(), CancellationToken.None);

        result.Score.Should().Be(7);
        result.HeadPoseValid.Should().BeTrue();
        result.FaceDetected.Should().BeTrue();
    }

    [Fact]
    public async Task WhenHeadPoseInvalid_ScoreIsZero()
    {
        _faceAnalysis
            .Setup(f => f.AnalyzeFrameAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnalysisResult
            {
                FaceDetected            = true,
                EmotionLabel            = "Happiness",
                TargetEmotionConfidence = 0.80,
                HeadPoseYaw             = 25,  // > 20 — invalid
                HeadPosePitch           = 0,
                HeadPoseValid           = false,
                Score                   = 0
            });

        SetupBlob();
        SetupTable();

        var result = await CreateHandler().Handle(BuildCommand(), CancellationToken.None);

        result.Score.Should().Be(0);
        result.HeadPoseValid.Should().BeFalse();
    }

    [Fact]
    public async Task WhenFaceNotDetected_ScoreIsZeroAndHeadPoseFieldsAreNull()
    {
        _faceAnalysis
            .Setup(f => f.AnalyzeFrameAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnalysisResult
            {
                FaceDetected            = false,
                EmotionLabel            = "Happiness",
                TargetEmotionConfidence = 0,
                HeadPoseYaw             = 0,
                HeadPosePitch           = 0,
                HeadPoseValid           = false,
                Score                   = 0
            });

        SetupBlob();
        SetupTable();

        var result = await CreateHandler().Handle(BuildCommand(), CancellationToken.None);

        result.Score.Should().Be(0);
        result.FaceDetected.Should().BeFalse();
        result.RawConfidence.Should().Be(0);
        result.HeadPoseYaw.Should().BeNull();
        result.HeadPosePitch.Should().BeNull();
    }

    [Fact]
    public async Task Handle_AlwaysUploadsImageRegardlessOfFaceDetection()
    {
        _faceAnalysis
            .Setup(f => f.AnalyzeFrameAsync(
                It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnalysisResult
            {
                FaceDetected = false,
                EmotionLabel = "Happiness",
                Score        = 0
            });

        SetupBlob("https://blob.example.com/round-1.jpg");
        SetupTable();

        var result = await CreateHandler().Handle(BuildCommand(), CancellationToken.None);

        result.ImageUrl.Should().Be("https://blob.example.com/round-1.jpg");
        _blobStorage.Verify(b =>
            b.UploadRoundImageAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<Stream>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
