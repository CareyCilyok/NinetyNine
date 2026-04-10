using Microsoft.Extensions.Logging.Abstractions;
using NinetyNine.Model;
using NinetyNine.Repository;
using NinetyNine.Repository.Repositories;
using NinetyNine.Services;

namespace NinetyNine.Services.Tests;

/// <summary>
/// Integration tests for <see cref="GameService"/> against a real MongoDB instance.
/// </summary>
[Collection(MongoCollection.Name)]
[Trait("Category", "Integration")]
public class GameServiceTests(MongoFixture fixture)
{
    private (IGameService service, IGameRepository repo) CreateService()
    {
        var ctx = fixture.CreateDbContext();
        var repo = new GameRepository(ctx, NullLogger<GameRepository>.Instance);
        var svc = new GameService(repo, NullLogger<GameService>.Instance);
        return (svc, repo);
    }

    [Fact]
    public async Task StartNewGameAsync_CreatesGameWithNineFrames()
    {
        var (svc, _) = CreateService();
        var playerId = Guid.NewGuid();
        var venueId = Guid.NewGuid();

        var game = await svc.StartNewGameAsync(playerId, venueId, TableSize.SevenFoot);

        game.Should().NotBeNull();
        game.PlayerId.Should().Be(playerId);
        game.VenueId.Should().Be(venueId);
        game.TableSize.Should().Be(TableSize.SevenFoot);
        game.GameState.Should().Be(GameState.InProgress);
        game.Frames.Should().HaveCount(9);
    }

    [Fact]
    public async Task StartNewGameAsync_PersistsGameToDatabase()
    {
        var (svc, repo) = CreateService();
        var game = await svc.StartNewGameAsync(Guid.NewGuid(), Guid.NewGuid(), TableSize.NineFoot);

        var persisted = await repo.GetByIdAsync(game.GameId);
        persisted.Should().NotBeNull();
        persisted!.Frames.Should().HaveCount(9);
    }

    [Fact]
    public async Task GetGameAsync_ReturnsGame_WhenExists()
    {
        var (svc, _) = CreateService();
        var created = await svc.StartNewGameAsync(Guid.NewGuid(), Guid.NewGuid(), TableSize.Unknown);

        var fetched = await svc.GetGameAsync(created.GameId);
        fetched.Should().NotBeNull();
        fetched!.GameId.Should().Be(created.GameId);
    }

    [Fact]
    public async Task GetGameAsync_ReturnsNull_WhenNotFound()
    {
        var (svc, _) = CreateService();
        var result = await svc.GetGameAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task RecordFrameAsync_Throws_WhenScoresInvalid()
    {
        var (svc, _) = CreateService();
        var game = await svc.StartNewGameAsync(Guid.NewGuid(), Guid.NewGuid(), TableSize.Unknown);

        // Invalid: BreakBonus = -1
        var act = async () => await svc.RecordFrameAsync(game.GameId, frameNumber: 1,
            breakBonus: -1, ballCount: 5, notes: null);

        await act.Should().ThrowAsync<InvalidOperationException>(
            "frame validation should reject negative break bonus");
    }

    [Fact]
    public async Task RecordFrameAsync_Throws_WhenFrameNotActive()
    {
        var (svc, _) = CreateService();
        var game = await svc.StartNewGameAsync(Guid.NewGuid(), Guid.NewGuid(), TableSize.Unknown);

        // Frame 2 is not active (frame 1 is)
        var act = async () => await svc.RecordFrameAsync(game.GameId, frameNumber: 2,
            breakBonus: 0, ballCount: 5, notes: null);

        await act.Should().ThrowAsync<InvalidOperationException>(
            "only the active frame can be recorded");
    }

    [Fact]
    public async Task RecordFrameAsync_Throws_WhenGameNotFound()
    {
        var (svc, _) = CreateService();
        var act = async () => await svc.RecordFrameAsync(Guid.NewGuid(), frameNumber: 1,
            breakBonus: 0, ballCount: 5, notes: null);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task CompleteGameAsync_MarksGameCompleted()
    {
        // Create a game and manually set all 9 frames as completed in the DB
        var ctx = fixture.CreateDbContext();
        var repo = new GameRepository(ctx, NullLogger<GameRepository>.Instance);
        var svc = new GameService(repo, NullLogger<GameService>.Instance);

        var game = await svc.StartNewGameAsync(Guid.NewGuid(), Guid.NewGuid(), TableSize.Unknown);

        // Manually complete all frames (circumvents the AdvanceToNextFrame bug)
        int running = 0;
        for (int i = 0; i < 9; i++)
        {
            game.Frames[i].BreakBonus = 0;
            game.Frames[i].BallCount = 5;
            game.Frames[i].IsActive = false;
            game.Frames[i].IsCompleted = true;
            running += 5;
            game.Frames[i].RunningTotal = running;
            game.Frames[i].CompletedAt = DateTime.UtcNow;
        }
        await repo.UpdateAsync(game);

        var completed = await svc.CompleteGameAsync(game.GameId);
        completed.GameState.Should().Be(GameState.Completed);
        completed.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CompleteGameAsync_Throws_WhenFramesNotDone()
    {
        var (svc, _) = CreateService();
        var game = await svc.StartNewGameAsync(Guid.NewGuid(), Guid.NewGuid(), TableSize.Unknown);

        var act = async () => await svc.CompleteGameAsync(game.GameId);
        await act.Should().ThrowAsync<InvalidOperationException>(
            "cannot complete game with fewer than 9 completed frames");
    }

    [Fact]
    public async Task ResetFrameAsync_ClearsActiveFrameScores()
    {
        var (svc, _) = CreateService();
        var game = await svc.StartNewGameAsync(Guid.NewGuid(), Guid.NewGuid(), TableSize.Unknown);

        // The active frame (frame 1) can be reset without advancing
        var reset = await svc.ResetFrameAsync(game.GameId, frameNumber: 1);
        reset.Frames[0].BreakBonus.Should().Be(0);
        reset.Frames[0].BallCount.Should().Be(0);
        reset.Frames[0].IsActive.Should().BeTrue(
            "resetting the active frame re-activates it");
    }

    [Fact]
    public async Task ResetFrameAsync_Throws_WhenGameNotFound()
    {
        var (svc, _) = CreateService();
        var act = async () => await svc.ResetFrameAsync(Guid.NewGuid(), frameNumber: 1);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
