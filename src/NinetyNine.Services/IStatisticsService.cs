using NinetyNine.Model;
using NinetyNine.Services.Models;

namespace NinetyNine.Services;

/// <summary>
/// Aggregates game data into statistics, leaderboards, and best-game lists.
/// </summary>
public interface IStatisticsService
{
    Task<PlayerStats> GetPlayerStatsAsync(Guid playerId, CancellationToken ct = default);
    Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(int limit, CancellationToken ct = default);
    Task<IReadOnlyList<Game>> GetBestGamesAsync(Guid playerId, int limit, CancellationToken ct = default);
}
