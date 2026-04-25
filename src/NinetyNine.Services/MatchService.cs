using Microsoft.Extensions.Logging;
using NinetyNine.Model;
using NinetyNine.Repository.Repositories;

namespace NinetyNine.Services;

public sealed class MatchService(
    IMatchRepository matches,
    IGameRepository games,
    IGameService gameService,
    ILogger<MatchService> logger) : IMatchService
{
    public async Task<ServiceResult<Match>> CreateMatchAsync(
        Guid creatorPlayerId,
        Guid opponentPlayerId,
        Guid venueId,
        TableSize tableSize,
        MatchFormat format,
        int target,
        BreakMethod breakMethod,
        int? tableNumber = null,
        string? stakes = null,
        CancellationToken ct = default)
    {
        if (creatorPlayerId == opponentPlayerId)
            return ServiceResult<Match>.Fail(
                "SelfMatch", "You cannot start a match against yourself.");

        if (format == MatchFormat.Single && target != 1)
            return ServiceResult<Match>.Fail(
                "InvalidTarget", "Single-game matches must have target 1.");

        if (format != MatchFormat.Single && target < 2)
            return ServiceResult<Match>.Fail(
                "InvalidTarget", "Race-to and best-of matches must have target at least 2.");

        var match = new Match
        {
            Format = format,
            Target = target,
            PlayerIds = [creatorPlayerId, opponentPlayerId],
            BreakMethod = breakMethod,
            TableNumber = tableNumber,
            Stakes = string.IsNullOrWhiteSpace(stakes) ? null : stakes.Trim(),
            VenueId = venueId,
            Status = MatchStatus.InProgress,
        };

        // Create the first game for the creator (seat 0 breaks first).
        var firstGame = await gameService.StartNewGameAsync(
            creatorPlayerId, venueId, tableSize, ct: ct);
        match.GameIds.Add(firstGame.GameId);

        await matches.CreateAsync(match, ct);

        logger.LogInformation(
            "Match {MatchId} created: {Format} to {Target}, players {P1} vs {P2}, first game {GameId}",
            match.MatchId, format, target, creatorPlayerId, opponentPlayerId, firstGame.GameId);

        return ServiceResult<Match>.Ok(match);
    }

    public async Task<MatchDetail?> GetMatchDetailAsync(
        Guid matchId, CancellationToken ct = default)
    {
        var match = await matches.GetByIdAsync(matchId, ct);
        if (match is null) return null;

        var gameList = new List<Game>(match.GameIds.Count);
        foreach (var gameId in match.GameIds)
        {
            var game = await games.GetByIdAsync(gameId, ct);
            if (game is not null) gameList.Add(game);
        }

        return new MatchDetail(match, gameList.AsReadOnly());
    }

    public async Task<ServiceResult<Match>> OnGameCompletedAsync(
        Guid matchId, Guid completedGameId, CancellationToken ct = default)
    {
        var match = await matches.GetByIdAsync(matchId, ct);
        if (match is null)
            return ServiceResult<Match>.Fail("MatchNotFound", "Match not found.");

        if (match.Status != MatchStatus.InProgress)
            return ServiceResult<Match>.Fail(
                "MatchNotInProgress", "This match is not in progress.");

        // Tally wins so far from all constituent games.
        var wins = new Dictionary<Guid, int>();
        foreach (var playerId in match.PlayerIds)
            wins[playerId] = 0;

        foreach (var gameId in match.GameIds)
        {
            var game = await games.GetByIdAsync(gameId, ct);
            if (game is null || game.GameState != GameState.Completed) continue;
            if (wins.ContainsKey(game.PlayerId))
                wins[game.PlayerId]++;
        }

        var winsNeeded = WinsRequired(match);
        var leader = wins.OrderByDescending(kv => kv.Value).First();

        if (leader.Value >= winsNeeded)
        {
            // Match won.
            match.Status = MatchStatus.Completed;
            match.WinnerPlayerId = leader.Key;
            match.CompletedAt = DateTime.UtcNow;
            await matches.UpdateAsync(match, ct);

            logger.LogInformation(
                "Match {MatchId} completed: winner {WinnerId} ({Wins}/{Needed})",
                matchId, leader.Key, leader.Value, winsNeeded);

            return ServiceResult<Match>.Ok(match);
        }

        // Determine who breaks the next game. Default: alternate.
        // For PreviousLoserBreaks mode (standard head-to-head alt break),
        // the loser of the just-completed game breaks next.
        var completedGame = await games.GetByIdAsync(completedGameId, ct);
        if (completedGame is null)
            return ServiceResult<Match>.Fail(
                "GameNotFound", "Completed game not found.");

        Guid nextBreakerId;
        if (match.BreakMethod == BreakMethod.PreviousLoserBreaks)
        {
            // The loser of the completed game breaks the next.
            var loserId = match.PlayerIds.FirstOrDefault(p => p != completedGame.PlayerId);
            nextBreakerId = loserId != Guid.Empty ? loserId : match.PlayerIds[0];
        }
        else
        {
            // Alternate: whoever didn't break the previous game breaks next.
            var previousBreakerId = completedGame.PlayerId;
            nextBreakerId = match.PlayerIds.FirstOrDefault(p => p != previousBreakerId);
            if (nextBreakerId == Guid.Empty)
                nextBreakerId = match.PlayerIds[0];
        }

        // Create the next game using the completed game's venue + table size.
        var nextGame = await gameService.StartNewGameAsync(
            nextBreakerId, completedGame.VenueId, completedGame.TableSize, ct: ct);
        match.GameIds.Add(nextGame.GameId);
        await matches.UpdateAsync(match, ct);

        logger.LogInformation(
            "Match {MatchId}: game {GameId} completed, next game {NextGameId} ({Breaker} breaks)",
            matchId, completedGameId, nextGame.GameId, nextBreakerId);

        return ServiceResult<Match>.Ok(match);
    }

    public async Task<ServiceResult> AbandonMatchAsync(
        Guid matchId, Guid byPlayerId, CancellationToken ct = default)
    {
        var match = await matches.GetByIdAsync(matchId, ct);
        if (match is null)
            return ServiceResult.Fail("MatchNotFound", "Match not found.");

        if (!match.PlayerIds.Contains(byPlayerId))
            return ServiceResult.Fail(
                "NotAuthorized", "Only match participants can abandon a match.");

        if (match.Status != MatchStatus.InProgress && match.Status != MatchStatus.Created)
            return ServiceResult.Fail(
                "MatchNotActive", "This match is not active.");

        match.Status = MatchStatus.Abandoned;
        match.CompletedAt = DateTime.UtcNow;
        await matches.UpdateAsync(match, ct);

        logger.LogInformation("Match {MatchId} abandoned by {PlayerId}", matchId, byPlayerId);
        return ServiceResult.Ok();
    }

    public Task<IReadOnlyList<Match>> ListForPlayerAsync(
        Guid playerId, MatchStatus? status = null,
        int skip = 0, int limit = 20, CancellationToken ct = default)
        => matches.ListForPlayerAsync(playerId, status, skip, limit, ct);

    /// <summary>
    /// Computes how many game wins are needed to win the match.
    /// Single: 1. RaceTo: Target. BestOf: ⌈Target/2⌉ (first to majority).
    /// </summary>
    private static int WinsRequired(Match match) => match.Format switch
    {
        MatchFormat.Single => 1,
        MatchFormat.RaceTo => match.Target,
        MatchFormat.BestOf => (match.Target / 2) + 1,
        _ => 1,
    };
}
