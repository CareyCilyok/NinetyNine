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
using System.IO;
using System.Linq;
using System.Text.Json;
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
        private readonly string _gamesDirectory;
        private readonly Dictionary<Guid, Game> _gameCache = new();

        public GameService()
        {
            // Use application data folder for persistent storage
            _gamesDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NinetyNine",
                "Games");

            // Ensure directory exists
            if (!Directory.Exists(_gamesDirectory))
            {
                Directory.CreateDirectory(_gamesDirectory);
            }
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
            try
            {
                // Check cache first
                if (_gameCache.TryGetValue(gameId, out var cachedGame))
                {
                    CurrentGame = cachedGame;
                    return cachedGame;
                }

                // Load from file storage
                var filePath = GetGameFilePath(gameId);
                if (!File.Exists(filePath))
                {
                    return null;
                }

                var json = await File.ReadAllTextAsync(filePath);
                var game = JsonSerializer.Deserialize<Game>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (game != null)
                {
                    _gameCache[gameId] = game;
                    CurrentGame = game;
                }

                return game;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading game: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets all saved games
        /// </summary>
        public async Task<List<Game>> GetAllGamesAsync()
        {
            var games = new List<Game>();

            try
            {
                var files = Directory.GetFiles(_gamesDirectory, "*.json");
                foreach (var file in files)
                {
                    var json = await File.ReadAllTextAsync(file);
                    var game = JsonSerializer.Deserialize<Game>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    if (game != null)
                    {
                        games.Add(game);
                        _gameCache[game.GameId] = game;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading games: {ex.Message}");
            }

            return games;
        }

        private string GetGameFilePath(Guid gameId)
        {
            return Path.Combine(_gamesDirectory, $"{gameId}.json");
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
                // Validate game before saving
                if (!ValidateCurrentGame())
                {
                    return false;
                }

                // Serialize and save to file
                var json = JsonSerializer.Serialize(CurrentGame, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                var filePath = GetGameFilePath(CurrentGame.GameId);
                await File.WriteAllTextAsync(filePath, json);

                // Update cache
                _gameCache[CurrentGame.GameId] = CurrentGame;

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving game: {ex.Message}");
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

        /// <summary>
        /// Gets the most recent in-progress game, if any exists
        /// </summary>
        /// <returns>The most recent in-progress game, or null if none</returns>
        public async Task<Game?> GetMostRecentInProgressGameAsync()
        {
            var allGames = await GetAllGamesAsync();

            return allGames
                .Where(g => g.GameState == GameState.InProgress || g.GameState == GameState.Paused)
                .OrderByDescending(g => g.WhenPlayed)
                .FirstOrDefault();
        }

        /// <summary>
        /// Undoes the most recently completed frame (reverts to previous frame)
        /// </summary>
        /// <returns>True if successful</returns>
        public async Task<bool> UndoLastFrameAsync()
        {
            if (CurrentGame == null || CurrentGame.CurrentFrameNumber <= 1)
                return false;

            try
            {
                // Get the previous frame (the one we want to undo)
                var previousFrameNumber = CurrentGame.CurrentFrameNumber - 1;
                var previousFrame = CurrentGame.Frames.FirstOrDefault(f => f.FrameNumber == previousFrameNumber);

                if (previousFrame == null || !previousFrame.IsCompleted)
                    return false;

                // Get current frame and deactivate it
                var currentFrame = CurrentGame.CurrentFrame;
                if (currentFrame != null)
                {
                    currentFrame.IsActive = false;
                }

                // Reset the previous frame
                previousFrame.IsCompleted = false;
                previousFrame.IsActive = true;
                previousFrame.RunningTotal = 0;

                // Decrement the current frame number
                CurrentGame.CurrentFrameNumber = previousFrameNumber;

                // Recalculate running totals for remaining frames
                var runningTotal = 0;
                foreach (var frame in CurrentGame.Frames.Where(f => f.FrameNumber < previousFrameNumber && f.IsCompleted))
                {
                    runningTotal += frame.FrameScore;
                }

                // Save the updated game
                await SaveCurrentGameAsync();

                // Notify UI of changes
                CurrentGameChanged?.Invoke(this, CurrentGame);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error undoing last frame: {ex.Message}");
                return false;
            }
        }
    }
}