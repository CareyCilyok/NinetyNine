using Microsoft.Extensions.Logging.Abstractions;
using NinetyNine.Model;
using NinetyNine.Repository.Repositories;
using NinetyNine.Services;

namespace NinetyNine.Services.Tests;

/// <summary>
/// Integration tests for the v0.5.x Concurrent multi-player match flow:
/// <see cref="MatchService.CreateConcurrentMatchAsync"/>,
/// <see cref="MatchService.FinishInningAsync"/>, and the seat-rotation /
/// match-completion logic.
/// </summary>
[Collection(MongoCollection.Name)]
[Trait("Category", "Integration")]
public class MatchServiceConcurrentTests(MongoFixture fixture)
{
    private (IMatchService matchSvc, IGameService gameSvc, IGameRepository gameRepo) CreateServices()
    {
        var ctx = fixture.CreateDbContext();
        var matchRepo = new MatchRepository(ctx, NullLogger<MatchRepository>.Instance);
        var gameRepo = new GameRepository(ctx, NullLogger<GameRepository>.Instance);
        var gameSvc = new GameService(gameRepo, NullLogger<GameService>.Instance);
        var matchSvc = new MatchService(
            matchRepo, gameRepo, gameSvc, NullLogger<MatchService>.Instance);
        return (matchSvc, gameSvc, gameRepo);
    }

    private static IReadOnlyList<ConcurrentMatchPlayerSetup> Setups(params Guid[] ids) =>
        ids.Select(id => new ConcurrentMatchPlayerSetup(id, IsEfrenVariant: false)).ToList();

    [Fact]
    public async Task CreateConcurrentMatchAsync_TwoPlayers_StartsTwoGames()
    {
        var (svc, _, _) = CreateServices();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var venue = Guid.NewGuid();

        var result = await svc.CreateConcurrentMatchAsync(
            creatorPlayerId: p1,
            players: Setups(p1, p2),
            venueId: venue,
            tableSize: TableSize.NineFoot,
            breakMethod: BreakMethod.Lagged);

        result.Success.Should().BeTrue();
        result.Value!.Rotation.Should().Be(MatchRotation.Concurrent);
        result.Value.PlayerIds.Should().Equal(p1, p2);
        result.Value.GameIds.Should().HaveCount(2);
        result.Value.CurrentPlayerSeat.Should().Be(0,
            "seat 0 always shoots the first inning");
        result.Value.Status.Should().Be(MatchStatus.InProgress);
    }

    [Fact]
    public async Task CreateConcurrentMatchAsync_FourPlayers_StartsFourGames()
    {
        var (svc, gameSvc, _) = CreateServices();
        var ids = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();

        var result = await svc.CreateConcurrentMatchAsync(
            creatorPlayerId: ids[0],
            players: Setups(ids),
            venueId: Guid.NewGuid(),
            tableSize: TableSize.SevenFoot,
            breakMethod: BreakMethod.CoinFlip);

        result.Success.Should().BeTrue();
        result.Value!.GameIds.Should().HaveCount(4);

        // Each game must exist and be InProgress with frame 1 active.
        foreach (var gameId in result.Value.GameIds)
        {
            var g = await gameSvc.GetGameAsync(gameId);
            g.Should().NotBeNull();
            g!.GameState.Should().Be(GameState.InProgress);
            g.CurrentFrameNumber.Should().Be(1);
        }
    }

