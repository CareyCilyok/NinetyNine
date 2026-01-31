/// Copyright (c) 2020-2025
///
/// Permission is hereby granted, free of charge, to any person obtaining a copy
/// of this software and associated documentation files (the "Software"), to deal
/// in the Software without restriction, including without limitation the rights
/// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
/// copies of the Software, and to permit persons to whom the Software is
/// furnished to do so, subject to the following conditions:
///
/// The above copyright notice and this permission notice shall be included in all
/// copies or substantial portions of the Software.
///
/// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
/// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
/// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
/// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
/// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
/// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
/// SOFTWARE.

using FluentAssertions;
using Moq;
using NinetyNine.Model;
using NinetyNine.Presentation.Services;
using NinetyNine.Presentation.ViewModels;
using Xunit;

namespace NinetyNine.Presentation.Tests
{
    public class GamePageViewModelTests
    {
        private readonly Mock<IGameService> _mockGameService;
        private readonly Mock<ICelebrationService> _mockCelebrationService;

        public GamePageViewModelTests()
        {
            _mockGameService = new Mock<IGameService>();
            _mockCelebrationService = new Mock<ICelebrationService>();
        }

        [Fact]
        public async Task NewGame_Initializes9Frames()
        {
            // Arrange
            var game = CreateGameWith9Frames();
            _mockGameService.Setup(x => x.CreateNewGameAsync(
                It.IsAny<Player>(),
                It.IsAny<Venue>(),
                It.IsAny<TableSize>()))
                .ReturnsAsync(game)
                .Callback(() =>
                {
                    _mockGameService.Raise(x => x.CurrentGameChanged += null, _mockGameService.Object, game);
                });

            var viewModel = new GamePageViewModel(_mockGameService.Object, _mockCelebrationService.Object);

            // Act - Give time for initialization
            await Task.Delay(100);

            // Assert
            viewModel.FrameViewModels.Should().HaveCount(9);
        }

        [Fact]
        public void IsPerfectGame_WhenScoreIs99_ReturnsTrue()
        {
            // Arrange
            var game = CreatePerfectGame();
            var viewModel = CreateViewModelWithGame(game);

            // Act & Assert
            viewModel.IsPerfectGame.Should().BeTrue();
        }

        [Fact]
        public void IsPerfectGame_WhenScoreIsNot99_ReturnsFalse()
        {
            // Arrange
            var game = CreateGameWith9Frames();
            game.GameState = GameState.Completed;
            // Total score will be less than 99
            var viewModel = CreateViewModelWithGame(game);

            // Act & Assert
            viewModel.IsPerfectGame.Should().BeFalse();
        }

        [Fact]
        public void GameStatus_WhenPerfectGame_ShowsLegendaryMessage()
        {
            // Arrange
            var game = CreatePerfectGame();
            var viewModel = CreateViewModelWithGame(game);

            // Act
            var status = viewModel.GameStatus;

            // Assert
            status.Should().Contain("LEGENDARY");
            status.Should().Contain("PERFECT 99");
        }

        [Fact]
        public void GameStatus_WhenInProgress_ShowsFrameAndScore()
        {
            // Arrange
            var game = CreateGameWith9Frames();
            game.GameState = GameState.InProgress;
            game.Frames[0].IsCompleted = true;
            game.Frames[0].BreakBonus = 1;
            game.Frames[0].BallCount = 8;
            game.Frames[1].IsActive = true;
            var viewModel = CreateViewModelWithGame(game);

            // Act
            var status = viewModel.GameStatus;

            // Assert
            status.Should().Contain("Frame");
            status.Should().Contain("of 9");
            status.Should().Contain("Score:");
        }

        [Fact]
        public void FrameViewModels_OrderedByFrameNumber()
        {
            // Arrange
            var game = CreateGameWith9Frames();
            var viewModel = CreateViewModelWithGame(game);

            // Act & Assert
            for (int i = 0; i < 9; i++)
            {
                viewModel.FrameViewModels[i].FrameNumber.Should().Be(i + 1);
            }
        }

