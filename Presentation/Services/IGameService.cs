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
    /// Service interface for managing game operations
    /// </summary>
    public interface IGameService
    {
        /// <summary>
        /// Gets the currently active game
        /// </summary>
        Game? CurrentGame { get; }

        /// <summary>
        /// Event raised when the current game changes
        /// </summary>
        event EventHandler<Game?> CurrentGameChanged;

        /// <summary>
        /// Event raised when a frame is completed
        /// </summary>
        event EventHandler<Frame> FrameCompleted;

        /// <summary>
        /// Event raised when a game is completed
        /// </summary>
        event EventHandler<Game> GameCompleted;

        /// <summary>
        /// Creates a new game
        /// </summary>
        /// <param name="player">The player</param>
        /// <param name="venue">The venue</param>
        /// <param name="tableSize">The table size</param>
        /// <returns>The new game</returns>
        Task<Game> CreateNewGameAsync(Player player, Venue venue, TableSize tableSize);

        /// <summary>
        /// Loads an existing game
        /// </summary>
        /// <param name="gameId">The game ID</param>
        /// <returns>The loaded game</returns>
        Task<Game?> LoadGameAsync(Guid gameId);

        /// <summary>
        /// Saves the current game
        /// </summary>
        /// <returns>True if successful</returns>
        Task<bool> SaveCurrentGameAsync();

        /// <summary>
        /// Completes the current frame with the specified scores
        /// </summary>
        /// <param name="breakBonus">Break bonus (0 or 1)</param>
        /// <param name="ballCount">Ball count (0-10)</param>
        /// <param name="notes">Optional notes</param>
        /// <returns>True if successful</returns>
        Task<bool> CompleteCurrentFrameAsync(int breakBonus, int ballCount, string? notes = null);

        /// <summary>
        /// Advances to the next frame
        /// </summary>
        /// <returns>True if successful</returns>
        Task<bool> AdvanceToNextFrameAsync();

        /// <summary>
        /// Resets the current frame
        /// </summary>
        /// <returns>True if successful</returns>
        Task<bool> ResetCurrentFrameAsync();

        /// <summary>
        /// Completes the current game
        /// </summary>
        /// <returns>True if successful</returns>
        Task<bool> CompleteGameAsync();

        /// <summary>
        /// Pauses the current game
        /// </summary>
        /// <returns>True if successful</returns>
        Task<bool> PauseGameAsync();

        /// <summary>
        /// Resumes the current game
        /// </summary>
        /// <returns>True if successful</returns>
        Task<bool> ResumeGameAsync();

        /// <summary>
        /// Validates the current game state
        /// </summary>
        /// <returns>True if valid</returns>
        bool ValidateCurrentGame();
    }
}