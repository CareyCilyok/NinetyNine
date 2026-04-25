using NinetyNine.Model;

namespace NinetyNine.Services;

/// <summary>
/// Orchestrates the lifecycle of a NinetyNine game, from creation through frame scoring to completion.
/// </summary>
public interface IGameService
{
    /// <summary>Starts a new game for the given player at the specified venue.</summary>
    /// <param name="isEfrenVariant">
    /// True if the game should be scored under "Efren" variant rules
    /// (no ball-in-hand placement after the break). Defaults to false.
    /// </param>
    Task<Game> StartNewGameAsync(Guid playerId, Guid venueId, TableSize tableSize, bool isEfrenVariant = false, CancellationToken ct = default);

    /// <summary>Records a completed frame score and advances the game state.</summary>
    Task<Game> RecordFrameAsync(Guid gameId, int frameNumber, int breakBonus, int ballCount, string? notes, CancellationToken ct = default);

    /// <summary>Resets a specific frame back to its initial state (e.g. to correct an entry error).</summary>
    Task<Game> ResetFrameAsync(Guid gameId, int frameNumber, CancellationToken ct = default);

    /// <summary>Finalizes the game after all 9 frames are complete.</summary>
    Task<Game> CompleteGameAsync(Guid gameId, CancellationToken ct = default);

    /// <summary>Retrieves a game by ID, or null if not found.</summary>
    Task<Game?> GetGameAsync(Guid gameId, CancellationToken ct = default);
}
