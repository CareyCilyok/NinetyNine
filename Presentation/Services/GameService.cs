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
using System.Threading.Tasks;
using NinetyNine.Model;

namespace NinetyNine.Presentation.Services
{
    /// <summary>
    /// Service for managing game operations and state
    /// </summary>
    public class GameService : IGameService
    {
        private Game? _currentGame;

        public GameService()
        {
        }

        /// <summary>
        /// Gets the currently active game
        /// </summary>
        public Game? CurrentGame
        {
            get => _currentGame;
            private set
            {
                _currentGame = value;
                CurrentGameChanged?.Invoke(this, _currentGame);
            }
        }

        /// <summary>
        /// Event raised when the current game changes
        /// </summary>
        public event EventHandler<Game?>? CurrentGameChanged;

        /// <summary>
        /// Event raised when a frame is completed
        /// </summary>
        public event EventHandler<Frame>? FrameCompleted;

        /// <summary>
        /// Event raised when a game is completed
        /// </summary>
        public event EventHandler<Game>? GameCompleted;

        /// <summary>
        /// Creates a new game
        /// </summary>
        /// <param name="player">The player</param>
        /// <param name="venue">The venue</param>
        /// <param name="tableSize">The table size</param>
        /// <returns>The new game</returns>
        public async Task<Game> CreateNewGameAsync(Player player, Venue venue, TableSize tableSize)
        {
            var game = new Game
            {
                Player = player,
                PlayerId = player.PlayerId,
                LocationPlayed = venue,
                VenueId = venue.VenueId,
                TableSize = tableSize,
                WhenPlayed = DateTime.Now,
                GameState = GameState.NotStarted
            };

            // Initialize the frames
            game.InitializeFrames();
            CurrentGame = game;

            return await Task.FromResult(game);
        }

        /// <summary>
        /// Loads an existing game
        /// </summary>
        /// <param name="gameId">The game ID</param>
        /// <returns>The loaded game</returns>
        public async Task<Game?> LoadGameAsync(Guid gameId)
        {
            // TODO: Implement actual loading from repository
            return await Task.FromResult<Game?>(null);
        }

        /// <summary>
        /// Saves the current game
        /// </summary>
        /// <returns>True if successful</returns>
        public async Task<bool> SaveCurrentGameAsync()
        {
            if (CurrentGame == null)
                return false;

            try
            {
                // TODO: Implement actual saving to repository
                var isValid = ValidateCurrentGame();
                return await Task.FromResult(isValid);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Completes the current frame with the specified scores
        /// </summary>
        /// <param name="breakBonus">Break bonus (0 or 1)</param>
        /// <param name="ballCount">Ball count (0-10)</param>
        /// <param name="notes">Optional notes</param>
        /// <returns>True if successful</returns>
        public async Task<bool> CompleteCurrentFrameAsync(int breakBonus, int ballCount, string? notes = null)
        {
            if (CurrentGame == null)
                return false;

            var currentFrame = CurrentGame.CurrentFrame;
            if (currentFrame == null)
                return false;

            try
            {
                // Validate scores
                if (breakBonus < 0 || breakBonus > 1)
                    return false;

                if (ballCount < 0 || ballCount > 10)
                    return false;

                if (breakBonus + ballCount > 11)
                    return false;

                // Complete the frame
                CurrentGame.CompleteCurrentFrame(breakBonus, ballCount, notes);

                // Raise frame completed event
                FrameCompleted?.Invoke(this, currentFrame);

                // Check if game is complete
                if (CurrentGame.IsCompleted)
                {
                    GameCompleted?.Invoke(this, CurrentGame);
                }

                // Auto-save after each frame
                await SaveCurrentGameAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Advances to the next frame
        /// </summary>
        /// <returns>True if successful</returns>
        public async Task<bool> AdvanceToNextFrameAsync()
        {
            if (CurrentGame == null)
                return false;

            try
            {
                var success = CurrentGame.AdvanceToNextFrame();
                return await Task.FromResult(success);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Resets the current frame
        /// </summary>
        /// <returns>True if successful</returns>
        public async Task<bool> ResetCurrentFrameAsync()
        {
            if (CurrentGame == null)
                return false;

            var currentFrame = CurrentGame.CurrentFrame;
            if (currentFrame == null)
                return false;

            try
            {
                currentFrame.ResetFrame();
                return await Task.FromResult(true);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Completes the current game
        /// </summary>
        /// <returns>True if successful</returns>
        public async Task<bool> CompleteGameAsync()
        {
            if (CurrentGame == null)
                return false;

            try
            {
                CurrentGame.GameState = GameState.Completed;
                
                // Raise game completed event
                GameCompleted?.Invoke(this, CurrentGame);
                
                // Save the completed game
                await SaveCurrentGameAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Pauses the current game
        /// </summary>
        /// <returns>True if successful</returns>
        public async Task<bool> PauseGameAsync()
        {
            if (CurrentGame == null || !CurrentGame.IsInProgress)
                return false;

            CurrentGame.GameState = GameState.Paused;
            await SaveCurrentGameAsync();
            
            return true;
        }

        /// <summary>
        /// Resumes the current game
        /// </summary>
        /// <returns>True if successful</returns>
        public async Task<bool> ResumeGameAsync()
        {
            if (CurrentGame == null || CurrentGame.GameState != GameState.Paused)
                return false;

            CurrentGame.GameState = GameState.InProgress;
            await SaveCurrentGameAsync();
            
            return true;
        }

        /// <summary>
        /// Validates the current game state
        /// </summary>
        /// <returns>True if valid</returns>
        public bool ValidateCurrentGame()
        {
            if (CurrentGame == null)
                return false;

            return CurrentGame.ValidateGame();
        }
    }
}