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
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;

namespace NinetyNine.Model
{
    /// <summary>
    /// Represents the current state of a game
    /// </summary>
    public enum GameState
    {
        NotStarted,
        InProgress,
        Completed,
        Paused
    }

    /// <summary>
    /// Represents a single Game of Ninety-Nine
    /// </summary>
    /// <remarks>
    /// A Game is made up of 9 <see cref="Frames"/> with a maximum total score of 99 points
    /// </remarks>
    public class Game : INotifyPropertyChanged
    {
        private GameState _gameState = GameState.NotStarted;
        private int _currentFrameNumber = 1;

        /// <summary>
        /// Gets/Sets the unique identifier for a single <see cref="Game"/> played
        /// </summary>
        public Guid GameId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Gets/Sets the <see cref="NinetyNine.Model.Player"/> of this <see cref="Game"/>
        /// </summary>
        [JsonIgnore]
        public Player? Player { get; set; }

        /// <summary>
        /// Gets/Sets the Player ID for serialization
        /// </summary>
        public Guid PlayerId { get; set; }

        /// <summary>
        /// Gets/Sets the location where this game took place. <seealso cref="Venue"/>
        /// </summary>
        [JsonIgnore]
        public Venue? LocationPlayed { get; set; }

        /// <summary>
        /// Gets/Sets the Venue ID for serialization
        /// </summary>
        public Guid VenueId { get; set; }

