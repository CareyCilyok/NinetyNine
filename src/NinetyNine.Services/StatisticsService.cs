using Microsoft.Extensions.Logging;
using NinetyNine.Model;
using NinetyNine.Repository.Repositories;
using NinetyNine.Services.Models;

namespace NinetyNine.Services;

/// <summary>
/// Computes player statistics and leaderboard entries by aggregating over completed game records.
/// All public reads support an optional <c>efrenOnly</c> filter that narrows
/// the aggregation to games where <see cref="Game.IsEfrenVariant"/> is true.
/// </summary>
public sealed class StatisticsService(
    IGameRepository gameRepository,
    IPlayerRepository playerRepository,
    IFriendshipRepository friendshipRepository,
    ICommunityMemberRepository communityMemberRepository,
    ILogger<StatisticsService> logger)
    : IStatisticsService
{
    public async Task<PlayerStats> GetPlayerStatsAsync(
        Guid playerId, bool efrenOnly = false, CancellationToken ct = default)
    {
        logger.LogDebug("Computing stats for player {PlayerId} (efrenOnly={EfrenOnly})",
            playerId, efrenOnly);

        var allGames = await gameRepository.GetByPlayerAsync(playerId, skip: 0, limit: int.MaxValue, ct);
        var scopedGames = ApplyVariantFilter(allGames, efrenOnly).ToList();
        var completedGames = scopedGames.Where(g => g.GameState == GameState.Completed).ToList();

        int gamesCompleted = completedGames.Count;
        double averageScore = gamesCompleted > 0
            ? completedGames.Average(g => (double)g.TotalScore)
            : 0;
        int bestScore = gamesCompleted > 0 ? completedGames.Max(g => g.TotalScore) : 0;
        int perfectGames = completedGames.Count(g => g.IsPerfectGame);
        int perfectFrames = completedGames.Sum(g => g.PerfectFrames);
        DateTime? lastPlayed = scopedGames.Count > 0
            ? scopedGames.Max(g => g.WhenPlayed)
            : null;

        return new PlayerStats(
            PlayerId: playerId,
            GamesPlayed: scopedGames.Count,
            GamesCompleted: gamesCompleted,
            AverageScore: Math.Round(averageScore, 2),
            BestScore: bestScore,
            PerfectGames: perfectGames,
            PerfectFrames: perfectFrames,
            LastPlayed: lastPlayed);
    }

    public async Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(
        int limit, bool efrenOnly = false, CancellationToken ct = default)
    {
        logger.LogDebug("Generating leaderboard (top {Limit}, efrenOnly={EfrenOnly})",
            limit, efrenOnly);

        var recentGames = await gameRepository.GetRecentAsync(limit: 5000, ct);

        var completedByPlayer = ApplyVariantFilter(recentGames, efrenOnly)
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

        var result = entries
            .OrderByDescending(e => e.AverageScore)
            .ThenByDescending(e => e.BestScore)
            .Take(limit)
            .ToList();

        return result.AsReadOnly();
    }

    public async Task<IReadOnlyList<Game>> GetBestGamesAsync(
        Guid playerId, int limit, bool efrenOnly = false, CancellationToken ct = default)
    {
        var completedGames = await gameRepository.GetCompletedByPlayerAsync(playerId, ct);

        var best = ApplyVariantFilter(completedGames, efrenOnly)
            .OrderByDescending(g => g.TotalScore)
            .Take(limit)
            .ToList();

        return best.AsReadOnly();
    }

    public async Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardForFriendsAsync(
        Guid viewerId, int limit, bool efrenOnly = false, CancellationToken ct = default)
    {
        var friendships = await friendshipRepository.ListForPlayerAsync(viewerId, ct);
        var playerIds = new HashSet<Guid> { viewerId };
        foreach (var f in friendships)
            playerIds.Add(f.OtherParty(viewerId));

        return await GetLeaderboardForPlayerSetAsync(playerIds, limit, efrenOnly, ct);
    }

    public async Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardForCommunityAsync(
        Guid communityId, int limit, bool efrenOnly = false, CancellationToken ct = default)
    {
        var memberships = await communityMemberRepository.ListMembersAsync(
            communityId, skip: 0, limit: int.MaxValue, ct);
        var playerIds = memberships.Select(m => m.PlayerId).ToHashSet();

        return await GetLeaderboardForPlayerSetAsync(playerIds, limit, efrenOnly, ct);
    }

    /// <summary>
    /// Core leaderboard logic filtered to a specific set of player IDs and
    /// optionally to Efren-variant games only.
    /// </summary>
    private async Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardForPlayerSetAsync(
        HashSet<Guid> playerIds, int limit, bool efrenOnly, CancellationToken ct)
    {
        var recentGames = await gameRepository.GetRecentAsync(limit: 5000, ct);

        var completedByPlayer = ApplyVariantFilter(recentGames, efrenOnly)
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

    /// <summary>
    /// Applies the Efren-variant filter when <paramref name="efrenOnly"/> is true.
    /// When false, returns the input unchanged. Centralizing the filter here
    /// keeps the per-method aggregation paths free of conditional bookkeeping.
    /// </summary>
    private static IEnumerable<Game> ApplyVariantFilter(IEnumerable<Game> games, bool efrenOnly) =>
        efrenOnly ? games.Where(g => g.IsEfrenVariant) : games;

    // ── Per-discipline rating (v0.9.0) ──────────────────────────────────

    /// <summary>
    /// Maps a <see cref="Game"/> to its <see cref="GameDiscipline"/>.
    /// v0.9.0 derives this from the legacy <see cref="Game.IsEfrenVariant"/>
    /// boolean. When the third discipline lands, this becomes
    /// <c>game.Discipline</c> (a real field on Game) and this helper
    /// goes away.
    /// </summary>
    internal static GameDiscipline DisciplineOf(Game game) =>
        game.IsEfrenVariant ? GameDiscipline.Efren99 : GameDiscipline.Standard99;

    public async Task<NinetyNineRating> CalculateRatingAsync(
        Guid playerId, GameDiscipline discipline, CancellationToken ct = default)
    {
        var allCompleted = await gameRepository.GetCompletedByPlayerAsync(playerId, ct);
        var disciplineGames = allCompleted
            .Where(g => DisciplineOf(g) == discipline)
            .ToList();
        return ComputeRating(discipline, disciplineGames);
    }

    public async Task<IReadOnlyDictionary<GameDiscipline, NinetyNineRating>>
        GetAllRatingsAsync(Guid playerId, CancellationToken ct = default)
    {
        var allCompleted = await gameRepository.GetCompletedByPlayerAsync(playerId, ct);
        var byDiscipline = allCompleted
            .GroupBy(DisciplineOf)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Game>)g.ToList());

        var result = new Dictionary<GameDiscipline, NinetyNineRating>();
        foreach (GameDiscipline d in Enum.GetValues<GameDiscipline>())
        {
            var games = byDiscipline.TryGetValue(d, out var list)
                ? list
                : Array.Empty<Game>();
            result[d] = ComputeRating(d, games);
        }
        return result;
    }

    /// <summary>
    /// Pure computation: given the player's completed games of one
    /// discipline, produces the rating record. Internally orders by
    /// <c>WhenPlayed</c> descending — callers don't need to pre-sort.
    /// Exposed <c>internal</c> so the unit tests can verify the formula
    /// without spinning up a Mongo container.
    /// </summary>
    internal static NinetyNineRating ComputeRating(
        GameDiscipline discipline, IReadOnlyList<Game> completedDisciplineGames)
    {
        int gameCount = completedDisciplineGames.Count;
        int confidence = Math.Min(gameCount, 20) * 5;
        bool certified = gameCount >= 20;

        if (gameCount == 0)
        {
            return new NinetyNineRating(
                Discipline: discipline,
                Rating: 0,
                GameCount: 0,
                ConfidencePercent: 0,
                IsCertified: false,
                UsedHandicapFormula: false);
        }

        if (gameCount < 5)
        {
            // < 5 games: simple average across all games. The "best 5
            // of last 20" formula is undefined when N < 5.
            var simpleAvg = completedDisciplineGames.Average(g => g.TotalScore);
            return new NinetyNineRating(
                Discipline: discipline,
                Rating: simpleAvg,
                GameCount: gameCount,
                ConfidencePercent: confidence,
                IsCertified: false,
                UsedHandicapFormula: false);
        }

        // ≥ 5 games: golf-handicap style. Take the most recent 20 games,
        // then within those the 5 highest scores. Average those 5.
        var lastTwenty = completedDisciplineGames
            .OrderByDescending(g => g.WhenPlayed)
            .Take(20)
            .ToList();
        var bestFiveAvg = lastTwenty
            .OrderByDescending(g => g.TotalScore)
            .Take(5)
            .Average(g => g.TotalScore);

        return new NinetyNineRating(
            Discipline: discipline,
            Rating: bestFiveAvg,
            GameCount: gameCount,
            ConfidencePercent: confidence,
            IsCertified: certified,
            UsedHandicapFormula: true);
    }
}
