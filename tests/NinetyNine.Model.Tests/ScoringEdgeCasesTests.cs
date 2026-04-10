using NinetyNine.Model;

namespace NinetyNine.Model.Tests;

/// <summary>
/// Documents and verifies scoring edge cases from the official P&amp;B Ninety-Nine rules.
/// </summary>
public class ScoringEdgeCasesTests
{
    /// <summary>
    /// Rule: The 9-ball counts as 2 points toward BallCount.
    /// A scratch break means BreakBonus = 0 (no legal break credit), but balls may still
    /// have been pocketed. BallCount = 10 represents pocketing all 9 balls where the
    /// 9-ball counts double (2 pts) + 8 others = 10 total ball-count points.
    /// </summary>
    [Fact]
    public void NineBallOnBreak_CountsAsTwo_WhenCombinedWithOtherBallsOnScratchBreak()
    {
        var frame = new Frame
        {
            FrameNumber = 1,
            BreakBonus = 0,  // scratch break — no break bonus
            BallCount = 10   // 9-ball (2 pts) + 8 other balls (1 pt each) = 10
        };

        frame.ValidateFrame().Should().BeTrue("BallCount of 10 on a scratch break is valid");
        frame.FrameScore.Should().Be(10);
    }

    /// <summary>
    /// A perfect break: BreakBonus = 1 (legal break, at least one ball pocketed)
    /// plus BallCount = 10 (all balls including 9-ball at double value).
    /// Maximum frame score = 11.
    /// </summary>
    [Fact]
    public void PerfectBreak_AllBallsPlus9Ball_MaxesFrameAt11()
    {
        var frame = new Frame
        {
            FrameNumber = 1,
            BreakBonus = 1,  // legal break with at least one ball pocketed
            BallCount = 10   // all balls including 9-ball (counts as 2)
        };

        frame.ValidateFrame().Should().BeTrue();
        frame.FrameScore.Should().Be(11, "maximum possible frame score is 11");
        frame.IsPerfectFrame.Should().BeTrue();
        frame.IsValidScore.Should().BeTrue();
    }

    /// <summary>
    /// A perfect game of 99 points requires 9 consecutive perfect frames (each 11 points).
    /// Tests the full game flow including running totals.
    /// </summary>
    [Fact]
    public void PerfectGame_99Points_AchievableViaNinePerfectFrames()
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
            game.CompleteCurrentFrame(breakBonus: 1, ballCount: 10);

            if (frameNum < 9)
            {
                // Re-activate to allow AdvanceToNextFrame to find the completed frame
                activeFrame.IsActive = true;
                game.AdvanceToNextFrame();
            }
        }

        game.GameState = GameState.Completed;
        game.CompletedAt = DateTime.UtcNow;

        game.TotalScore.Should().Be(99, "9 perfect frames × 11 points each = 99");
        game.IsPerfectGame.Should().BeTrue();
        game.PerfectFrames.Should().Be(9);
        game.CompletedFrames.Should().Be(9);
        game.Frames.Last().RunningTotal.Should().Be(99);
    }
}
