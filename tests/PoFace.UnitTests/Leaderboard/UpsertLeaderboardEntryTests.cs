using PoFace.Api.Features.Leaderboard;

namespace PoFace.UnitTests.Leaderboard;

/// <summary>
/// Unit tests for <see cref="UpsertLeaderboardEntryHandler"/> covering
/// the four branches of the BestMatchUpsertStrategy.
/// </summary>
public sealed class UpsertLeaderboardEntryTests
{
    private readonly Mock<ILeaderboardTableRepository> _repoMock = new();
    private readonly UpsertLeaderboardEntryHandler     _handler;

    public UpsertLeaderboardEntryTests()
        => _handler = new UpsertLeaderboardEntryHandler(_repoMock.Object);

    private static UpsertLeaderboardEntryCommand Command(int score) =>
        new(UserId:      "user-1",
            DisplayName: "Test User",
            TotalScore:  score,
            SessionId:   Guid.NewGuid().ToString("N"),
            RecapUrl:    "/recap/session-abc",
            DeviceType:  "Desktop",
            AchievedAt:  DateTimeOffset.UtcNow);

    private void SetupExisting(int? score)
    {
        LeaderboardEntity? entity = score is null
            ? null
            : new LeaderboardEntity { PartitionKey = "2026", RowKey = "user-1", TotalScore = score.Value };

        _repoMock
            .Setup(r => r.GetEntryAsync(It.IsAny<string>(), "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
    }

    // ── Case 1: no existing entry ─────────────────────────────────────────────

    [Fact]
    public async Task NoExistingEntry_Inserts_AndReturnsPersonalBest()
    {
        SetupExisting(null);

        var result = await _handler.Handle(Command(30), CancellationToken.None);

        result.IsPersonalBest.Should().BeTrue();
        _repoMock.Verify(r => r.UpsertEntryAsync(
            It.IsAny<LeaderboardEntity>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Case 2: new score > existing ─────────────────────────────────────────

    [Fact]
    public async Task NewScoreGreater_Replaces_AndReturnsPersonalBest()
    {
        SetupExisting(20);

        var result = await _handler.Handle(Command(30), CancellationToken.None);

        result.IsPersonalBest.Should().BeTrue();
        _repoMock.Verify(r => r.UpsertEntryAsync(
            It.IsAny<LeaderboardEntity>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Case 3: new score == existing ────────────────────────────────────────

    [Fact]
    public async Task NewScoreEqual_IsNoOp_AndNotPersonalBest()
    {
        SetupExisting(30);

        var result = await _handler.Handle(Command(30), CancellationToken.None);

        result.IsPersonalBest.Should().BeFalse();
        _repoMock.Verify(r => r.UpsertEntryAsync(
            It.IsAny<LeaderboardEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Case 4: new score < existing ─────────────────────────────────────────

    [Fact]
    public async Task NewScoreLower_IsNoOp_AndNotPersonalBest()
    {
        SetupExisting(30);

        var result = await _handler.Handle(Command(20), CancellationToken.None);

        result.IsPersonalBest.Should().BeFalse();
        _repoMock.Verify(r => r.UpsertEntryAsync(
            It.IsAny<LeaderboardEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
