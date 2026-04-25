using Microsoft.Extensions.Logging;
using NinetyNine.Model;
using NinetyNine.Repository.Repositories;

namespace NinetyNine.Services;

/// <summary>
/// Implements the full NinetyNine game scoring flow.
/// Delegates state transitions to the <see cref="Game"/> entity and persists via <see cref="IGameRepository"/>.
/// </summary>
public sealed class GameService(IGameRepository gameRepository, ILogger<GameService> logger)
    : IGameService
{
    public async Task<Game> StartNewGameAsync(
        Guid playerId, Guid venueId, TableSize tableSize,
        bool isEfrenVariant = false, CancellationToken ct = default)
    {
        logger.LogInformation(
            "Starting new game for player {PlayerId} at venue {VenueId} on {TableSize} table (Efren={IsEfren})",
            playerId, venueId, tableSize, isEfrenVariant);

        var game = new Game
        {
            PlayerId = playerId,
            VenueId = venueId,
            TableSize = tableSize,
            WhenPlayed = DateTime.UtcNow,
            IsEfrenVariant = isEfrenVariant
        };

        game.InitializeFrames();

        await gameRepository.CreateAsync(game, ct);
        return game;
    }

    public async Task<Game> RecordFrameAsync(
        Guid gameId, int frameNumber, int breakBonus, int ballCount, string? notes,
        CancellationToken ct = default)
    {
        var game = await GetRequiredGameAsync(gameId, ct);

        if (!game.IsInProgress)
            throw new InvalidOperationException(
                $"Game {gameId} is not in progress (state: {game.GameState}).");

        var frame = game.Frames.FirstOrDefault(f => f.FrameNumber == frameNumber)
            ?? throw new ArgumentException($"Frame {frameNumber} not found in game {gameId}.", nameof(frameNumber));

        if (!frame.IsActive)
            throw new InvalidOperationException(
                $"Frame {frameNumber} is not the active frame (current: {game.CurrentFrameNumber}).");

        logger.LogDebug(
            "Recording frame {FrameNumber} for game {GameId}: breakBonus={BreakBonus}, ballCount={BallCount}",
            frameNumber, gameId, breakBonus, ballCount);

        // CompleteCurrentFrame validates, auto-advances, and auto-finalizes on frame 9
        game.CompleteCurrentFrame(breakBonus, ballCount, notes);

        if (game.IsCompleted)
        {
            logger.LogInformation(
                "Game {GameId} auto-completed after all 9 frames with total score {TotalScore}",
                gameId, game.TotalScore);
        }

        await gameRepository.UpdateAsync(game, ct);
        return game;
    }

    public async Task<Game> ResetFrameAsync(
        Guid gameId, int frameNumber, CancellationToken ct = default)
    {
        var game = await GetRequiredGameAsync(gameId, ct);

        if (!game.IsInProgress)
            throw new InvalidOperationException(
                $"Game {gameId} is not in progress (state: {game.GameState}).");

        var frame = game.Frames.FirstOrDefault(f => f.FrameNumber == frameNumber)
            ?? throw new ArgumentException($"Frame {frameNumber} not found in game {gameId}.", nameof(frameNumber));

        logger.LogInformation("Resetting frame {FrameNumber} in game {GameId}", frameNumber, gameId);

        bool wasActive = frame.IsActive;
        frame.ResetFrame();

        // If we reset the currently active frame, mark it active again
        if (wasActive)
        {
            frame.IsActive = true;
        }
        else if (frame.IsCompleted == false && frameNumber <= game.CurrentFrameNumber)
        {
            // Resetting a previously completed frame — reactivate it and deactivate current
            var currentActive = game.Frames.FirstOrDefault(f => f.IsActive);
            if (currentActive is not null)
            {
                currentActive.IsActive = false;
            }
            frame.IsActive = true;
            game.CurrentFrameNumber = frameNumber;

            // Recalculate running totals for all frames from this point forward
            RecalculateRunningTotals(game, frameNumber);
        }

        await gameRepository.UpdateAsync(game, ct);
        return game;
    }

    public async Task<Game> CompleteGameAsync(Guid gameId, CancellationToken ct = default)
    {
        var game = await GetRequiredGameAsync(gameId, ct);

        if (!game.IsInProgress)
            throw new InvalidOperationException(
                $"Game {gameId} is not in progress (state: {game.GameState}).");

        if (game.CompletedFrames < 9)
            throw new InvalidOperationException(
                $"Cannot complete game {gameId}: only {game.CompletedFrames} of 9 frames are completed.");

        logger.LogInformation("Completing game {GameId} with total score {TotalScore}", gameId, game.TotalScore);

        game.GameState = GameState.Completed;
        game.CompletedAt = DateTime.UtcNow;

        await gameRepository.UpdateAsync(game, ct);
        return game;
    }

    public Task<Game?> GetGameAsync(Guid gameId, CancellationToken ct = default)
        => gameRepository.GetByIdAsync(gameId, ct);

    private async Task<Game> GetRequiredGameAsync(Guid gameId, CancellationToken ct)
    {
        var game = await gameRepository.GetByIdAsync(gameId, ct);
        if (game is null)
            throw new KeyNotFoundException($"Game {gameId} not found.");
        return game;
    }

    /// <summary>
    /// Recalculates running totals for all completed frames from <paramref name="fromFrameNumber"/> onwards.
    /// </summary>
    private static void RecalculateRunningTotals(Game game, int fromFrameNumber)
    {
        int runningTotal = game.Frames
            .Where(f => f.IsCompleted && f.FrameNumber < fromFrameNumber)
            .OrderByDescending(f => f.FrameNumber)
            .FirstOrDefault()?.RunningTotal ?? 0;

        foreach (var frame in game.Frames
            .Where(f => f.IsCompleted && f.FrameNumber >= fromFrameNumber)
            .OrderBy(f => f.FrameNumber))
        {
            runningTotal += frame.FrameScore;
            frame.RunningTotal = runningTotal;
        }
    }
}
