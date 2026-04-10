using Microsoft.Extensions.Logging.Abstractions;
using NinetyNine.Model;
using NinetyNine.Repository.Repositories;

namespace NinetyNine.Repository.Tests;

[Collection(MongoCollection.Name)]
[Trait("Category", "Integration")]
public class GameRepositoryTests(MongoFixture fixture)
{
    private IGameRepository CreateRepo()
    {
        var ctx = fixture.CreateDbContext();
        return new GameRepository(ctx, NullLogger<GameRepository>.Instance);
    }

    private static Game MakeGame(Guid? playerId = null, GameState state = GameState.InProgress)
    {
        // InitializeFrames requires NotStarted state; we set the desired state afterwards.
        var game = new Game
        {
            GameId = Guid.NewGuid(),
            PlayerId = playerId ?? Guid.NewGuid(),
            VenueId = Guid.NewGuid(),
            TableSize = TableSize.SevenFoot,
            GameState = GameState.NotStarted,
            WhenPlayed = DateTime.UtcNow
        };
        game.InitializeFrames();          // transitions to InProgress
        game.GameState = state;           // override to desired state for test data
        if (state == GameState.Completed)
            game.CompletedAt = DateTime.UtcNow;
        return game;
    }

    [Fact]
    public async Task CreateAndGetById_PreservesEmbeddedFrames()
    {
        var repo = CreateRepo();
        var game = MakeGame();

        // Complete frame 1 manually
        var f1 = game.Frames[0];
        f1.BreakBonus = 1;
        f1.BallCount = 8;
        f1.RunningTotal = 9;
        f1.IsCompleted = true;
        f1.IsActive = false;
        f1.CompletedAt = DateTime.UtcNow;

        await repo.CreateAsync(game);

        var retrieved = await repo.GetByIdAsync(game.GameId);
        retrieved.Should().NotBeNull();
        retrieved!.GameId.Should().Be(game.GameId);
        retrieved.Frames.Should().HaveCount(9);
        retrieved.Frames[0].BreakBonus.Should().Be(1);
        retrieved.Frames[0].BallCount.Should().Be(8);
        retrieved.Frames[0].RunningTotal.Should().Be(9);
        retrieved.Frames[0].IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var repo = CreateRepo();
        var result = await repo.GetByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var repo = CreateRepo();
        var game = MakeGame();
        await repo.CreateAsync(game);

        game.GameState = GameState.Completed;
        game.CompletedAt = DateTime.UtcNow;
        game.Notes = "Updated notes";
        await repo.UpdateAsync(game);

        var retrieved = await repo.GetByIdAsync(game.GameId);
        retrieved!.GameState.Should().Be(GameState.Completed);
        retrieved.Notes.Should().Be("Updated notes");
        retrieved.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_Throws_WhenNotFound()
    {
        var repo = CreateRepo();
        var ghost = MakeGame();
        var act = async () => await repo.UpdateAsync(ghost);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_RemovesGame()
    {
        var repo = CreateRepo();
        var game = MakeGame();
        await repo.CreateAsync(game);

        await repo.DeleteAsync(game.GameId);
        var result = await repo.GetByIdAsync(game.GameId);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByPlayerAsync_ReturnsMostRecentFirst()
    {
        var repo = CreateRepo();
        var playerId = Guid.NewGuid();

        var older = MakeGame(playerId);
        older.WhenPlayed = DateTime.UtcNow.AddDays(-2);
        var newer = MakeGame(playerId);
        newer.WhenPlayed = DateTime.UtcNow.AddDays(-1);
        var newest = MakeGame(playerId);
        newest.WhenPlayed = DateTime.UtcNow;

        await repo.CreateAsync(older);
        await repo.CreateAsync(newer);
        await repo.CreateAsync(newest);

        var games = await repo.GetByPlayerAsync(playerId, skip: 0, limit: 10);
        games.Should().HaveCount(3);
        games[0].GameId.Should().Be(newest.GameId, "most recent should be first");
        games[2].GameId.Should().Be(older.GameId, "oldest should be last");
    }

    [Fact]
    public async Task GetByPlayerAsync_RespectsSkipAndLimit()
    {
        var repo = CreateRepo();
        var playerId = Guid.NewGuid();
        for (int i = 0; i < 5; i++)
        {
            var g = MakeGame(playerId);
            g.WhenPlayed = DateTime.UtcNow.AddDays(-i);
            await repo.CreateAsync(g);
        }

        var page2 = await repo.GetByPlayerAsync(playerId, skip: 2, limit: 2);
        page2.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsMostRecentFirst()
    {
        var repo = CreateRepo();
        var g1 = MakeGame(); g1.WhenPlayed = DateTime.UtcNow.AddDays(-3);
        var g2 = MakeGame(); g2.WhenPlayed = DateTime.UtcNow.AddDays(-1);
        var g3 = MakeGame(); g3.WhenPlayed = DateTime.UtcNow;
        await repo.CreateAsync(g1);
        await repo.CreateAsync(g2);
        await repo.CreateAsync(g3);

        var recent = await repo.GetRecentAsync(limit: 2);
        recent.Should().HaveCount(2);
        recent[0].GameId.Should().Be(g3.GameId, "most recent should come first");
    }

    [Fact]
    public async Task GetCompletedByPlayerAsync_FiltersCompletedOnly()
    {
        var repo = CreateRepo();
        var playerId = Guid.NewGuid();
        var completed = MakeGame(playerId, GameState.Completed);
        completed.CompletedAt = DateTime.UtcNow;
        var inProgress = MakeGame(playerId, GameState.InProgress);

        await repo.CreateAsync(completed);
        await repo.CreateAsync(inProgress);

        var results = await repo.GetCompletedByPlayerAsync(playerId);
        results.Should().HaveCount(1);
        results[0].GameId.Should().Be(completed.GameId);
    }

    [Fact]
    public async Task GetActiveForPlayerAsync_ReturnsInProgressGame()
    {
        var repo = CreateRepo();
        var playerId = Guid.NewGuid();
        var active = MakeGame(playerId, GameState.InProgress);
        var done = MakeGame(playerId, GameState.Completed);

        await repo.CreateAsync(active);
        await repo.CreateAsync(done);

        var result = await repo.GetActiveForPlayerAsync(playerId);
        result.Should().NotBeNull();
        result!.GameId.Should().Be(active.GameId);
    }

    [Fact]
    public async Task GetActiveForPlayerAsync_ReturnsNull_WhenNoActiveGame()
    {
        var repo = CreateRepo();
        var playerId = Guid.NewGuid();
        var result = await repo.GetActiveForPlayerAsync(playerId);
        result.Should().BeNull();
    }
}
