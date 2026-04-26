using NinetyNine.Model;
using NinetyNine.Services.Models;

namespace NinetyNine.Services;

/// <summary>
/// Aggregates game data into statistics, leaderboards, and best-game lists.
/// Every read accepts an optional <c>efrenOnly</c> filter that restricts the
/// aggregation to games where <see cref="Game.IsEfrenVariant"/> is true.
/// </summary>
public interface IStatisticsService
{
    /// <summary>
    /// Aggregates a single player's stats. When <paramref name="efrenOnly"/>
    /// is true, only Efren-variant games count toward the result.
    /// </summary>
    Task<PlayerStats> GetPlayerStatsAsync(
        Guid playerId, bool efrenOnly = false, CancellationToken ct = default);

    /// <summary>
    /// Top players by average score. When <paramref name="efrenOnly"/> is
    /// true, only Efren-variant games count.
    /// </summary>
    Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(
        int limit, bool efrenOnly = false, CancellationToken ct = default);

    /// <summary>
    /// A player's highest-scoring completed games. When <paramref name="efrenOnly"/>
    /// is true, only Efren-variant games are considered.
    /// </summary>
    Task<IReadOnlyList<Game>> GetBestGamesAsync(
        Guid playerId, int limit, bool efrenOnly = false, CancellationToken ct = default);

    /// <summary>
    /// Leaderboard filtered to only the viewer's mutual friends (+ the viewer).
    /// </summary>
    Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardForFriendsAsync(
        Guid viewerId, int limit, bool efrenOnly = false, CancellationToken ct = default);

    /// <summary>
    /// Leaderboard filtered to current members of a specific community.
    /// </summary>
    Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardForCommunityAsync(
        Guid communityId, int limit, bool efrenOnly = false, CancellationToken ct = default);

    // ── Per-discipline rating (v0.9.0) ──────────────────────────────────

    /// <summary>
    /// Computes the player's NinetyNine handicap-style rating for one
    /// <see cref="GameDiscipline"/>. The formula mirrors a golf handicap:
    /// take the highest 5 game totals from the player's last 20 completed
    /// games of this discipline, average them. For players with fewer
    /// than 5 completed games of the discipline, return the simple
    /// average across all their games (best-of-N is undefined when
    /// N &lt; 5). Confidence is <c>min(GameCount, 20) × 5</c> percent —
    /// at 20 games the rating is "certified".
    /// <para>
    /// On-demand computation; not cached on the Player document. The
    /// active dataset is small (≤ 20 games per discipline per player by
    /// definition of the formula), so the per-call cost is negligible.
    /// Move to a cached field if usage patterns change.
    /// </para>
    /// </summary>
    Task<NinetyNineRating> CalculateRatingAsync(
        Guid playerId,
        GameDiscipline discipline,
        CancellationToken ct = default);

    /// <summary>
    /// Returns every discipline-rating the player has any games for, plus
    /// a zero-rating placeholder for any discipline they haven't played
    /// yet. Useful for the profile page's "ratings dictionary" display:
    /// Standard 99 + Efren 99 today; future disciplines as they're added.
    /// </summary>
    Task<IReadOnlyDictionary<GameDiscipline, NinetyNineRating>> GetAllRatingsAsync(
        Guid playerId,
        CancellationToken ct = default);
}

/// <summary>
/// Player rating for one <see cref="GameDiscipline"/>, golf-handicap
/// style. See <see cref="IStatisticsService.CalculateRatingAsync"/> for
/// the formula. The full rating document for the profile page is the
/// <c>IReadOnlyDictionary&lt;GameDiscipline, NinetyNineRating&gt;</c>
/// returned by <see cref="IStatisticsService.GetAllRatingsAsync"/>.
/// </summary>
/// <param name="Discipline">The game discipline this rating is for.</param>
/// <param name="Rating">Average score (0.0 – 99.0). Zero when no games.</param>
/// <param name="GameCount">Total completed games of this discipline.</param>
/// <param name="ConfidencePercent">
/// Confidence band: <c>min(GameCount, 20) × 5</c>%. Zero when no games;
/// 100% (= "certified") at 20 games.
/// </param>
/// <param name="IsCertified">True when GameCount ≥ 20.</param>
/// <param name="UsedHandicapFormula">
/// True when the rating is the average of the best 5 of the last 20
/// games (≥ 5 completed games); false when it's the simple average
/// (&lt; 5 completed games). Lets the UI show "best 5 of last 20" vs
/// "average of N games" hint copy correctly.
/// </param>
public sealed record NinetyNineRating(
    GameDiscipline Discipline,
    double Rating,
    int GameCount,
    int ConfidencePercent,
    bool IsCertified,
    bool UsedHandicapFormula)
{
    /// <summary>True when the player has at least one completed game of this discipline.</summary>
    public bool HasRating => GameCount > 0;

    /// <summary>Convenience: rating rounded to one decimal for display.</summary>
    public double DisplayRating => Math.Round(Rating, 1);
}