        [Fact]
        public void CanCompleteFrame_WhenGameInProgressAndFrameActive_ReturnsTrue()
        {
            // Arrange
            var game = CreateGameWith9Frames();
            game.GameState = GameState.InProgress;
            game.Frames[0].IsActive = true;
            game.Frames[0].BreakBonus = 1;
            game.Frames[0].BallCount = 5;
            var viewModel = CreateViewModelWithGame(game);

            // Act & Assert
            viewModel.CanCompleteFrame.Should().BeTrue();
        }

        [Fact]
        public void CanCompleteGame_WhenGameInProgress_ReturnsTrue()
        {
            // Arrange
            var game = CreateGameWith9Frames();
            game.GameState = GameState.InProgress;
            var viewModel = CreateViewModelWithGame(game);

            // Act & Assert
            viewModel.CanCompleteGame.Should().BeTrue();
        }

        [Fact]
        public void CanCompleteGame_WhenGameNotInProgress_ReturnsFalse()
        {
            // Arrange
            var game = CreateGameWith9Frames();
            game.GameState = GameState.NotStarted;
            var viewModel = CreateViewModelWithGame(game);

            // Act & Assert
            viewModel.CanCompleteGame.Should().BeFalse();
        }

        [Fact]
        public void TotalScore_CalculatesFromAllFrames()
        {
            // Arrange
            var game = CreateGameWith9Frames();
            game.Frames[0].BreakBonus = 1;
            game.Frames[0].BallCount = 10;
            game.Frames[0].IsCompleted = true;
            game.Frames[0].RunningTotal = 11;
            var viewModel = CreateViewModelWithGame(game);

            // Act
            var totalScore = viewModel.TotalScore;

            // Assert
            totalScore.Should().Be(11);
        }

        [Fact]
        public void PlayerName_DefaultValue_IsPlayer1()
        {
            // Arrange
            var viewModel = new GamePageViewModel(_mockGameService.Object, _mockCelebrationService.Object);

            // Assert
            viewModel.PlayerName.Should().Be("Player 1");
        }

        [Fact]
        public void VenueName_DefaultValue_IsHomeTable()
        {
            // Arrange
            var viewModel = new GamePageViewModel(_mockGameService.Object, _mockCelebrationService.Object);

            // Assert
            viewModel.VenueName.Should().Be("Home Table");
        }

        [Fact]
        public void SelectedTableSize_DefaultValue_IsNineFoot()
        {
            // Arrange
            var viewModel = new GamePageViewModel(_mockGameService.Object, _mockCelebrationService.Object);

            // Assert
            viewModel.SelectedTableSize.Should().Be(TableSize.NineFoot);
        }

        private GamePageViewModel CreateViewModelWithGame(Game game)
        {
            _mockGameService.Raise(x => x.CurrentGameChanged += null, _mockGameService.Object, game);
            var viewModel = new GamePageViewModel(_mockGameService.Object, _mockCelebrationService.Object);
            // Simulate the game being set
            _mockGameService.Raise(x => x.CurrentGameChanged += null, _mockGameService.Object, game);
            return viewModel;
        }

        private Game CreateGameWith9Frames()
        {
            var game = new Game
            {
                GameId = Guid.NewGuid(),
                GameState = GameState.NotStarted,
                TableSize = TableSize.NineFoot,
                Frames = new List<Frame>()
            };

            for (int i = 1; i <= 9; i++)
            {
                game.Frames.Add(new Frame { FrameNumber = i });
            }

            return game;
        }

        private Game CreatePerfectGame()
        {
            var game = new Game
            {
                GameId = Guid.NewGuid(),
                GameState = GameState.Completed,
                TableSize = TableSize.NineFoot,
                Frames = new List<Frame>()
            };

            int runningTotal = 0;
            for (int i = 1; i <= 9; i++)
            {
                runningTotal += 11;
                game.Frames.Add(new Frame
                {
                    FrameNumber = i,
                    BreakBonus = 1,
                    BallCount = 10,
                    IsCompleted = true,
                    RunningTotal = runningTotal
                });
            }

            return game;
        }
    }
}
