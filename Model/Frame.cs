/// Copyright (c) 2020-2022
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
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace NinetyNine.Model
{
    /// <summary>
    /// Represents a single frame (rack) in a game of Ninety-Nine
    /// </summary>
    /// <remarks>
    /// A frame has a maximum score of 11 points:
    /// - Break Bonus: 0 or 1 point (awarded for legally pocketing balls on break)
    /// - Ball Count: 0-10 points (1 point per ball, except 9-ball which is worth 2 points)
    /// </remarks>
    public class Frame : INotifyPropertyChanged
    {
        private int _breakBonus = 0;
        private int _ballCount = 0;
        private bool _isCompleted = false;
        private bool _isActive = false;
        private DateTime? _completedAt;

        public Guid FrameId { get; set; } = Guid.NewGuid();

        public Guid GameId { get; set; }
        
        [JsonIgnore]
        public Game? Game { get; set; }

        /// <summary>
        /// The frame number within the game (1-9)
        /// </summary>
        [Range(1, 9, ErrorMessage = "Frame number must be between 1 and 9")]
        public int FrameNumber { get; set; }

        /// <summary>
        /// Break bonus points (0 or 1)
        /// </summary>
        [Range(0, 1, ErrorMessage = "Break bonus must be 0 or 1")]
        public int BreakBonus
        {
            get => _breakBonus;
            set
            {
                if (_breakBonus != value)
                {
                    _breakBonus = value;
                    OnPropertyChanged(nameof(BreakBonus));
                    OnPropertyChanged(nameof(FrameScore));
                    OnPropertyChanged(nameof(IsValidScore));
                }
            }
        }

        /// <summary>
        /// Number of balls pocketed (0-10, with 9-ball worth 2 points)
        /// </summary>
        [Range(0, 10, ErrorMessage = "Ball count must be between 0 and 10")]
        public int BallCount
        {
            get => _ballCount;
            set
            {
                if (_ballCount != value)
                {
                    _ballCount = value;
                    OnPropertyChanged(nameof(BallCount));
                    OnPropertyChanged(nameof(FrameScore));
                    OnPropertyChanged(nameof(IsValidScore));
                }
            }
        }

        /// <summary>
        /// Total score for this frame (BreakBonus + BallCount)
        /// </summary>
        [JsonIgnore]
        public int FrameScore => BreakBonus + BallCount;

        /// <summary>
        /// Running total score through this frame
        /// </summary>
        public int RunningTotal { get; set; } = 0;

        /// <summary>
        /// Whether this frame has been completed
        /// </summary>
        public bool IsCompleted
        {
            get => _isCompleted;
            set
            {
                if (_isCompleted != value)
                {
                    _isCompleted = value;
                    if (value && !_completedAt.HasValue)
                    {
                        _completedAt = DateTime.Now;
                    }
                    OnPropertyChanged(nameof(IsCompleted));
                }
            }
        }

        /// <summary>
        /// Whether this is the currently active frame being played
        /// </summary>
        [JsonIgnore]
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChanged(nameof(IsActive));
                }
            }
        }

        /// <summary>
        /// When this frame was completed
        /// </summary>
        public DateTime? CompletedAt
        {
            get => _completedAt;
            set
            {
                if (_completedAt != value)
                {
                    _completedAt = value;
                    OnPropertyChanged(nameof(CompletedAt));
                }
            }
        }

        /// <summary>
        /// Whether the current score is valid (max 11 points per frame)
        /// </summary>
        [JsonIgnore]
        public bool IsValidScore => FrameScore <= 11;

        /// <summary>
        /// Whether this frame achieved the maximum possible score (11 points)
        /// </summary>
        [JsonIgnore]
        public bool IsPerfectFrame => FrameScore == 11;

        /// <summary>
        /// Additional notes for this frame (fouls, scratches, etc.)
        /// </summary>
        public string? Notes { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Validates the frame scoring according to Ninety-Nine rules
        /// </summary>
        /// <returns>True if the frame is valid, false otherwise</returns>
        public bool ValidateFrame()
        {
            // Break bonus can only be 0 or 1
            if (BreakBonus < 0 || BreakBonus > 1)
                return false;

            // Ball count must be between 0 and 10
            if (BallCount < 0 || BallCount > 10)
                return false;

            // Total frame score cannot exceed 11
            if (FrameScore > 11)
                return false;

            // Frame number must be between 1 and 9
            if (FrameNumber < 1 || FrameNumber > 9)
                return false;

            return true;
        }

        /// <summary>
        /// Completes the frame and calculates the running total
        /// </summary>
        /// <param name="previousRunningTotal">The running total from the previous frame</param>
        public void CompleteFrame(int previousRunningTotal = 0)
        {
            if (!ValidateFrame())
                throw new InvalidOperationException("Cannot complete frame with invalid scores");

            RunningTotal = previousRunningTotal + FrameScore;
            IsCompleted = true;
            IsActive = false;
        }

        /// <summary>
        /// Resets the frame to allow re-scoring
        /// </summary>
        public void ResetFrame()
        {
            BreakBonus = 0;
            BallCount = 0;
            IsCompleted = false;
            CompletedAt = null;
            Notes = null;
        }
    }
}
