using Microsoft.Extensions.Logging.Abstractions;
using NinetyNine.Model;
using NinetyNine.Repository;
using NinetyNine.Repository.Repositories;
using NinetyNine.Services;

namespace NinetyNine.Services.Tests;

[Collection(MongoCollection.Name)]
[Trait("Category", "Integration")]
public class StatisticsServiceTests(MongoFixture fixture)
{
    private (IStatisticsService stats, IGameRepository gameRepo, IPlayerRepository playerRepo)
        CreateServices()
    {
        var ctx = fixture.CreateDbContext();
        var gameRepo = new GameRepository(ctx, NullLogger<GameRepository>.Instance);
        var playerRepo = new PlayerRepository(ctx, NullLogger<PlayerRepository>.Instance);
        var statsSvc = new StatisticsService(gameRepo, playerRepo, NullLogger<StatisticsService>.Instance);
        return (statsSvc, gameRepo, playerRepo);
    }

    /// <summary>
    /// Creates a completed game with all frames manually filled to the given total score.
    /// Distributes the score evenly across 9 frames.
    /// </summary>
    private static Game MakeCompletedGame(Guid playerId, int totalScore)
    {
        var game = new Game
        {
            GameId = Guid.NewGuid(),
            PlayerId = playerId,
            VenueId = Guid.NewGuid(),
            WhenPlayed = DateTime.UtcNow
        };
        game.InitializeFrames();  // sets GameState = InProgress
        game.GameState = GameState.Completed;
        game.CompletedAt = DateTime.UtcNow;

        int perFrame = totalScore / 9;
        int remainder = totalScore % 9;
        int running = 0;

        for (int i = 0; i < 9; i++)
        {
            int score = perFrame + (i < remainder ? 1 : 0);
            game.Frames[i].BreakBonus = score > 0 ? 0 : 0;
            game.Frames[i].BallCount = Math.Min(score, 10);
            game.Frames[i].IsCompleted = true;
            game.Frames[i].IsActive = false;
            running += score;
            game.Frames[i].RunningTotal = running;
            game.Frames[i].CompletedAt = DateTime.UtcNow;
        }

        return game;
    }

    private static Player MakePlayer(string name) =>
        new() { PlayerId = Guid.NewGuid(), DisplayName = name };

    [Fact]
    public async Task GetPlayerStatsAsync_AggregatesCorrectly()
    {
        var (stats, gameRepo, _) = CreateServices();
        var playerId = Guid.NewGuid();

        var g1 = MakeCompletedGame(playerId, totalScore: 45);
        var g2 = MakeCompletedGame(playerId, totalScore: 63);
        var g3 = new Game
        {
            GameId = Guid.NewGuid(),
            PlayerId = playerId,
            VenueId = Guid.NewGuid(),
            WhenPlayed = DateTime.UtcNow
        };
        g3.InitializeFrames();  // transitions to InProgress

        await gameRepo.CreateAsync(g1);
        await gameRepo.CreateAsync(g2);
        await gameRepo.CreateAsync(g3);

        var result = await stats.GetPlayerStatsAsync(playerId);

        result.GamesPlayed.Should().Be(3, "3 games total including the in-progress one");
        result.GamesCompleted.Should().Be(2, "2 completed games");
        result.BestScore.Should().Be(63, "highest completed game score");
        result.AverageScore.Should().BeApproximately(54.0, 0.1, "(45 + 63) / 2 = 54");
        result.LastPlayed.Should().NotBeNull();
    }

    [Fact]
    public async Task GetPlayerStatsAsync_ReturnsZeros_WhenNoGames()
    {
        var (stats, _, _) = CreateServices();
        var result = await stats.GetPlayerStatsAsync(Guid.NewGuid());

        result.GamesPlayed.Should().Be(0);
        result.GamesCompleted.Should().Be(0);
        result.AverageScore.Should().Be(0);
        result.BestScore.Should().Be(0);
        result.LastPlayed.Should().BeNull();
    }

    [Fact]
    public async Task GetLeaderboardAsync_OrdersByAverageScoreDescending()
    {
        var (stats, gameRepo, playerRepo) = CreateServices();

        var p1 = MakePlayer($"LBoard_A_{Guid.NewGuid():N}");
        var p2 = MakePlayer($"LBoard_B_{Guid.NewGuid():N}");
        await playerRepo.CreateAsync(p1);
        await playerRepo.CreateAsync(p2);

        // p1 averages 45, p2 averages 63
        await gameRepo.CreateAsync(MakeCompletedGame(p1.PlayerId, 45));
        await gameRepo.CreateAsync(MakeCompletedGame(p2.PlayerId, 63));

        var leaderboard = await stats.GetLeaderboardAsync(limit: 10);

        // Both players should appear; p2 with higher average should be first
        var p2Entry = leaderboard.FirstOrDefault(e => e.PlayerId == p2.PlayerId);
        var p1Entry = leaderboard.FirstOrDefault(e => e.PlayerId == p1.PlayerId);

        p2Entry.Should().NotBeNull();
        p1Entry.Should().NotBeNull();

        var p2Index = leaderboard.ToList().IndexOf(p2Entry!);
        var p1Index = leaderboard.ToList().IndexOf(p1Entry!);
        p2Index.Should().BeLessThan(p1Index, "higher average score should rank higher");
    }

    [Fact]
    public async Task GetBestGamesAsync_ReturnsHighestScoresFirst()
    {
        var (stats, gameRepo, _) = CreateServices();
        var playerId = Guid.NewGuid();

        var low = MakeCompletedGame(playerId, 36);
        var high = MakeCompletedGame(playerId, 72);
        var mid = MakeCompletedGame(playerId, 54);

        await gameRepo.CreateAsync(low);
        await gameRepo.CreateAsync(high);
        await gameRepo.CreateAsync(mid);

        var best = await stats.GetBestGamesAsync(playerId, limit: 3);
        best.Should().HaveCount(3);
        best[0].TotalScore.Should().Be(72, "highest score should come first");
        best[1].TotalScore.Should().Be(54);
        best[2].TotalScore.Should().Be(36);
    }

    [Fact]
    public async Task GetBestGamesAsync_RespectsLimit()
    {
        var (stats, gameRepo, _) = CreateServices();
        var playerId = Guid.NewGuid();
        for (int i = 0; i < 5; i++)
            await gameRepo.CreateAsync(MakeCompletedGame(playerId, 10 + i * 5));

        var best = await stats.GetBestGamesAsync(playerId, limit: 2);
        best.Should().HaveCount(2);
    }
}
