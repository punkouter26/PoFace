using PoFace.Api.Features.Leaderboard;

namespace PoFace.UnitTests.Leaderboard;

public sealed class GetLeaderboardQueryTests
{
    [Fact]
    public async Task EmptyTable_ReturnsEmptyList()
    {
        var repo = new Mock<ILeaderboardTableRepository>();
        repo.Setup(r => r.GetEntriesForYearAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<LeaderboardEntity>());

        var sut = new GetLeaderboardHandler(repo.Object);
        var result = await sut.Handle(new GetLeaderboardQuery(), CancellationToken.None);

        result.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task SingleEntry_Returned()
    {
        var entity = new LeaderboardEntity
        {
            RowKey = "user-1",
            DisplayName = "Alpha",
            TotalScore = 10,
            DeviceType = "Desktop",
            RecapUrl = "/recap/a",
            AchievedAt = DateTimeOffset.UtcNow,
        };

        var repo = new Mock<ILeaderboardTableRepository>();
        repo.Setup(r => r.GetEntriesForYearAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([entity]);

        var sut = new GetLeaderboardHandler(repo.Object);
        var result = await sut.Handle(new GetLeaderboardQuery(), CancellationToken.None);

        result.Entries.Should().HaveCount(1);
        result.Entries[0].DisplayName.Should().Be("Alpha");
    }

    [Fact]
    public async Task EqualScore_NewerRanksHigher()
    {
        var older = new LeaderboardEntity
        {
            RowKey = "user-old",
            DisplayName = "Old",
            TotalScore = 20,
            DeviceType = "Desktop",
            RecapUrl = "/recap/old",
            AchievedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
        };

        var newer = new LeaderboardEntity
        {
            RowKey = "user-new",
            DisplayName = "New",
            TotalScore = 20,
            DeviceType = "Desktop",
            RecapUrl = "/recap/new",
            AchievedAt = DateTimeOffset.UtcNow,
        };

        var repo = new Mock<ILeaderboardTableRepository>();
        repo.Setup(r => r.GetEntriesForYearAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([older, newer]);

        var sut = new GetLeaderboardHandler(repo.Object);
        var result = await sut.Handle(new GetLeaderboardQuery(), CancellationToken.None);

        result.Entries[0].DisplayName.Should().Be("New");
    }

    [Fact]
    public async Task DifferentScores_HigherRanksFirst()
    {
        var low = new LeaderboardEntity
        {
            RowKey = "user-low",
            DisplayName = "Low",
            TotalScore = 11,
            DeviceType = "Desktop",
            RecapUrl = "/recap/low",
            AchievedAt = DateTimeOffset.UtcNow,
        };

        var high = new LeaderboardEntity
        {
            RowKey = "user-high",
            DisplayName = "High",
            TotalScore = 49,
            DeviceType = "Desktop",
            RecapUrl = "/recap/high",
            AchievedAt = DateTimeOffset.UtcNow,
        };

        var repo = new Mock<ILeaderboardTableRepository>();
        repo.Setup(r => r.GetEntriesForYearAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([low, high]);

        var sut = new GetLeaderboardHandler(repo.Object);
        var result = await sut.Handle(new GetLeaderboardQuery(), CancellationToken.None);

        result.Entries[0].DisplayName.Should().Be("High");
    }

    [Fact]
    public async Task TopOne_ReturnsOneEntry()
    {
        var entries = Enumerable.Range(1, 5)
            .Select(i => new LeaderboardEntity
            {
                RowKey = $"user-{i}",
                DisplayName = $"User {i}",
                TotalScore = i,
                DeviceType = "Desktop",
                RecapUrl = $"/recap/{i}",
                AchievedAt = DateTimeOffset.UtcNow,
            })
            .ToList();

        var repo = new Mock<ILeaderboardTableRepository>();
        repo.Setup(r => r.GetEntriesForYearAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        var sut = new GetLeaderboardHandler(repo.Object);
        var result = await sut.Handle(new GetLeaderboardQuery(1), CancellationToken.None);

        result.Entries.Should().HaveCount(1);
    }

    [Fact]
    public async Task TopGreaterThan500_IsClamped()
    {
        var entries = Enumerable.Range(1, 700)
            .Select(i => new LeaderboardEntity
            {
                RowKey = $"user-{i}",
                DisplayName = $"User {i}",
                TotalScore = i,
                DeviceType = "Desktop",
                RecapUrl = $"/recap/{i}",
                AchievedAt = DateTimeOffset.UtcNow,
            })
            .ToList();

        var repo = new Mock<ILeaderboardTableRepository>();
        repo.Setup(r => r.GetEntriesForYearAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        var sut = new GetLeaderboardHandler(repo.Object);
        var result = await sut.Handle(new GetLeaderboardQuery(999), CancellationToken.None);

        result.Entries.Should().HaveCount(500);
    }
}
