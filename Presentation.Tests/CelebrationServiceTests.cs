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
using NinetyNine.Presentation.Services;
using Xunit;

namespace NinetyNine.Presentation.Tests
{
    public class CelebrationServiceTests
    {
        [Fact]
        public void TriggerPerfectFrame_WithValidFrameNumber_RaisesCelebrationEvent()
        {
            // Arrange
            var service = CelebrationService.CreateNew();
            CelebrationEventArgs? receivedArgs = null;
            service.CelebrationTriggered += (sender, args) => receivedArgs = args;

            // Act
            service.TriggerPerfectFrame(5);

            // Assert
            receivedArgs.Should().NotBeNull();
            receivedArgs!.Type.Should().Be(CelebrationType.PerfectFrame);
            receivedArgs.FrameNumber.Should().Be(5);
            receivedArgs.Score.Should().Be(11);
            receivedArgs.Message.Should().Contain("PERFECT FRAME 5");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(10)]
        [InlineData(100)]
        public void TriggerPerfectFrame_WithInvalidFrameNumber_ThrowsArgumentOutOfRangeException(int invalidFrame)
        {
            // Arrange
            var service = CelebrationService.CreateNew();

            // Act & Assert
            var act = () => service.TriggerPerfectFrame(invalidFrame);
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void TriggerPerfectGame_With99Points_RaisesLegendaryMessage()
        {
            // Arrange
            var service = CelebrationService.CreateNew();
            CelebrationEventArgs? receivedArgs = null;
            service.CelebrationTriggered += (sender, args) => receivedArgs = args;

            // Act
            service.TriggerPerfectGame(99);

            // Assert
            receivedArgs.Should().NotBeNull();
            receivedArgs!.Type.Should().Be(CelebrationType.PerfectGame);
            receivedArgs.Score.Should().Be(99);
            receivedArgs.Message.Should().Contain("LEGENDARY");
            receivedArgs.Message.Should().Contain("PERFECT 99");
            receivedArgs.DurationMs.Should().Be(5000); // Longer duration for perfect game
        }

        [Fact]
        public void TriggerPerfectGame_WithNonPerfectScore_RaisesIncredibleMessage()
        {
            // Arrange
            var service = CelebrationService.CreateNew();
            CelebrationEventArgs? receivedArgs = null;
            service.CelebrationTriggered += (sender, args) => receivedArgs = args;

            // Act
            service.TriggerPerfectGame(95);

            // Assert
            receivedArgs.Should().NotBeNull();
            receivedArgs!.Type.Should().Be(CelebrationType.PerfectGame);
            receivedArgs.Score.Should().Be(95);
            receivedArgs.Message.Should().Contain("INCREDIBLE");
        }

        [Fact]
        public void TriggerScorePop_RaisesScorePopEvent()
        {
            // Arrange
            var service = CelebrationService.CreateNew();
            CelebrationEventArgs? receivedArgs = null;
            service.CelebrationTriggered += (sender, args) => receivedArgs = args;

            // Act
            service.TriggerScorePop(7);

            // Assert
            receivedArgs.Should().NotBeNull();
            receivedArgs!.Type.Should().Be(CelebrationType.ScorePop);
            receivedArgs.Score.Should().Be(7);
            receivedArgs.DurationMs.Should().Be(400); // Quick pop animation
        }

        [Theory]
        [InlineData(90, "EXCELLENT")]
        [InlineData(95, "EXCELLENT")]
        [InlineData(80, "GREAT")]
        [InlineData(89, "GREAT")]
        [InlineData(70, "GOOD")]
        [InlineData(79, "GOOD")]
        [InlineData(50, "Game Complete")]
        public void TriggerGameCompleted_ReturnsAppropriateMessage(int score, string expectedMessage)
        {
            // Arrange
            var service = CelebrationService.CreateNew();
            CelebrationEventArgs? receivedArgs = null;
            service.CelebrationTriggered += (sender, args) => receivedArgs = args;

            // Act
            service.TriggerGameCompleted(score);

            // Assert
            receivedArgs.Should().NotBeNull();
            receivedArgs!.Type.Should().Be(CelebrationType.GameCompleted);
            receivedArgs.Score.Should().Be(score);
            receivedArgs.Message.Should().Contain(expectedMessage);
        }

        [Fact]
        public void Singleton_ReturnsSameInstance()
        {
            // Act
            var instance1 = CelebrationService.Instance;
            var instance2 = CelebrationService.Instance;

            // Assert
            instance1.Should().BeSameAs(instance2);
        }

        [Fact]
        public void CreateNew_ReturnsDifferentInstances()
        {
            // Act
            var instance1 = CelebrationService.CreateNew();
            var instance2 = CelebrationService.CreateNew();

            // Assert
            instance1.Should().NotBeSameAs(instance2);
        }

        [Fact]
        public void MultipleSubscribers_AllReceiveEvents()
        {
            // Arrange
            var service = CelebrationService.CreateNew();
            var receivedCount = 0;
            service.CelebrationTriggered += (sender, args) => receivedCount++;
            service.CelebrationTriggered += (sender, args) => receivedCount++;
            service.CelebrationTriggered += (sender, args) => receivedCount++;

            // Act
            service.TriggerScorePop(5);

            // Assert
            receivedCount.Should().Be(3);
        }
    }
}
