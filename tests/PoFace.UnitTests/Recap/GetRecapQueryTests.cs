using PoFace.Api.Features.GameSession;
using PoFace.Api.Features.Recap;
using PoFace.Api.Features.Scoring;
using PoFace.Api.Infrastructure.Storage;

namespace PoFace.UnitTests.Recap;

public sealed class GetRecapQueryTests
{
    private readonly Mock<IGameSessionLookupService> _sessionLookup = new();
    private readonly Mock<ITableStorageService> _tableStorage = new();
    private readonly Mock<IBlobImageRepository> _blobImages = new();

    private GetRecapHandler CreateHandler()
        => new(_sessionLookup.Object, _tableStorage.Object, _blobImages.Object);

    [Fact]
    public async Task AllFiveBlobsExist_ReturnsFoundWithFiveRounds()
    {
        var session = BuildSession();
        _sessionLookup.Setup(s => s.GetBySessionIdAsync(session.SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _blobImages.Setup(b => b.GetRoundImageUrlsAsync(session.UserId, session.SessionId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                "https://img/1.jpg", "https://img/2.jpg", "https://img/3.jpg", "https://img/4.jpg", "https://img/5.jpg"
            ]);

        SetupRoundRows(session.SessionId, includeMissingRound: false);

        var sut = CreateHandler();
        var result = await sut.Handle(new GetRecapQuery(session.SessionId), CancellationToken.None);

        result.Status.Should().Be(RecapStatus.Found);
        result.Recap.Should().NotBeNull();
        result.Recap!.Rounds.Should().HaveCount(5);
    }

    [Fact]
    public async Task ExpiredSession_ReturnsGone()
    {
        var session = BuildSession(expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1), isPersonalBest: false);
        _sessionLookup.Setup(s => s.GetBySessionIdAsync(session.SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var sut = CreateHandler();
        var result = await sut.Handle(new GetRecapQuery(session.SessionId), CancellationToken.None);

        result.Status.Should().Be(RecapStatus.Gone);
    }

    [Fact]
    public async Task IncompleteSession_ReturnsIncomplete()
    {
        var session = BuildSession(isCompleted: false);
        _sessionLookup.Setup(s => s.GetBySessionIdAsync(session.SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var sut = CreateHandler();
        var result = await sut.Handle(new GetRecapQuery(session.SessionId), CancellationToken.None);

        result.Status.Should().Be(RecapStatus.Incomplete);
        result.Recap.Should().BeNull();
    }

    [Fact]
    public async Task SessionNotFound_ReturnsNotFound()
    {
        _sessionLookup.Setup(s => s.GetBySessionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameSessionEntity?)null);

        var sut = CreateHandler();
        var result = await sut.Handle(new GetRecapQuery(Guid.NewGuid().ToString("N")), CancellationToken.None);

        result.Status.Should().Be(RecapStatus.NotFound);
    }

    [Fact]
    public async Task MissingBlob_UsesPlaceholderUrl()
    {
        var session = BuildSession();
        _sessionLookup.Setup(s => s.GetBySessionIdAsync(session.SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _blobImages.Setup(b => b.GetRoundImageUrlsAsync(session.UserId, session.SessionId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                "https://img/1.jpg",
                BlobImageRepository.PlaceholderImageUrl,
                "https://img/3.jpg",
                "https://img/4.jpg",
                "https://img/5.jpg"
            ]);

        SetupRoundRows(session.SessionId, includeMissingRound: false);

        var sut = CreateHandler();
        var result = await sut.Handle(new GetRecapQuery(session.SessionId), CancellationToken.None);

        result.Status.Should().Be(RecapStatus.Found);
        result.Recap!.Rounds[1].ImageUrl.Should().Be(BlobImageRepository.PlaceholderImageUrl);
    }

    private static GameSessionEntity BuildSession(
        DateTimeOffset? expiresAt = null,
        bool isPersonalBest = true,
        bool isCompleted = true)
    {
        var id = Guid.NewGuid().ToString("N");
        return new GameSessionEntity
        {
            PartitionKey   = "user-1",
            RowKey         = id,
            SessionId      = id,
            UserId         = "user-1",
            DisplayName    = "User",
            TotalScore     = 31,
            IsPersonalBest = isPersonalBest,
            IsCompleted    = isCompleted,
            CompletedAt    = DateTimeOffset.UtcNow,
            ExpiresAt      = expiresAt,
        };
    }

    private void SetupRoundRows(string sessionId, bool includeMissingRound)
    {
        var capturesByRound = new Dictionary<int, RoundCaptureEntity?>();
        for (var i = 1; i <= 5; i++)
        {
            capturesByRound[i] = includeMissingRound && i == 2
                ? null
                : new RoundCaptureEntity
                {
                    PartitionKey = sessionId,
                    RowKey = $"{sessionId}_{i}",
                    SessionId = sessionId,
                    RoundNumber = i,
                    TargetEmotion = "Happiness",
                    Score = 7,
                    HeadPoseValid = true,
                    CapturedAt = DateTimeOffset.UtcNow,
                    ImageBlobUrl = ""
                };
        }

        _tableStorage.Setup(x => x.GetEntityAsync<RoundCaptureEntity>(
                "RoundCaptures",
                sessionId,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string _, string rowKey, CancellationToken _) =>
            {
                var suffix = rowKey.Split('_').Last();
                if (!int.TryParse(suffix, out var roundNumber))
                {
                    return null;
                }

                return capturesByRound.TryGetValue(roundNumber, out var capture) ? capture : null;
            });
    }
}
