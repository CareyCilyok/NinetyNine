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
    /// Types of celebrations that can be triggered
    /// </summary>
    public enum CelebrationType
    {
        ScorePop,
        PerfectFrame,
        PerfectGame,
        GameCompleted
    }

    /// <summary>
    /// Event arguments for celebration events
    /// </summary>
    public class CelebrationEventArgs : EventArgs
    {
        public CelebrationType Type { get; }
        public int FrameNumber { get; }
        public int Score { get; }
        public string Message { get; }
        public int DurationMs { get; }

        public CelebrationEventArgs(CelebrationType type, int frameNumber, int score, string message, int durationMs = 3000)
        {
            Type = type;
            FrameNumber = frameNumber;
            Score = score;
            Message = message;
            DurationMs = durationMs;
        }
    }

    /// <summary>
    /// Service for triggering celebration effects in the UI
    /// </summary>
    public interface ICelebrationService
    {
        /// <summary>
        /// Raised when a celebration should be displayed
        /// </summary>
        event EventHandler<CelebrationEventArgs>? CelebrationTriggered;

        /// <summary>
        /// Trigger a perfect frame celebration (11 points)
        /// </summary>
        /// <param name="frameNumber">The frame number (1-9)</param>
        void TriggerPerfectFrame(int frameNumber);

        /// <summary>
        /// Trigger a perfect game celebration (99 points)
        /// </summary>
        /// <param name="totalScore">The total score achieved</param>
        void TriggerPerfectGame(int totalScore);

        /// <summary>
        /// Trigger a score pop animation
        /// </summary>
        /// <param name="score">The score to display</param>
        void TriggerScorePop(int score);

        /// <summary>
        /// Trigger a game completed celebration
        /// </summary>
        /// <param name="totalScore">The final score</param>
        void TriggerGameCompleted(int totalScore);
    }
}