    [Fact]
    public async Task CreateConcurrentMatchAsync_PerPlayerEfrenFlags_AppliedIndependently()
    {
        var (svc, gameSvc, _) = CreateServices();
        var efrenPlayer = Guid.NewGuid();
        var standardPlayer = Guid.NewGuid();

        var result = await svc.CreateConcurrentMatchAsync(
            creatorPlayerId: efrenPlayer,
            players: new[]
            {
                new ConcurrentMatchPlayerSetup(efrenPlayer, IsEfrenVariant: true),
                new ConcurrentMatchPlayerSetup(standardPlayer, IsEfrenVariant: false),
            },
            venueId: Guid.NewGuid(),
            tableSize: TableSize.NineFoot,
            breakMethod: BreakMethod.Lagged);

        result.Success.Should().BeTrue();

        var games = await Task.WhenAll(result.Value!.GameIds
            .Select(id => gameSvc.GetGameAsync(id)));

        games.Single(g => g!.PlayerId == efrenPlayer)!.IsEfrenVariant
            .Should().BeTrue("Efren is per-Game in NinetyNine");
        games.Single(g => g!.PlayerId == standardPlayer)!.IsEfrenVariant
            .Should().BeFalse();
    }

    [Fact]
    public async Task CreateConcurrentMatchAsync_OnePlayer_Fails()
    {
        var (svc, _, _) = CreateServices();
        var p = Guid.NewGuid();

        var result = await svc.CreateConcurrentMatchAsync(
            creatorPlayerId: p,
            players: Setups(p),
            venueId: Guid.NewGuid(),
            tableSize: TableSize.SevenFoot,
            breakMethod: BreakMethod.Lagged);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("InvalidPlayerCount");
    }

    [Fact]
    public async Task CreateConcurrentMatchAsync_FivePlayers_Fails()
    {
        var (svc, _, _) = CreateServices();
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();

        var result = await svc.CreateConcurrentMatchAsync(
            creatorPlayerId: ids[0],
            players: Setups(ids),
            venueId: Guid.NewGuid(),
            tableSize: TableSize.SevenFoot,
            breakMethod: BreakMethod.Lagged);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("InvalidPlayerCount");
    }

    [Fact]
    public async Task CreateConcurrentMatchAsync_DuplicatePlayer_Fails()
    {
        var (svc, _, _) = CreateServices();
        var p = Guid.NewGuid();

        var result = await svc.CreateConcurrentMatchAsync(
            creatorPlayerId: p,
            players: Setups(p, p),
            venueId: Guid.NewGuid(),
            tableSize: TableSize.SevenFoot,
            breakMethod: BreakMethod.Lagged);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("DuplicatePlayer");
    }

    [Fact]
    public async Task CreateConcurrentMatchAsync_CreatorNotInPlayers_Fails()
    {
        var (svc, _, _) = CreateServices();
        var creator = Guid.NewGuid();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var result = await svc.CreateConcurrentMatchAsync(
            creatorPlayerId: creator,
            players: Setups(p1, p2),
            venueId: Guid.NewGuid(),
            tableSize: TableSize.SevenFoot,
            breakMethod: BreakMethod.Lagged);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("CreatorNotInMatch");
    }

    [Fact]
    public async Task FinishInningAsync_RotatesSeats_0_to_1_to_2_to_0()
    {
        var (matchSvc, _, _) = CreateServices();
        var ids = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToArray();

        var created = (await matchSvc.CreateConcurrentMatchAsync(
            creatorPlayerId: ids[0],
            players: Setups(ids),
            venueId: Guid.NewGuid(),
            tableSize: TableSize.SevenFoot,
            breakMethod: BreakMethod.Lagged)).Value!;

        // Seat 0 → 1
        var step1 = (await matchSvc.FinishInningAsync(created.MatchId)).Value!;
        step1.CurrentPlayerSeat.Should().Be(1);

        // Seat 1 → 2
        var step2 = (await matchSvc.FinishInningAsync(created.MatchId)).Value!;
        step2.CurrentPlayerSeat.Should().Be(2);

        // Seat 2 → 0 (wrap)
        var step3 = (await matchSvc.FinishInningAsync(created.MatchId)).Value!;
        step3.CurrentPlayerSeat.Should().Be(0,
            "the seat index wraps modulo the player count");
        step3.Status.Should().Be(MatchStatus.InProgress,
            "no game has finished yet — only the seat has rotated");
    }

