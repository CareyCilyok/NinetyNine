using FluentAssertions;
using Xunit;

namespace NinetyNine.Model.Tests;

public class GameTests
{
    [Fact]
    public void Game_WhenCreated_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var game = new Game();

        // Assert
        game.Id.Should().Be(0);
        game.Frames.Should().NotBeNull();
        game.Frames.Should().BeEmpty();
        game.TotalScore.Should().Be(0);
        game.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void Game_TotalScore_ShouldSumAllFrameScores()
    {
        // Arrange
        var game = new Game();
        var frame1 = new Frame { Score = 10 };
        var frame2 = new Frame { Score = 8 };
        var frame3 = new Frame { Score = 11 };

        // Act
        game.Frames.Add(frame1);
        game.Frames.Add(frame2);
        game.Frames.Add(frame3);

        // Assert
        game.TotalScore.Should().Be(29);
    }

    [Fact]
    public void Game_IsCompleted_ShouldBeTrueWhenNineFramesAdded()
    {
        // Arrange
        var game = new Game();

        // Act
        for (int i = 0; i < 9; i++)
        {
            game.Frames.Add(new Frame { Score = 5 });
        }

        // Assert
        game.IsCompleted.Should().BeTrue();
        game.Frames.Count.Should().Be(9);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(5, false)]
    [InlineData(8, false)]
    [InlineData(9, true)]
    public void Game_IsCompleted_ShouldReturnCorrectValue(int frameCount, bool expectedIsCompleted)
    {
        // Arrange
        var game = new Game();

        // Act
        for (int i = 0; i < frameCount; i++)
        {
            game.Frames.Add(new Frame { Score = 5 });
        }

        // Assert
        game.IsCompleted.Should().Be(expectedIsCompleted);
    }
}