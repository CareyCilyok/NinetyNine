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

using Avalonia.Media;
using FluentAssertions;
using Moq;
using NinetyNine.Model;
using NinetyNine.Presentation.Services;
using NinetyNine.Presentation.ViewModels;
using Xunit;

namespace NinetyNine.Presentation.Tests
{
    public class FrameControlViewModelTests
    {
        private readonly Mock<ICelebrationService> _mockCelebrationService;

        public FrameControlViewModelTests()
        {
            _mockCelebrationService = new Mock<ICelebrationService>();
        }

        [Fact]
        public void IsPerfectFrame_WhenScoreIs11_ReturnsTrue()
        {
            // Arrange
            var frame = new Frame { FrameNumber = 1, BreakBonus = 1, BallCount = 10, IsCompleted = true };
            var viewModel = new FrameControlViewModel(frame, _mockCelebrationService.Object);

            // Assert
            viewModel.IsPerfectFrame.Should().BeTrue();
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 0)]
        [InlineData(0, 10)]
        [InlineData(1, 9)]
        public void IsPerfectFrame_WhenScoreIsNot11_ReturnsFalse(int breakBonus, int ballCount)
        {
            // Arrange
            var frame = new Frame { FrameNumber = 1, BreakBonus = breakBonus, BallCount = ballCount };
            var viewModel = new FrameControlViewModel(frame, _mockCelebrationService.Object);

            // Assert
            viewModel.IsPerfectFrame.Should().BeFalse();
        }

        [Fact]
        public void FrameScore_CalculatesCorrectly()
        {
            // Arrange
            var frame = new Frame { FrameNumber = 1, BreakBonus = 1, BallCount = 7 };
            var viewModel = new FrameControlViewModel(frame, _mockCelebrationService.Object);

            // Assert
            viewModel.FrameScore.Should().Be(8);
        }

        [Fact]
        public void BreakBonus_SetValue_UpdatesFrameScore()
        {
            // Arrange
            var frame = new Frame { FrameNumber = 1, BreakBonus = 0, BallCount = 5, IsActive = true };
            var viewModel = new FrameControlViewModel(frame, _mockCelebrationService.Object);

            // Act
            viewModel.BreakBonus = 1;

            // Assert
            viewModel.FrameScore.Should().Be(6);
        }

        [Fact]
        public void BallCount_SetValue_UpdatesFrameScore()
        {
            // Arrange
            var frame = new Frame { FrameNumber = 1, BreakBonus = 1, BallCount = 0, IsActive = true };
            var viewModel = new FrameControlViewModel(frame, _mockCelebrationService.Object);

            // Act
            viewModel.BallCount = 8;

            // Assert
            viewModel.FrameScore.Should().Be(9);
        }

        [Fact]
        public void BreakBonus_ClampsToMaximum1()
        {
            // Arrange
            var frame = new Frame { FrameNumber = 1, BreakBonus = 0, IsActive = true };
            var viewModel = new FrameControlViewModel(frame, _mockCelebrationService.Object);

            // Act
            viewModel.BreakBonus = 5;

            // Assert
            viewModel.BreakBonus.Should().Be(1);
        }

        [Fact]
        public void BallCount_ClampsToMaximum10()
        {
            // Arrange
            var frame = new Frame { FrameNumber = 1, BallCount = 0, IsActive = true };
            var viewModel = new FrameControlViewModel(frame, _mockCelebrationService.Object);

            // Act
            viewModel.BallCount = 15;

            // Assert
            viewModel.BallCount.Should().Be(10);
        }

        [Fact]
        public void BreakBonus_ClampsToMinimum0()
        {
            // Arrange
            var frame = new Frame { FrameNumber = 1, BreakBonus = 1, IsActive = true };
            var viewModel = new FrameControlViewModel(frame, _mockCelebrationService.Object);

            // Act
            viewModel.BreakBonus = -5;

            // Assert
            viewModel.BreakBonus.Should().Be(0);
        }

