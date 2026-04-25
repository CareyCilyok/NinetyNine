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
    /// Creates a new <see cref="MatchRotation.Sequential"/> head-to-head
    /// match with the given players and format. Also creates the first
    /// <see cref="Game"/> for the first player to break.
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
    /// Creates a new <see cref="MatchRotation.Concurrent"/> multi-player
    /// match (2–4 players) and starts a complete 9-frame
    /// <see cref="Game"/> for every player simultaneously. The match's
    /// <c>CurrentPlayerSeat</c> begins at 0 — the first
    /// <paramref name="players"/> entry breaks first. Each player's
    /// <see cref="Game.IsEfrenVariant"/> is set independently from the
    /// per-player setup; Efren is a per-Game property in NinetyNine,
    /// not a match-level switch.
    /// </summary>
    /// <param name="creatorPlayerId">
    /// The player who created the match. Must appear in
    /// <paramref name="players"/>.
    /// </param>
    /// <param name="players">
    /// 2–4 player setups, in seating/lag order. The seat-0 player breaks
    /// the first inning. Duplicate <c>PlayerId</c>s are rejected.
    /// </param>
    Task<ServiceResult<Match>> CreateConcurrentMatchAsync(
        Guid creatorPlayerId,
        IReadOnlyList<ConcurrentMatchPlayerSetup> players,
        Guid venueId,
        TableSize tableSize,
        BreakMethod breakMethod,
        int? tableNumber = null,
        string? stakes = null,
        CancellationToken ct = default);

    /// <summary>
    /// Advances a <see cref="MatchRotation.Concurrent"/> match's
    /// <c>CurrentPlayerSeat</c> to the next player after the active
    /// seat finishes their inning (i.e., after their current frame is
    /// recorded). Wraps from seat N-1 back to seat 0; on wrap, the
    /// per-Game <c>CurrentFrameNumber</c> for every player is already
    /// at the next frame because each Game auto-advances on
    /// <c>RecordFrame</c>.
    /// <para>
    /// When every player's Game has reached <see cref="GameState.Completed"/>,
    /// the match is finalized (status → Completed, winner computed by
    /// highest <c>TotalScore</c>, ties broken by most perfect frames
    /// then earliest game-completion timestamp).
    /// </para>
    /// </summary>
    Task<ServiceResult<Match>> FinishInningAsync(
        Guid matchId,
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

/// <summary>
/// Per-player setup for <see cref="IMatchService.CreateConcurrentMatchAsync"/>.
/// Carries the player identity plus their per-Game Efren-variant flag —
/// Efren is per-Game in NinetyNine, so a four-player concurrent match
/// can mix Efren and standard scoring across players.
/// </summary>
public sealed record ConcurrentMatchPlayerSetup(Guid PlayerId, bool IsEfrenVariant);
