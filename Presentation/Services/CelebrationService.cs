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

using System;

namespace NinetyNine.Presentation.Services
{
    /// <summary>
    /// Service for triggering celebration effects in the UI
    /// </summary>
    public class CelebrationService : ICelebrationService
    {
        private static readonly Lazy<CelebrationService> _instance =
            new Lazy<CelebrationService>(() => new CelebrationService());

        /// <summary>
        /// Singleton instance of the celebration service
        /// </summary>
        public static CelebrationService Instance => _instance.Value;

        /// <summary>
        /// Create a new instance (for testing)
        /// </summary>
        public static CelebrationService CreateNew() => new CelebrationService();

        /// <inheritdoc/>
        public event EventHandler<CelebrationEventArgs>? CelebrationTriggered;

        /// <summary>
        /// Private constructor for singleton pattern
        /// </summary>
        private CelebrationService()
        {
        }

        /// <inheritdoc/>
        public void TriggerPerfectFrame(int frameNumber)
        {
            if (frameNumber < 1 || frameNumber > 9)
            {
                throw new ArgumentOutOfRangeException(nameof(frameNumber),
                    "Frame number must be between 1 and 9");
            }

            var args = new CelebrationEventArgs(
                CelebrationType.PerfectFrame,
                frameNumber,
                11,
                $"PERFECT FRAME {frameNumber}!",
                1500);

            OnCelebrationTriggered(args);
        }

        /// <inheritdoc/>
        public void TriggerPerfectGame(int totalScore)
        {
            string message;
            int duration;

            if (totalScore == 99)
            {
                message = "LEGENDARY! PERFECT 99!";
                duration = 5000;
            }
            else
            {
                message = $"INCREDIBLE! Score: {totalScore}";
                duration = 3000;
            }

            var args = new CelebrationEventArgs(
                CelebrationType.PerfectGame,
                9,
                totalScore,
                message,
                duration);

            OnCelebrationTriggered(args);
        }

        /// <inheritdoc/>
        public void TriggerScorePop(int score)
        {
            var args = new CelebrationEventArgs(
                CelebrationType.ScorePop,
                0,
                score,
                score.ToString(),
                400);

            OnCelebrationTriggered(args);
        }

        /// <inheritdoc/>
        public void TriggerGameCompleted(int totalScore)
        {
            string message = totalScore switch
            {
                99 => "LEGENDARY! PERFECT 99!",
                >= 90 => $"EXCELLENT! Score: {totalScore}",
                >= 80 => $"GREAT! Score: {totalScore}",
                >= 70 => $"GOOD! Score: {totalScore}",
                _ => $"Game Complete! Score: {totalScore}"
            };

            var args = new CelebrationEventArgs(
                CelebrationType.GameCompleted,
                9,
                totalScore,
                message,
                3000);

            OnCelebrationTriggered(args);
        }

        /// <summary>
        /// Raises the CelebrationTriggered event
        /// </summary>
        protected virtual void OnCelebrationTriggered(CelebrationEventArgs args)
        {
            CelebrationTriggered?.Invoke(this, args);
        }
    }
}
