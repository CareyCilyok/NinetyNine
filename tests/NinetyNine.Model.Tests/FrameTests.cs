using NinetyNine.Model;

namespace NinetyNine.Model.Tests;

public class FrameTests
{
    // ── ValidateFrame ─────────────────────────────────────────────────────────

    [Fact]
    public void ValidateFrame_ReturnsTrue_WhenScoresValid()
    {
        var frame = new Frame { FrameNumber = 1, BreakBonus = 1, BallCount = 5 };
        frame.ValidateFrame().Should().BeTrue();
    }

    [Fact]
    public void ValidateFrame_ReturnsFalse_WhenBreakBonusNegative()
    {
        var frame = new Frame { FrameNumber = 1, BreakBonus = -1, BallCount = 5 };
        frame.ValidateFrame().Should().BeFalse();
    }

    [Fact]
    public void ValidateFrame_ReturnsFalse_WhenBreakBonusGreaterThanOne()
    {
        var frame = new Frame { FrameNumber = 1, BreakBonus = 2, BallCount = 5 };
        frame.ValidateFrame().Should().BeFalse();
    }

    [Fact]
    public void ValidateFrame_ReturnsFalse_WhenBallCountNegative()
    {
        var frame = new Frame { FrameNumber = 1, BreakBonus = 0, BallCount = -1 };
        frame.ValidateFrame().Should().BeFalse();
    }

    [Fact]
    public void ValidateFrame_ReturnsFalse_WhenBallCountGreaterThanNine()
    {
        // v2 rule: max BallCount per frame is 9 (each ball scores 1 point).
        var frame = new Frame { FrameNumber = 1, BreakBonus = 0, BallCount = 10 };
        frame.ValidateFrame().Should().BeFalse();
    }

    [Fact]
    public void ValidateFrame_ReturnsTrue_AtV2MaxFrameScore()
    {
        // Max frame score under the v2 rule is 10 (1 break + 9 balls).
        var frame = new Frame { FrameNumber = 1, BreakBonus = 1, BallCount = 9 };
        frame.ValidateFrame().Should().BeTrue("break bonus 1 + ball count 9 = 10 is the v2 max");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void ValidateFrame_ReturnsFalse_WhenFrameNumberOutOfRange(int invalidFrameNumber)
    {
        var frame = new Frame { FrameNumber = invalidFrameNumber, BreakBonus = 0, BallCount = 5 };
        frame.ValidateFrame().Should().BeFalse();
    }

    // ── FrameScore ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 0, 1)]
    [InlineData(0, 5, 5)]
    [InlineData(1, 5, 6)]
    [InlineData(1, 9, 10)]
    [InlineData(0, 9, 9)]
    public void FrameScore_EqualsBreakBonusPlusBallCount(int breakBonus, int ballCount, int expectedScore)
    {
        var frame = new Frame { BreakBonus = breakBonus, BallCount = ballCount };
        frame.FrameScore.Should().Be(expectedScore);
    }

    // ── IsPerfectFrame ────────────────────────────────────────────────────────

    [Fact]
    public void IsPerfectFrame_IsTrue_WhenScoreEqualsTen()
    {
        // v2 perfect frame: 1 break bonus + 9 balls = 10.
        var frame = new Frame { BreakBonus = 1, BallCount = 9 };
        frame.IsPerfectFrame.Should().BeTrue();
    }

    [Fact]
    public void IsPerfectFrame_IsFalse_WhenScoreLessThanTen()
    {
        var frame = new Frame { BreakBonus = 0, BallCount = 9 };
        frame.IsPerfectFrame.Should().BeFalse();
    }

    // ── IsValidScore ──────────────────────────────────────────────────────────

    [Fact]
    public void IsValidScore_IsFalse_WhenScoreExceedsTen()
    {
        // Construct a frame with invalid score by bypassing ValidateFrame.
        var frame = new Frame
        {
            BreakBonus = 2,   // invalid per rules but property is settable
            BallCount = 9
        };
        // FrameScore = 11 under v2 max-10 rule → IsValidScore = false.
        frame.IsValidScore.Should().BeFalse();
    }

    [Fact]
    public void IsValidScore_IsTrue_WhenScoreExactlyTen()
    {
        var frame = new Frame { BreakBonus = 1, BallCount = 9 };
        frame.IsValidScore.Should().BeTrue();
    }

    // ── CompleteFrame ─────────────────────────────────────────────────────────

    [Fact]
    public void CompleteFrame_SetsRunningTotal_FromPrevious()
    {
        var frame = new Frame { FrameNumber = 3, BreakBonus = 1, BallCount = 5 };
        frame.CompleteFrame(previousRunningTotal: 20);
        frame.RunningTotal.Should().Be(26, "20 previous + 6 frame score = 26");
    }

    [Fact]
    public void CompleteFrame_Throws_WhenInvalidScore()
    {
        var frame = new Frame { FrameNumber = 1, BreakBonus = -1, BallCount = 5 };
        var act = () => frame.CompleteFrame();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CompleteFrame_SetsIsCompletedTrue()
    {
        var frame = new Frame { FrameNumber = 1, BreakBonus = 0, BallCount = 7 };
        frame.CompleteFrame();
        frame.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void CompleteFrame_SetsIsActiveFalse()
    {
        var frame = new Frame { FrameNumber = 1, BreakBonus = 0, BallCount = 7, IsActive = true };
        frame.CompleteFrame();
        frame.IsActive.Should().BeFalse();
    }

    [Fact]
    public void CompleteFrame_SetsCompletedAt()
    {
        var before = DateTime.UtcNow;
        var frame = new Frame { FrameNumber = 1, BreakBonus = 0, BallCount = 5 };
        frame.CompleteFrame();
        frame.CompletedAt.Should().NotBeNull();
        frame.CompletedAt!.Value.Should().BeOnOrAfter(before);
    }

    // ── ResetFrame ────────────────────────────────────────────────────────────

    [Fact]
    public void ResetFrame_ClearsAllScoreFields()
    {
        var frame = new Frame
        {
            FrameNumber = 2,
            BreakBonus = 1,
            BallCount = 8,
            RunningTotal = 25,
            IsCompleted = true,
            IsActive = false,
            CompletedAt = DateTime.UtcNow,
            Notes = "nice break"
        };

        frame.ResetFrame();

        frame.BreakBonus.Should().Be(0);
        frame.BallCount.Should().Be(0);
        frame.RunningTotal.Should().Be(0);
        frame.IsCompleted.Should().BeFalse();
        frame.IsActive.Should().BeFalse();
        frame.CompletedAt.Should().BeNull();
        frame.Notes.Should().BeNull();
    }
}
