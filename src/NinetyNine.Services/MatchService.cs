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

    // ── Concurrent (multi-player, alternating-innings) matches ───────────────

    /// <summary>
    /// Minimum players for a concurrent match. v0.9.2 lowered this from
    /// 2 to 1 to allow solo matches — a 1-player match is just the
    /// player's own 9-frame game, recorded through the unified match
    /// flow (see CLAUDE.md: there's one workflow for all play).
    /// </summary>
    public const int ConcurrentMinPlayers = 1;
    public const int ConcurrentMaxPlayers = 4;

    public async Task<ServiceResult<Match>> CreateConcurrentMatchAsync(
        Guid creatorPlayerId,
        IReadOnlyList<ConcurrentMatchPlayerSetup> players,
        Guid venueId,
        TableSize tableSize,
        BreakMethod breakMethod,
        int? tableNumber = null,
        string? stakes = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(players);

        if (players.Count < ConcurrentMinPlayers || players.Count > ConcurrentMaxPlayers)
            return ServiceResult<Match>.Fail(
                "InvalidPlayerCount",
                $"Concurrent matches require {ConcurrentMinPlayers}–{ConcurrentMaxPlayers} players.");

        var distinctPlayerIds = players.Select(p => p.PlayerId).Distinct().Count();
        if (distinctPlayerIds != players.Count)
            return ServiceResult<Match>.Fail(
                "DuplicatePlayer", "A concurrent match cannot have the same player in more than one seat.");

        if (!players.Any(p => p.PlayerId == creatorPlayerId))
            return ServiceResult<Match>.Fail(
                "CreatorNotInMatch", "The match creator must be one of the participating players.");

        var match = new Match
        {
            Rotation = MatchRotation.Concurrent,
            // Concurrent matches have a fixed shape (9 frames per player); the
            // Format/Target fields don't drive a win count for them but we
            // record sensible defaults so the persisted document is sane.
            Format = MatchFormat.Single,
            Target = 1,
            PlayerIds = players.Select(p => p.PlayerId).ToList(),
            CurrentPlayerSeat = 0,
            BreakMethod = breakMethod,
            TableNumber = tableNumber,
            Stakes = string.IsNullOrWhiteSpace(stakes) ? null : stakes.Trim(),
            VenueId = venueId,
            Status = MatchStatus.InProgress,
        };

        // Start a complete 9-frame Game for every player up front. All games
        // begin in the InProgress state with frame 1 active; rotation gating
        // happens at the match level via CurrentPlayerSeat.
        foreach (var setup in players)
        {
            var game = await gameService.StartNewGameAsync(
                setup.PlayerId, venueId, tableSize, setup.IsEfrenVariant, ct);
            match.GameIds.Add(game.GameId);
        }

        await matches.CreateAsync(match, ct);

        logger.LogInformation(
            "Concurrent match {MatchId} created: {PlayerCount} players, venue {VenueId}, {GameCount} games",
            match.MatchId, players.Count, venueId, match.GameIds.Count);

        return ServiceResult<Match>.Ok(match);
    }

    public async Task<ServiceResult<Match>> FinishInningAsync(
        Guid matchId, CancellationToken ct = default)
    {
        var match = await matches.GetByIdAsync(matchId, ct);
        if (match is null)
            return ServiceResult<Match>.Fail("MatchNotFound", "Match not found.");

        if (match.Rotation != MatchRotation.Concurrent)
            return ServiceResult<Match>.Fail(
                "WrongMatchRotation",
                "FinishInning is only valid for Concurrent matches; Sequential matches finalize on game completion.");

        if (match.Status != MatchStatus.InProgress)
            return ServiceResult<Match>.Fail(
                "MatchNotInProgress", "This match is not in progress.");

        // Reload all constituent games to evaluate completion + tie-breakers.
        var allGames = new List<Game>(match.GameIds.Count);
        foreach (var gameId in match.GameIds)
        {
            var game = await games.GetByIdAsync(gameId, ct);
            if (game is null)
                return ServiceResult<Match>.Fail(
                    "GameNotFound", $"Constituent game {gameId} not found.");
            allGames.Add(game);
        }

        // If every player's Game is complete the match is over — compute the
        // winner. Every-game-complete is the only end-of-match signal for
        // Concurrent matches; CurrentPlayerSeat doesn't gate completion
        // because the seat may not land on 0 when the last frame finishes.
        if (allGames.All(g => g.GameState == GameState.Completed))
        {
            var winnerId = SelectConcurrentWinner(allGames);
            match.Status = MatchStatus.Completed;
            match.WinnerPlayerId = winnerId;
            match.CompletedAt = DateTime.UtcNow;
            await matches.UpdateAsync(match, ct);

            logger.LogInformation(
                "Concurrent match {MatchId} completed: winner {WinnerId} (final scores: {Scores})",
                matchId, winnerId,
                string.Join(", ", allGames.Select(g => $"{g.PlayerId}={g.TotalScore}")));

            return ServiceResult<Match>.Ok(match);
        }

        // Otherwise rotate to the next seat that still has frames left to play.
        // Skipping fully-completed seats keeps the rotation honest when one
        // player finishes their nine frames before the others (their Game is
        // Completed but the match continues).
        var seatCount = match.PlayerIds.Count;
        for (int step = 1; step <= seatCount; step++)
        {
            var candidateSeat = (match.CurrentPlayerSeat + step) % seatCount;
            var candidatePlayerId = match.PlayerIds[candidateSeat];
            var candidateGame = allGames.FirstOrDefault(g => g.PlayerId == candidatePlayerId);
            if (candidateGame is not null && candidateGame.GameState != GameState.Completed)
            {
                match.CurrentPlayerSeat = candidateSeat;
                await matches.UpdateAsync(match, ct);

                logger.LogDebug(
                    "Concurrent match {MatchId}: inning advanced to seat {Seat} (player {PlayerId}, frame {Frame})",
                    matchId, candidateSeat, candidatePlayerId, candidateGame.CurrentFrameNumber);

                return ServiceResult<Match>.Ok(match);
            }
        }

        // Defensive: if no seat has remaining frames but not every game is
        // Completed, the data is inconsistent. Fail loudly rather than spin.
        return ServiceResult<Match>.Fail(
            "RotationStalled",
            "No seat has remaining frames, but the match is not yet complete. " +
            "This indicates a corrupted Match/Game state.");
    }

    /// <summary>
    /// Selects the winner of a completed concurrent match.
    /// Primary: highest <see cref="Game.TotalScore"/>.
    /// Tie-break 1: most <see cref="Game.PerfectFrames"/> (frames scoring 11).
    /// Tie-break 2: earliest <see cref="Game.CompletedAt"/> — the player who
    /// finished their nine frames first wins among otherwise-tied scores.
    /// </summary>
    internal static Guid SelectConcurrentWinner(IReadOnlyList<Game> games)
    {
        if (games.Count == 0)
            throw new InvalidOperationException("Cannot select a winner from an empty game list.");

        // DateTime.MaxValue puts any null CompletedAt at the back of the
        // tie-break order (a Completed game without CompletedAt is malformed,
        // but we don't want to crash on it).
        return games
            .OrderByDescending(g => g.TotalScore)
            .ThenByDescending(g => g.PerfectFrames)
            .ThenBy(g => g.CompletedAt ?? DateTime.MaxValue)
            .First()
            .PlayerId;
    }

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