        /// <summary>
        /// Gets/Sets the date and time when this <see cref="Game"/> was started
        /// </summary>
        public DateTime WhenPlayed { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets/Sets the date and time when this <see cref="Game"/> was completed
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Gets/Sets the size of the pool table played on. <seealso cref="NinetyNine.Model.TableSize"/>
        /// </summary>
        public TableSize TableSize { get; set; } = TableSize.Unknown;

        /// <summary>
        /// Gets/Sets the current state of the game
        /// </summary>
        public GameState GameState
        {
            get => _gameState;
            set
            {
                if (_gameState != value)
                {
                    _gameState = value;
                    if (value == GameState.Completed && !CompletedAt.HasValue)
                    {
                        CompletedAt = DateTime.Now;
                    }
                    OnPropertyChanged(nameof(GameState));
                    OnPropertyChanged(nameof(IsInProgress));
                    OnPropertyChanged(nameof(IsCompleted));
                }
            }
        }

        /// <summary>
        /// Gets/Sets the current frame number being played (1-9)
        /// </summary>
        public int CurrentFrameNumber
        {
            get => _currentFrameNumber;
            set
            {
                if (_currentFrameNumber != value && value >= 1 && value <= 9)
                {
                    _currentFrameNumber = value;
                    OnPropertyChanged(nameof(CurrentFrameNumber));
                }
            }
        }

        /// <summary>
        /// Gets/Sets the list of frames played in this <see cref="Game"/>. <seealso cref="Frame"/>
        /// </summary>
        public List<Frame> Frames { get; set; } = new List<Frame>(9);

        /// <summary>
        /// Gets the total score for the completed frames
        /// </summary>
        [JsonIgnore]
        public int TotalScore => Frames.Where(f => f.IsCompleted).Sum(f => f.FrameScore);

        /// <summary>
        /// Gets the current running total (used for display)
        /// </summary>
        [JsonIgnore]
        public int RunningTotal => Frames.LastOrDefault(f => f.IsCompleted)?.RunningTotal ?? 0;

        /// <summary>
        /// Gets whether the game is currently in progress
        /// </summary>
        [JsonIgnore]
        public bool IsInProgress => GameState == GameState.InProgress;

        /// <summary>
        /// Gets whether the game has been completed
        /// </summary>
        [JsonIgnore]
        public bool IsCompleted => GameState == GameState.Completed;

        /// <summary>
        /// Gets the number of completed frames
        /// </summary>
        [JsonIgnore]
        public int CompletedFrames => Frames.Count(f => f.IsCompleted);

        /// <summary>
        /// Gets the currently active frame
        /// </summary>
        [JsonIgnore]
        public Frame? CurrentFrame => Frames.FirstOrDefault(f => f.IsActive);

        /// <summary>
        /// Gets the average score per completed frame
        /// </summary>
        [JsonIgnore]
        public double AverageScore => CompletedFrames > 0 ? (double)TotalScore / CompletedFrames : 0;

        /// <summary>
        /// Gets the highest scoring frame in the game
        /// </summary>
        [JsonIgnore]
        public Frame? BestFrame => Frames.Where(f => f.IsCompleted).OrderByDescending(f => f.FrameScore).FirstOrDefault();

        /// <summary>
        /// Gets the number of perfect frames (11 points) in the game
        /// </summary>
        [JsonIgnore]
        public int PerfectFrames => Frames.Count(f => f.IsPerfectFrame);

        /// <summary>
        /// Gets whether this game achieved the maximum possible score (99 points)
        /// </summary>
        [JsonIgnore]
        public bool IsPerfectGame => IsCompleted && TotalScore == 99;

        /// <summary>
        /// Additional notes about the game
        /// </summary>
        public string? Notes { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Initializes a new game with 9 empty frames
        /// </summary>
        public void InitializeFrames()
        {
            Frames.Clear();
            for (int i = 1; i <= 9; i++)
            {
                Frames.Add(new Frame
                {
                    GameId = GameId,
                    Game = this,
                    FrameNumber = i,
                    IsActive = i == 1
                });
            }
            CurrentFrameNumber = 1;
            GameState = GameState.InProgress;
        }

        /// <summary>
        /// Advances to the next frame
        /// </summary>
        /// <returns>True if successfully advanced, false if already at last frame</returns>
        public bool AdvanceToNextFrame()
        {
            var currentFrame = CurrentFrame;
            if (currentFrame != null && !currentFrame.IsCompleted)
            {
                throw new InvalidOperationException("Cannot advance to next frame until current frame is completed");
            }

            if (CurrentFrameNumber >= 9)
            {
                GameState = GameState.Completed;
                return false;
            }

            // Deactivate current frame
            if (currentFrame != null)
            {
                currentFrame.IsActive = false;
            }

            // Activate next frame
            CurrentFrameNumber++;
            var nextFrame = Frames.FirstOrDefault(f => f.FrameNumber == CurrentFrameNumber);
            if (nextFrame != null)
            {
                nextFrame.IsActive = true;
            }

            // Check if game is complete
            if (CurrentFrameNumber > 9 || Frames.Count(f => f.IsCompleted) == 9)
            {
                GameState = GameState.Completed;
            }

            return true;
        }

        /// <summary>
        /// Completes the current frame and advances to the next
        /// </summary>
        /// <param name="breakBonus">Break bonus for the frame (0 or 1)</param>
        /// <param name="ballCount">Number of balls pocketed (0-10)</param>
        /// <param name="notes">Optional notes for the frame</param>
        public void CompleteCurrentFrame(int breakBonus, int ballCount, string? notes = null)
        {
            var currentFrame = CurrentFrame;
            if (currentFrame == null)
            {
                throw new InvalidOperationException("No active frame to complete");
            }

            currentFrame.BreakBonus = breakBonus;
            currentFrame.BallCount = ballCount;
            currentFrame.Notes = notes;

            if (!currentFrame.ValidateFrame())
            {
                throw new ArgumentException("Invalid frame scores provided");
            }

            var previousTotal = CurrentFrameNumber > 1 ? 
                Frames.Where(f => f.FrameNumber < CurrentFrameNumber && f.IsCompleted)
                      .Sum(f => f.FrameScore) : 0;

            currentFrame.CompleteFrame(previousTotal);

            // Update property notifications
            OnPropertyChanged(nameof(TotalScore));
            OnPropertyChanged(nameof(RunningTotal));
            OnPropertyChanged(nameof(CompletedFrames));

            // Auto-advance to next frame or complete game
            if (CurrentFrameNumber < 9)
            {
                AdvanceToNextFrame();
            }
            else
            {
                GameState = GameState.Completed;
            }
        }

        /// <summary>
        /// Validates that the game is properly configured
        /// </summary>
        /// <returns>True if valid, false otherwise</returns>
        public bool ValidateGame()
        {
            // Must have exactly 9 frames
            if (Frames.Count != 9)
                return false;

            // Frame numbers must be 1-9
            for (int i = 0; i < 9; i++)
            {
                if (Frames[i].FrameNumber != i + 1)
                    return false;
            }

            // All completed frames must be valid
            foreach (var frame in Frames.Where(f => f.IsCompleted))
            {
                if (!frame.ValidateFrame())
                    return false;
            }

            return true;
        }
    }
}