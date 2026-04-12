using Microsoft.Extensions.Logging;
using NinetyNine.Model;
using NinetyNine.Repository.Repositories;
using NinetyNine.Services.Models;

namespace NinetyNine.Services;

/// <summary>
/// Computes player statistics and leaderboard entries by aggregating over completed game records.
/// </summary>
public sealed class StatisticsService(
    IGameRepository gameRepository,
    IPlayerRepository playerRepository,
    IFriendshipRepository friendshipRepository,
    ICommunityMemberRepository communityMemberRepository,
    ILogger<StatisticsService> logger)
    : IStatisticsService
{
    public async Task<PlayerStats> GetPlayerStatsAsync(Guid playerId, CancellationToken ct = default)
    {
        logger.LogDebug("Computing stats for player {PlayerId}", playerId);

        var allGames = await gameRepository.GetByPlayerAsync(playerId, skip: 0, limit: int.MaxValue, ct);
        var completedGames = allGames.Where(g => g.GameState == GameState.Completed).ToList();

        int gamesCompleted = completedGames.Count;
        double averageScore = gamesCompleted > 0
            ? completedGames.Average(g => (double)g.TotalScore)
            : 0;
        int bestScore = gamesCompleted > 0 ? completedGames.Max(g => g.TotalScore) : 0;
        int perfectGames = completedGames.Count(g => g.IsPerfectGame);
        int perfectFrames = completedGames.Sum(g => g.PerfectFrames);
        DateTime? lastPlayed = allGames.Count > 0
            ? allGames.Max(g => g.WhenPlayed)
            : null;

        return new PlayerStats(
            PlayerId: playerId,
            GamesPlayed: allGames.Count,
            GamesCompleted: gamesCompleted,
            AverageScore: Math.Round(averageScore, 2),
            BestScore: bestScore,
            PerfectGames: perfectGames,
            PerfectFrames: perfectFrames,
            LastPlayed: lastPlayed);
    }

    public async Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(
        int limit, CancellationToken ct = default)
    {
        logger.LogDebug("Generating leaderboard (top {Limit})", limit);

        // Get recent games and group by player
        var recentGames = await gameRepository.GetRecentAsync(limit: 5000, ct);

        var completedByPlayer = recentGames
            .Where(g => g.GameState == GameState.Completed)
            .GroupBy(g => g.PlayerId);

        var entries = new List<LeaderboardEntry>();

        foreach (var group in completedByPlayer)
        {
            var games = group.ToList();
            var playerId = group.Key;
            var player = await playerRepository.GetByIdAsync(playerId, ct);
            if (player is null || player.RetiredAt is not null) continue;

            string? avatarUrl = player.Avatar is not null
                ? $"/api/avatars/{playerId}"
                : null;

            entries.Add(new LeaderboardEntry(
                PlayerId: playerId,
                DisplayName: player.DisplayName,
                AvatarUrl: avatarUrl,
                GamesPlayed: games.Count,
                AverageScore: Math.Round(games.Average(g => (double)g.TotalScore), 2),
                BestScore: games.Max(g => g.TotalScore)));
        }

        // Sort by average score descending, then best score
        var result = entries
            .OrderByDescending(e => e.AverageScore)
            .ThenByDescending(e => e.BestScore)
            .Take(limit)
            .ToList();

        return result.AsReadOnly();
    }

    public async Task<IReadOnlyList<Game>> GetBestGamesAsync(
        Guid playerId, int limit, CancellationToken ct = default)
    {
        var completedGames = await gameRepository.GetCompletedByPlayerAsync(playerId, ct);

        var best = completedGames
            .OrderByDescending(g => g.TotalScore)
            .Take(limit)
            .ToList();

        return best.AsReadOnly();
    }

    public async Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardForFriendsAsync(
        Guid viewerId, int limit, CancellationToken ct = default)
    {
        var friendships = await friendshipRepository.ListForPlayerAsync(viewerId, ct);
        var playerIds = new HashSet<Guid> { viewerId };
        foreach (var f in friendships)
            playerIds.Add(f.OtherParty(viewerId));

        return await GetLeaderboardForPlayerSetAsync(playerIds, limit, ct);
    }

    public async Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardForCommunityAsync(
        Guid communityId, int limit, CancellationToken ct = default)
    {
        var memberships = await communityMemberRepository.ListMembersAsync(
            communityId, skip: 0, limit: int.MaxValue, ct);
        var playerIds = memberships.Select(m => m.PlayerId).ToHashSet();

        return await GetLeaderboardForPlayerSetAsync(playerIds, limit, ct);
    }

    /// <summary>
    /// Core leaderboard logic filtered to a specific set of player IDs.
    /// Reuses the same aggregation logic as <see cref="GetLeaderboardAsync"/>
    /// but only includes games from the given player set.
    /// </summary>
    private async Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardForPlayerSetAsync(
        HashSet<Guid> playerIds, int limit, CancellationToken ct)
    {
        var recentGames = await gameRepository.GetRecentAsync(limit: 5000, ct);

        var completedByPlayer = recentGames
            .Where(g => g.GameState == GameState.Completed && playerIds.Contains(g.PlayerId))
            .GroupBy(g => g.PlayerId);

        var entries = new List<LeaderboardEntry>();

        foreach (var group in completedByPlayer)
        {
            var games = group.ToList();
            var playerId = group.Key;
            var player = await playerRepository.GetByIdAsync(playerId, ct);
            if (player is null || player.RetiredAt is not null) continue;

            string? avatarUrl = player.Avatar is not null
                ? $"/api/avatars/{playerId}"
                : null;

            entries.Add(new LeaderboardEntry(
                PlayerId: playerId,
                DisplayName: player.DisplayName,
                AvatarUrl: avatarUrl,
                GamesPlayed: games.Count,
                AverageScore: Math.Round(games.Average(g => (double)g.TotalScore), 2),
                BestScore: games.Max(g => g.TotalScore)));
        }

        return entries
            .OrderByDescending(e => e.AverageScore)
            .ThenByDescending(e => e.BestScore)
            .Take(limit)
            .ToList()
            .AsReadOnly();
    }
}
