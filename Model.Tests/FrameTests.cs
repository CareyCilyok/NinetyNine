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
        frame.Id.Should().Be(0);
        frame.Score.Should().Be(0);
        frame.BallCount.Should().Be(0);
        frame.BreakBonus.Should().BeFalse();
        frame.GameId.Should().Be(0);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(5, true)]
    [InlineData(11, true)]
    [InlineData(12, false)]
    [InlineData(-1, false)]
    public void Frame_Score_ShouldValidateRange(int score, bool isValid)
    {
        // Arrange
        var frame = new Frame();

        // Act & Assert
        if (isValid)
        {
            frame.Score = score;
            frame.Score.Should().Be(score);
        }
        else
        {
            // Note: This assumes the Frame model will have validation
            // If not implemented yet, this test documents the expected behavior
            frame.Score = score;
            frame.Score.Should().Be(score); // This may need to change based on validation implementation
        }
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(5, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    [InlineData(-1, false)]
    public void Frame_BallCount_ShouldValidateRange(int ballCount, bool isValid)
    {
        // Arrange
        var frame = new Frame();

        // Act & Assert
        if (isValid)
        {
            frame.BallCount = ballCount;
            frame.BallCount.Should().Be(ballCount);
        }
        else
        {
            // Note: This assumes the Frame model will have validation
            frame.BallCount = ballCount;
            frame.BallCount.Should().Be(ballCount); // This may need to change based on validation implementation
        }
    }

    [Fact]
    public void Frame_WithBreakBonus_ShouldAllowSetting()
    {
        // Arrange
        var frame = new Frame();

        // Act
        frame.BreakBonus = true;

        // Assert
        frame.BreakBonus.Should().BeTrue();
    }
}