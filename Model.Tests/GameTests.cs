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
        game.GameId.Should().NotBeEmpty();
        game.Frames.Should().NotBeNull();
        game.Frames.Should().BeEmpty();
        game.TotalScore.Should().Be(0);
        game.IsCompleted.Should().BeFalse();
        game.GameState.Should().Be(GameState.NotStarted);
    }

    [Fact]
    public void Game_TotalScore_ShouldSumAllCompletedFrameScores()
    {
        // Arrange
        var game = new Game();
        game.InitializeFrames();

        // Act - Complete first 3 frames with known scores
        game.Frames[0].BreakBonus = 1;
        game.Frames[0].BallCount = 9;
        game.Frames[0].CompleteFrame(0);

        game.Frames[1].BreakBonus = 0;
        game.Frames[1].BallCount = 8;
        game.Frames[1].CompleteFrame(10);

        game.Frames[2].BreakBonus = 1;
        game.Frames[2].BallCount = 10;
        game.Frames[2].CompleteFrame(18);

        // Assert
        game.TotalScore.Should().Be(29); // (1+9) + (0+8) + (1+10) = 10 + 8 + 11 = 29
    }

    [Fact]
    public void Game_IsCompleted_ShouldBeTrueWhenGameStateIsCompleted()
    {
        // Arrange
        var game = new Game();
        game.InitializeFrames();

        // Act - Complete all 9 frames
        int runningTotal = 0;
        for (int i = 0; i < 9; i++)
        {
            game.Frames[i].BreakBonus = 0;
            game.Frames[i].BallCount = 5;
            game.Frames[i].CompleteFrame(runningTotal);
            runningTotal += 5;
        }
        game.GameState = GameState.Completed;

        // Assert
        game.IsCompleted.Should().BeTrue();
        game.Frames.Count.Should().Be(9);
    }

    [Theory]
    [InlineData(GameState.NotStarted, false)]
    [InlineData(GameState.InProgress, false)]
    [InlineData(GameState.Paused, false)]
    [InlineData(GameState.Completed, true)]
    public void Game_IsCompleted_ShouldReturnCorrectValueBasedOnState(GameState state, bool expectedIsCompleted)
    {
        // Arrange
        var game = new Game();

        // Act
        game.GameState = state;

        // Assert
        game.IsCompleted.Should().Be(expectedIsCompleted);
    }

    [Fact]
    public void Game_InitializeFrames_ShouldCreate9Frames()
    {
        // Arrange
        var game = new Game();

        // Act
        game.InitializeFrames();

        // Assert
        game.Frames.Should().HaveCount(9);
        game.Frames[0].FrameNumber.Should().Be(1);
        game.Frames[8].FrameNumber.Should().Be(9);
        game.GameState.Should().Be(GameState.InProgress);
    }

    [Fact]
    public void Game_CompleteCurrentFrame_ShouldAdvanceToNextFrame()
    {
        // Arrange
        var game = new Game();
        game.InitializeFrames();

        // Act
        game.CompleteCurrentFrame(1, 8);

        // Assert
        game.CurrentFrameNumber.Should().Be(2);
        game.Frames[0].IsCompleted.Should().BeTrue();
        game.Frames[0].FrameScore.Should().Be(9);
    }

    [Fact]
    public void Game_IsPerfectGame_ShouldBeTrueWhen99Points()
    {
        // Arrange
        var game = new Game();
        game.InitializeFrames();

        // Act - Complete all frames with perfect scores (11 each = 99 total)
        int runningTotal = 0;
        for (int i = 0; i < 9; i++)
        {
            game.Frames[i].BreakBonus = 1;
            game.Frames[i].BallCount = 10;
            game.Frames[i].CompleteFrame(runningTotal);
            runningTotal += 11;
        }
        game.GameState = GameState.Completed;

        // Assert
        game.TotalScore.Should().Be(99);
        game.IsPerfectGame.Should().BeTrue();
    }

    [Fact]
    public void Game_ValidateGame_ShouldReturnTrueForValidGame()
    {
        // Arrange
        var game = new Game();
        game.InitializeFrames();

        // Assert
        game.ValidateGame().Should().BeTrue();
    }
}
