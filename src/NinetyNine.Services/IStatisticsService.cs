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
}