    [Fact]
    public async Task FinishInningAsync_OnSequentialMatch_Fails()
    {
        var (matchSvc, _, _) = CreateServices();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var created = (await matchSvc.CreateMatchAsync(
            p1, p2, Guid.NewGuid(), TableSize.SevenFoot,
            MatchFormat.Single, target: 1, BreakMethod.Lagged)).Value!;

        var result = await matchSvc.FinishInningAsync(created.MatchId);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("WrongMatchRotation");
    }

    [Fact]
    public async Task FinishInningAsync_AllGamesComplete_FinalizesMatch()
    {
        var (matchSvc, _, gameRepo) = CreateServices();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        var created = (await matchSvc.CreateConcurrentMatchAsync(
            creatorPlayerId: p1,
            players: Setups(p1, p2),
            venueId: Guid.NewGuid(),
            tableSize: TableSize.SevenFoot,
            breakMethod: BreakMethod.Lagged)).Value!;

        // Manually mark both Games complete with distinct totals so the
        // winner-selection arbiter has a clear answer.
        foreach (var gameId in created.GameIds)
        {
            var g = await gameRepo.GetByIdAsync(gameId);
            g!.GameState = GameState.Completed;
            g.CompletedAt = DateTime.UtcNow;
            // Spread frames so PlayerId == p1 ends with TotalScore = 99 (winner).
            for (int i = 1; i <= 9; i++)
            {
                int score = g.PlayerId == p1 ? 11 : 5;
                g.Frames.Add(new Frame
                {
                    FrameId = Guid.NewGuid(),
                    GameId = gameId,
                    FrameNumber = i,
                    BreakBonus = 1,
                    BallCount = score - 1,
                    IsCompleted = true,
                });
            }
            await gameRepo.UpdateAsync(g);
        }

        var result = await matchSvc.FinishInningAsync(created.MatchId);

        result.Success.Should().BeTrue();
        result.Value!.Status.Should().Be(MatchStatus.Completed);
        result.Value.WinnerPlayerId.Should().Be(p1, "p1 ran out 99-45");
        result.Value.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task FinishInningAsync_SkipsCompletedSeats()
    {
        var (matchSvc, _, gameRepo) = CreateServices();
        var p1 = Guid.NewGuid(); // seat 0 — will be marked complete
        var p2 = Guid.NewGuid(); // seat 1 — still playing
        var p3 = Guid.NewGuid(); // seat 2 — still playing

        var created = (await matchSvc.CreateConcurrentMatchAsync(
            creatorPlayerId: p1,
            players: Setups(p1, p2, p3),
            venueId: Guid.NewGuid(),
            tableSize: TableSize.SevenFoot,
            breakMethod: BreakMethod.Lagged)).Value!;

        // Mark seat 0's game as Completed (player finished early).
        var seat0GameId = created.GameIds[0];
        var seat0Game = await gameRepo.GetByIdAsync(seat0GameId);
        seat0Game!.GameState = GameState.Completed;
        seat0Game.CompletedAt = DateTime.UtcNow;
        await gameRepo.UpdateAsync(seat0Game);

        // Currently seat 0; FinishInning should skip to seat 1.
        var step1 = (await matchSvc.FinishInningAsync(created.MatchId)).Value!;
        step1.CurrentPlayerSeat.Should().Be(1);

        // Now from seat 1 → seat 2 (skipping back over seat 0 which is done).
        var step2 = (await matchSvc.FinishInningAsync(created.MatchId)).Value!;
        step2.CurrentPlayerSeat.Should().Be(2,
            "seat 0 is complete, so seat 2 is the next non-completed seat after 1");

        // From seat 2 → wrap back to seat 1 (skipping seat 0).
        var step3 = (await matchSvc.FinishInningAsync(created.MatchId)).Value!;
        step3.CurrentPlayerSeat.Should().Be(1,
            "the rotation wraps past the completed seat 0 and lands on seat 1 again");
    }
}