        [Fact]
        public void CanEdit_WhenActiveAndNotCompleted_ReturnsTrue()
        {
            // Arrange
            var frame = new Frame { FrameNumber = 1, IsActive = true, IsCompleted = false };
            var viewModel = new FrameControlViewModel(frame, _mockCelebrationService.Object);

            // Assert
            viewModel.CanEdit.Should().BeTrue();
        }

        [Fact]
        public void CanEdit_WhenCompleted_ReturnsFalse()
        {
            // Arrange
            var frame = new Frame { FrameNumber = 1, IsActive = true, IsCompleted = true };
            var viewModel = new FrameControlViewModel(frame, _mockCelebrationService.Object);

            // Assert
            viewModel.CanEdit.Should().BeFalse();
        }

        [Fact]
        public void CanReset_WhenHasScore_ReturnsTrue()
        {
            // Arrange
            var frame = new Frame { FrameNumber = 1, BreakBonus = 1, BallCount = 5, IsCompleted = false };
            var viewModel = new FrameControlViewModel(frame, _mockCelebrationService.Object);

            // Assert
            viewModel.CanReset.Should().BeTrue();
        }

        [Fact]
        public void CanReset_WhenNoScore_ReturnsFalse()
        {
            // Arrange
            var frame = new Frame { FrameNumber = 1, BreakBonus = 0, BallCount = 0, IsCompleted = false };
            var viewModel = new FrameControlViewModel(frame, _mockCelebrationService.Object);

            // Assert
            viewModel.CanReset.Should().BeFalse();
        }

        [Fact]
        public void BorderBrush_WhenPerfect_ReturnsGoldColor()
        {
            // Arrange
            var frame = new Frame { FrameNumber = 1, BreakBonus = 1, BallCount = 10, IsCompleted = true };
            var viewModel = new FrameControlViewModel(frame, _mockCelebrationService.Object);

            // Act
            var brush = viewModel.BorderBrush as SolidColorBrush;

            // Assert
            brush.Should().NotBeNull();
            // Check RGB values instead of string (Gold = #FFD700)
            brush!.Color.R.Should().Be(0xFF);
            brush.Color.G.Should().Be(0xD7);
            brush.Color.B.Should().Be(0x00);
        }

        [Fact]
        public void BorderBrush_WhenActive_ReturnsNeonBlueColor()
        {
            // Arrange
            var frame = new Frame { FrameNumber = 1, IsActive = true, IsCompleted = false };
            var viewModel = new FrameControlViewModel(frame, _mockCelebrationService.Object);

            // Act
            var brush = viewModel.BorderBrush as SolidColorBrush;

            // Assert
            brush.Should().NotBeNull();
            brush!.Color.ToString().ToUpperInvariant().Should().Be("#FF00D4FF"); // Neon Blue
        }

        [Fact]
        public void ToolTipText_WhenCompleted_ContainsAllDetails()
        {
            // Arrange
            var frame = new Frame
            {
                FrameNumber = 3,
                BreakBonus = 1,
                BallCount = 8,
                IsCompleted = true,
                RunningTotal = 25
            };
            var viewModel = new FrameControlViewModel(frame, _mockCelebrationService.Object);

            // Act
            var tooltip = viewModel.ToolTipText;

            // Assert
            tooltip.Should().Contain("Frame 3");
            tooltip.Should().Contain("Break Bonus: 1");
            tooltip.Should().Contain("Ball Count: 8");
            tooltip.Should().Contain("Frame Score: 9");
            tooltip.Should().Contain("Running Total: 25");
        }

        [Fact]
        public void ToolTipText_WhenPerfect_ContainsPerfectIndicator()
        {
            // Arrange
            var frame = new Frame
            {
                FrameNumber = 1,
                BreakBonus = 1,
                BallCount = 10,
                IsCompleted = true
            };
            var viewModel = new FrameControlViewModel(frame, _mockCelebrationService.Object);

            // Act
            var tooltip = viewModel.ToolTipText;

            // Assert
            tooltip.Should().Contain("PERFECT");
        }
    }
}
