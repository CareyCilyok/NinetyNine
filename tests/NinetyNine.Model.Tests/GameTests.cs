using NinetyNine.Model;

namespace NinetyNine.Model.Tests;

public class GameTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Creates a game with 9 initialized frames, state = InProgress.</summary>
    private static Game CreateInitializedGame()
    {
        var game = new Game { PlayerId = Guid.NewGuid(), VenueId = Guid.NewGuid() };
        game.InitializeFrames();
        return game;
    }

    /// <summary>Completes the current active frame. CompleteCurrentFrame auto-advances.</summary>
    private static void CompleteFrameAndAdvance(Game game, int breakBonus, int ballCount,
        string? notes = null)
        => game.CompleteCurrentFrame(breakBonus, ballCount, notes);

    /// <summary>Completes all 9 frames. The final frame auto-finalizes the game.</summary>
    private static void CompleteAllFrames(Game game, int breakBonus = 1, int ballCount = 10)
    {
        for (int i = 1; i <= 9; i++)
            game.CompleteCurrentFrame(breakBonus, ballCount);
    }

    // ── InitializeFrames ──────────────────────────────────────────────────────

    [Fact]
    public void InitializeFrames_Creates_NineFrames()
    {
        var game = new Game();
        game.InitializeFrames();
        game.Frames.Should().HaveCount(9);
    }

    [Fact]
    public void InitializeFrames_FirstFrame_IsActive()
    {
        var game = new Game();
        game.InitializeFrames();
        game.Frames[0].IsActive.Should().BeTrue();
    }

    [Fact]
    public void InitializeFrames_AllFramesHaveCorrectNumbers()
    {
        var game = new Game();
        game.InitializeFrames();
        for (int i = 0; i < 9; i++)
            game.Frames[i].FrameNumber.Should().Be(i + 1);
    }

    [Fact]
    public void InitializeFrames_SetsGameStateToInProgress()
    {
        var game = new Game();
        game.InitializeFrames();
        game.GameState.Should().Be(GameState.InProgress);
    }

    [Fact]
    public void InitializeFrames_Throws_WhenAlreadyInProgress()
    {
        var game = new Game();
        game.InitializeFrames();
        var act = () => game.InitializeFrames();
        act.Should().Throw<InvalidOperationException>();
    }

    // ── CompleteCurrentFrame ──────────────────────────────────────────────────

    [Fact]
    public void CompleteCurrentFrame_AdvancesToNextFrame()
    {
        var game = CreateInitializedGame();
        CompleteFrameAndAdvance(game, breakBonus: 1, ballCount: 5);
        game.CurrentFrameNumber.Should().Be(2);
        game.Frames[1].IsActive.Should().BeTrue();
    }

    [Fact]
    public void CompleteCurrentFrame_UpdatesTotalScore()
    {
        var game = CreateInitializedGame();
        game.CompleteCurrentFrame(breakBonus: 1, ballCount: 5);
        game.TotalScore.Should().Be(6);
    }

    [Fact]
    public void CompleteCurrentFrame_UpdatesRunningTotal()
    {
        var game = CreateInitializedGame();
        CompleteFrameAndAdvance(game, breakBonus: 1, ballCount: 5); // frame 1: score=6
        game.CompleteCurrentFrame(breakBonus: 0, ballCount: 3);     // frame 2: score=3
        game.RunningTotal.Should().Be(9, "frame1=6 + frame2=3 = 9");
    }

    [Fact]
    public void CompleteCurrentFrame_OnFrame9_AutoFinalizesGame()
    {
        var game = CreateInitializedGame();
        for (int i = 1; i <= 9; i++)
            game.CompleteCurrentFrame(breakBonus: 0, ballCount: 5);

        game.IsCompleted.Should().BeTrue("frame 9 completion should finalize the game");
        game.GameState.Should().Be(GameState.Completed);
        game.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void CompleteCurrentFrame_OnFrame9_NoActiveFrameRemains()
    {
        var game = CreateInitializedGame();
        for (int i = 1; i <= 9; i++)
            game.CompleteCurrentFrame(0, 5);

        game.Frames.Any(f => f.IsActive).Should().BeFalse();
        game.CurrentFrame.Should().BeNull();
    }

    [Fact]
    public void CompleteCurrentFrame_Throws_WhenNoActiveFrame()
    {
        var game = CreateInitializedGame();
        foreach (var f in game.Frames) f.IsActive = false;
        var act = () => game.CompleteCurrentFrame(0, 5);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CompleteCurrentFrame_Throws_WhenScoresInvalid()
    {
        var game = CreateInitializedGame();
        var act = () => game.CompleteCurrentFrame(breakBonus: -1, ballCount: 5);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CompleteCurrentFrame_Throws_WhenGameNotInProgress()
    {
        var game = new Game { GameState = GameState.Completed };
        game.Frames.Add(new Frame { FrameNumber = 1, IsActive = true });
        var act = () => game.CompleteCurrentFrame(0, 5);
        act.Should().Throw<InvalidOperationException>();
    }

    // ── TotalScore ────────────────────────────────────────────────────────────

    [Fact]
    public void TotalScore_SumsCompletedFrames()
    {
        var game = CreateInitializedGame();
        CompleteFrameAndAdvance(game, 1, 5); // 6
        game.CompleteCurrentFrame(0, 4);     // 4
        game.TotalScore.Should().Be(10);
    }

    [Fact]
    public void TotalScore_IsZeroWhenNoFramesCompleted()
    {
        var game = CreateInitializedGame();
        game.TotalScore.Should().Be(0);
    }

    // ── IsPerfectGame ─────────────────────────────────────────────────────────

    [Fact]
    public void IsPerfectGame_True_WhenCompletedWithScore99()
    {
        var game = CreateInitializedGame();
        CompleteAllFrames(game, breakBonus: 1, ballCount: 10);
        game.IsPerfectGame.Should().BeTrue();
    }

    [Fact]
    public void IsPerfectGame_False_WhenIncomplete()
    {
        var game = CreateInitializedGame();
        game.IsPerfectGame.Should().BeFalse();
    }

    [Fact]
    public void IsPerfectGame_False_WhenCompletedButScoreLessThan99()
    {
        var game = CreateInitializedGame();
        CompleteAllFrames(game, breakBonus: 0, ballCount: 5); // 9 × 5 = 45
        game.IsPerfectGame.Should().BeFalse();
    }

    // ── PerfectFrames ─────────────────────────────────────────────────────────

    [Fact]
    public void PerfectFrames_CountsFramesWithElevenPoints()
    {
        var game = CreateInitializedGame();
        CompleteFrameAndAdvance(game, 1, 10); // perfect: 11
        game.CompleteCurrentFrame(0, 5);      // not perfect: 5
        game.PerfectFrames.Should().Be(1);
    }

    [Fact]
    public void PerfectFrames_IsZeroWhenNoPerfectFrames()
    {
        var game = CreateInitializedGame();
        game.CompleteCurrentFrame(0, 5);
        game.PerfectFrames.Should().Be(0);
    }

    // ── BestFrame ─────────────────────────────────────────────────────────────

    [Fact]
    public void BestFrame_ReturnsHighestScoringCompletedFrame()
    {
        var game = CreateInitializedGame();
        CompleteFrameAndAdvance(game, 0, 3);  // 3
        CompleteFrameAndAdvance(game, 1, 10); // 11
        game.CompleteCurrentFrame(0, 7);      // 7

        game.BestFrame.Should().NotBeNull();
        game.BestFrame!.FrameScore.Should().Be(11);
    }

    [Fact]
    public void BestFrame_IsNullWhenNoCompletedFrames()
    {
        var game = CreateInitializedGame();
        game.BestFrame.Should().BeNull();
    }

    // ── IsEfrenVariant ────────────────────────────────────────────────────────

    [Fact]
    public void IsEfrenVariant_DefaultsToFalse()
    {
        var game = new Game();
        game.IsEfrenVariant.Should().BeFalse(
            "standard rules apply unless the player explicitly opts into Efren mode");
    }

    [Fact]
    public void IsEfrenVariant_DoesNotAffectScoring()
    {
        // The flag is data-only — it drives visual indication and stats
        // filters, not score math. Two games with identical frames must
        // produce identical TotalScore regardless of the flag.
        var standard = CreateInitializedGame();
        var efren = CreateInitializedGame();
        efren.IsEfrenVariant = true;

        for (int i = 1; i <= 9; i++)
        {
            standard.CompleteCurrentFrame(1, 5);
            efren.CompleteCurrentFrame(1, 5);
        }

        standard.TotalScore.Should().Be(efren.TotalScore,
            "Efren mode is data-only — score math is unchanged");
    }

    // ── AverageScore ──────────────────────────────────────────────────────────

    [Fact]
    public void AverageScore_ZeroWhenNoCompletedFrames()
    {
        var game = CreateInitializedGame();
        game.AverageScore.Should().Be(0);
    }

    [Fact]
    public void AverageScore_CalculatedCorrectly()
    {
        var game = CreateInitializedGame();
        CompleteFrameAndAdvance(game, 1, 5); // 6
        game.CompleteCurrentFrame(0, 4);     // 4
        // avg = 10 / 2 = 5
        game.AverageScore.Should().BeApproximately(5.0, 0.001);
    }

    // ── ValidateGame ──────────────────────────────────────────────────────────

    [Fact]
    public void ValidateGame_ReturnsTrue_ForValidGame()
    {
        var game = CreateInitializedGame();
        game.ValidateGame().Should().BeTrue();
    }

    [Fact]
    public void ValidateGame_ReturnsFalse_WhenFrameCountNotNine()
    {
        var game = new Game();
        game.Frames.Add(new Frame { FrameNumber = 1 });
        game.ValidateGame().Should().BeFalse();
    }

    [Fact]
    public void ValidateGame_ReturnsFalse_WhenFrameNumbersWrong()
    {
        // Test the two-active-frames violation (multi-active invariant)
        var game = CreateInitializedGame();
        game.Frames[1].IsActive = true; // second frame also active
        game.ValidateGame().Should().BeFalse();
    }

    [Fact]
    public void ValidateGame_ReturnsFalse_WhenRunningTotalDecreases()
    {
        var game = CreateInitializedGame();
        CompleteFrameAndAdvance(game, 1, 5); // frame 1: running total = 6
        game.CompleteCurrentFrame(0, 3);     // frame 2: running total should be 9

        // Corrupt running total on frame 2 to be less than frame 1's total
        game.Frames[1].RunningTotal = 3;

        game.ValidateGame().Should().BeFalse();
    }

    // ── CurrentFrame ─────────────────────────────────────────────────────────

    [Fact]
    public void CurrentFrame_ReturnsActiveFrame()
    {
        var game = CreateInitializedGame();
        game.CurrentFrame.Should().NotBeNull();
        game.CurrentFrame!.FrameNumber.Should().Be(1);
    }

    [Fact]
    public void CurrentFrame_IsNullWhenNoActiveFrame()
    {
        var game = new Game();
        game.CurrentFrame.Should().BeNull();
    }

    // ── CompletedFrames ───────────────────────────────────────────────────────

    [Fact]
    public void CompletedFrames_CountsOnlyCompletedFrames()
    {
        var game = CreateInitializedGame();
        game.CompleteCurrentFrame(0, 5);
        game.CompletedFrames.Should().Be(1);
    }
}
