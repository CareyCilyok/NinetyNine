using FluentAssertions;
using Xunit;

namespace NinetyNine.Model.Tests;

public class FrameTests
{
    [Fact]
    public void Frame_WhenCreated_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var frame = new Frame();

        // Assert
        frame.FrameId.Should().NotBeEmpty();
        frame.FrameScore.Should().Be(0);
        frame.BallCount.Should().Be(0);
        frame.BreakBonus.Should().Be(0);
        frame.IsCompleted.Should().BeFalse();
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 0, 1)]
    [InlineData(0, 5, 5)]
    [InlineData(1, 5, 6)]
    [InlineData(1, 10, 11)]
    [InlineData(0, 10, 10)]
    public void Frame_FrameScore_ShouldCalculateCorrectly(int breakBonus, int ballCount, int expectedScore)
    {
        // Arrange
        var frame = new Frame();

        // Act
        frame.BreakBonus = breakBonus;
        frame.BallCount = ballCount;

        // Assert
        frame.FrameScore.Should().Be(expectedScore);
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(1, 0, true)]
    [InlineData(0, 10, true)]
    [InlineData(1, 10, true)]
    [InlineData(0, 11, false)]  // Ball count exceeds 10
    [InlineData(2, 5, false)]   // Break bonus exceeds 1
    [InlineData(-1, 5, false)]  // Negative break bonus
    [InlineData(0, -1, false)]  // Negative ball count
    public void Frame_ValidateFrame_ShouldReturnCorrectValue(int breakBonus, int ballCount, bool expectedValid)
    {
        // Arrange
        var frame = new Frame { FrameNumber = 1 };

        // Act
        frame.BreakBonus = breakBonus;
        frame.BallCount = ballCount;

        // Assert
        frame.ValidateFrame().Should().Be(expectedValid);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(10, true)]
    public void Frame_BallCount_ShouldAcceptValidValues(int ballCount, bool isValid)
    {
        // Arrange
        var frame = new Frame { FrameNumber = 1 };

        // Act
        frame.BallCount = ballCount;

        // Assert
        if (isValid)
        {
            frame.BallCount.Should().Be(ballCount);
            frame.ValidateFrame().Should().BeTrue();
        }
    }

    [Fact]
    public void Frame_BreakBonus_ShouldAcceptZeroOrOne()
    {
        // Arrange
        var frame = new Frame { FrameNumber = 1 };

        // Act & Assert for 0
        frame.BreakBonus = 0;
        frame.BreakBonus.Should().Be(0);
        frame.ValidateFrame().Should().BeTrue();

        // Act & Assert for 1
        frame.BreakBonus = 1;
        frame.BreakBonus.Should().Be(1);
        frame.ValidateFrame().Should().BeTrue();
    }

    [Fact]
    public void Frame_CompleteFrame_ShouldSetIsCompletedAndRunningTotal()
    {
        // Arrange
        var frame = new Frame { FrameNumber = 1 };
        frame.BreakBonus = 1;
        frame.BallCount = 8;

        // Act
        frame.CompleteFrame(previousRunningTotal: 0);

        // Assert
        frame.IsCompleted.Should().BeTrue();
        frame.RunningTotal.Should().Be(9);
        frame.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Frame_CompleteFrame_ShouldCalculateCorrectRunningTotal()
    {
        // Arrange
        var frame = new Frame { FrameNumber = 3 };
        frame.BreakBonus = 1;
        frame.BallCount = 7;

        // Act
        frame.CompleteFrame(previousRunningTotal: 20);

        // Assert
        frame.RunningTotal.Should().Be(28); // 20 + (1 + 7) = 28
    }

    [Fact]
    public void Frame_ResetFrame_ShouldClearAllValues()
    {
        // Arrange
        var frame = new Frame { FrameNumber = 1 };
        frame.BreakBonus = 1;
        frame.BallCount = 8;
        frame.CompleteFrame(0);

        // Act
        frame.ResetFrame();

        // Assert
        frame.BreakBonus.Should().Be(0);
        frame.BallCount.Should().Be(0);
        frame.IsCompleted.Should().BeFalse();
        frame.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void Frame_IsPerfectFrame_ShouldBeTrueWhenScoreIs11()
    {
        // Arrange
        var frame = new Frame { FrameNumber = 1 };

        // Act
        frame.BreakBonus = 1;
        frame.BallCount = 10;

        // Assert
        frame.IsPerfectFrame.Should().BeTrue();
        frame.FrameScore.Should().Be(11);
    }

    [Fact]
    public void Frame_IsValidScore_ShouldBeTrueWhenScoreIs11OrLess()
    {
        // Arrange
        var frame = new Frame { FrameNumber = 1 };

        // Act & Assert for valid score
        frame.BreakBonus = 1;
        frame.BallCount = 10;
        frame.IsValidScore.Should().BeTrue();
    }
}
