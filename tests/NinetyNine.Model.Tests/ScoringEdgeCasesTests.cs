using NinetyNine.Model;

namespace NinetyNine.Model.Tests;

/// <summary>
/// Documents and verifies scoring edge cases under the v2 ruleset (effective v0.3.0):
/// each ball pocketed counts 1 point, BallCount caps at 9, max frame is 10
/// (1 break bonus + 9 balls), max game is 90.
/// </summary>
public class ScoringEdgeCasesTests
{
    /// <summary>
    /// Rule: each ball legally pocketed scores 1 point. The 9-ball does NOT
    /// double under the v2 product rule. Maximum BallCount per frame is 9.
    /// </summary>
    [Fact]
    public void BallCount_Caps_AtNine_UnderV2Rule()
    {
        var frame = new Frame
        {
            FrameNumber = 1,
            BreakBonus = 0,
            BallCount = 9
        };

        frame.ValidateFrame().Should().BeTrue("BallCount of 9 is the v2 max (1 point per ball)");
        frame.FrameScore.Should().Be(9);
    }

    /// <summary>
    /// A perfect break under v2: BreakBonus = 1 (legal break, at least one ball
    /// pocketed) plus BallCount = 9 (all 9 balls). Maximum frame score = 10.
    /// </summary>
    [Fact]
    public void PerfectBreak_BreakBonusPlusAllBalls_MaxesFrameAtTen()
    {
        var frame = new Frame
        {
            FrameNumber = 1,
            BreakBonus = 1,
            BallCount = 9
        };

        frame.ValidateFrame().Should().BeTrue();
        frame.FrameScore.Should().Be(10, "v2 maximum possible frame score is 10");
        frame.IsPerfectFrame.Should().BeTrue();
        frame.IsValidScore.Should().BeTrue();
    }

    /// <summary>
    /// A perfect game of 90 points requires 9 consecutive perfect frames
    /// (each 10 points). Verifies the full game flow including running totals.
    /// </summary>
    [Fact]
    public void PerfectGame_90Points_AchievableViaNinePerfectFrames()
    {
        var game = new Game
        {
            PlayerId = Guid.NewGuid(),
            VenueId = Guid.NewGuid()
        };
        game.InitializeFrames();

        for (int frameNum = 1; frameNum <= 9; frameNum++)
        {
            var activeFrame = game.Frames.First(f => f.IsActive);
            game.CompleteCurrentFrame(breakBonus: 1, ballCount: 9);

            if (frameNum < 9)
            {
                // Re-activate to allow AdvanceToNextFrame to find the completed frame.
                activeFrame.IsActive = true;
                game.AdvanceToNextFrame();
            }
        }

        game.GameState = GameState.Completed;
        game.CompletedAt = DateTime.UtcNow;

        game.TotalScore.Should().Be(90, "9 perfect frames × 10 points each = 90");
        game.IsPerfectGame.Should().BeTrue();
        game.PerfectFrames.Should().Be(9);
        game.CompletedFrames.Should().Be(9);
        game.Frames.Last().RunningTotal.Should().Be(90);
    }

    /// <summary>
    /// A frame with BallCount &gt; 9 is rejected under v2 even on a scratch break.
    /// Catches any path that still emits a 10-ball frame from legacy code.
    /// </summary>
    [Fact]
    public void BallCount_OfTen_IsInvalid_UnderV2Rule()
    {
        var frame = new Frame
        {
            FrameNumber = 1,
            BreakBonus = 0,
            BallCount = 10
        };

        frame.ValidateFrame().Should().BeFalse("v2 caps BallCount at 9");
    }
}
