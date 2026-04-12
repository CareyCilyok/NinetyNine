using NinetyNine.Model;

namespace NinetyNine.Services;

/// <summary>
/// Orchestrates multi-game match play. Owns the match lifecycle
/// (Created → InProgress → Completed/Abandoned), creates constituent
/// <see cref="Game"/> documents via <see cref="IGameService"/>, and
/// computes win conditions for all three formats (Single, RaceTo,
/// BestOf).
/// <para>See <c>docs/plans/v2-roadmap.md</c> Sprint 10 S10.2.</para>
/// </summary>
public interface IMatchService
{
    /// <summary>
    /// Creates a new match with the given players and format. Also
    /// creates the first <see cref="Game"/> for the first player to
    /// break. Returns the created match.
    /// </summary>
    Task<ServiceResult<Match>> CreateMatchAsync(
        Guid creatorPlayerId,
        Guid opponentPlayerId,
        Guid venueId,
        TableSize tableSize,
        MatchFormat format,
        int target,
        BreakMethod breakMethod,
        int? tableNumber = null,
        string? stakes = null,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves a match with all its constituent games loaded.
    /// </summary>
    Task<MatchDetail?> GetMatchDetailAsync(Guid matchId, CancellationToken ct = default);

    /// <summary>
    /// Called by the match-scoring UI when a game within the match
    /// completes. Detects win conditions and either creates the next
    /// game (if the match continues) or marks the match Completed.
    /// </summary>
    Task<ServiceResult<Match>> OnGameCompletedAsync(
        Guid matchId,
        Guid completedGameId,
        CancellationToken ct = default);

    /// <summary>
    /// Abandons an in-progress match. Either player can abandon.
    /// </summary>
    Task<ServiceResult> AbandonMatchAsync(
        Guid matchId,
        Guid byPlayerId,
        CancellationToken ct = default);

    /// <summary>
    /// Lists matches a player participated in. Used on the profile
    /// page and for leaderboard win/loss records.
    /// </summary>
    Task<IReadOnlyList<Match>> ListForPlayerAsync(
        Guid playerId,
        MatchStatus? status = null,
        int skip = 0,
        int limit = 20,
        CancellationToken ct = default);
}

/// <summary>
/// Match + the full game documents it references. Convenience
/// projection for UI that needs both in one shot.
/// </summary>
public sealed record MatchDetail(Match Match, IReadOnlyList<Game> Games);
