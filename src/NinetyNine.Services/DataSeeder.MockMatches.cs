using Microsoft.Extensions.Logging;
using NinetyNine.Model;

namespace NinetyNine.Services;

/// <summary>
/// Match-history generator for the seeded mock roster. Pairs players
/// of similar Fargo for competitive 2-, 3-, and 4-player concurrent
/// matches, generates a Game per seat (each scored from that player's
/// Fargo distribution — see <c>DataSeeder.MockHistory.cs</c>), then
/// persists the Match document with the correct winner per the
/// concurrent-match arbiter (highest TotalScore → most PerfectFrames →
/// earliest CompletedAt). Idempotent: skips if any of the seeded mock
/// players already have a match record.
/// </summary>
public sealed partial class DataSeeder
{
    /// <summary>
    /// Match templates — picks players by display name from the mock
    /// roster, sets a venue, and a number of days ago to back-date the
    /// match. Players in a match should be within ~100 Fargo points of
    /// each other for competitiveness; the templates below honour that.
    /// </summary>
    private sealed record MockMatchTemplate(
        string[] PlayerDisplayNames,
        string VenueName,
        int DaysAgo,
        bool ForceEfren = false);

    /// <summary>
    /// Hand-picked matches across the bracket spectrum. Mix of head-to-
    /// head, 3-player, and 4-player concurrent matches. The pro-tier
    /// matches are flagged ForceEfren so every game in those matches
    /// is the Efren variant — this is where the pros' demo data shines.
    /// </summary>
    private static readonly MockMatchTemplate[] SeededMockMatches =
    [
        // ── Local league head-to-heads (B / B+ amateurs) ──────────────
        new(["b_chris", "b_kaitlyn"],         "Bumpers Billiards of Huntsville", 2),
        new(["b_doug", "b_marco"],            "Steve's Cue and Grill",            5),
        new(["bp_kevin", "bp_dale"],          "Bumpers Billiards of Huntsville", 8),
        new(["bp_anh", "bp_sam"],             "Iron City Billiards",             14),
        new(["b_priya", "b_jen"],             "Chips & Salsa Sports Bar & Grill", 4),
        new(["c_marie", "c_jason"],           "Good Timez Billiards",            10),
        new(["danny_c", "c_ricky"],           "Limestone Legends Billiards",     16),

        // ── Strong-amateur head-to-heads (A- / A+) ────────────────────
        new(["a_mike", "a_sarah"],            "Iron City Billiards",              6),
        new(["a_jorge", "a_quentin"],         "MrCues II Billiards",             11),
        new(["ap_riley", "ap_dom"],           "Melrose Billiard Parlor",          9),

        // ── 3-player matches ──────────────────────────────────────────
        new(["b_chris", "b_kaitlyn", "bp_kevin"], "Steve's Cue and Grill",       18),
        new(["a_mike", "a_jorge", "a_sarah"],     "Iron City Billiards",         22),
        new(["bp_dale", "bp_sam", "a_quentin"],   "Bumpers Billiards of Huntsville", 25),
        new(["c_nina", "c_jason", "danny_c"],     "Good Timez Billiards",        30),

        // ── 4-player matches (tournament-style) ───────────────────────
        new(["b_chris", "b_doug", "b_marco", "bp_anh"],     "Bumpers Billiards of Huntsville", 35),
        new(["a_mike", "ap_riley", "elite_trent", "a_sarah"], "Iron City Billiards",            40),

        // ── Pro matches (Efren-only) ──────────────────────────────────
        new(["pro_svb", "pro_filler"],                       "MrCues II Billiards",   12, ForceEfren: true),
        new(["pro_shaw", "pro_ko"],                          "Melrose Billiard Parlor", 19, ForceEfren: true),
        new(["pro_gorst", "pro_hohmann", "pro_efren"],       "Iron City Billiards",   28, ForceEfren: true),
        new(["pro_svb", "pro_filler", "pro_gorst", "pro_shaw"], "MrCues II Billiards", 45, ForceEfren: true),
    ];

    /// <summary>
    /// Reconcile pass — creates Match + per-seat Game documents for
    /// every entry in <see cref="SeededMockMatches"/>, scoring each
    /// player's game from their Fargo distribution. Idempotent: gated
    /// on whether any mock player already has match records (cheap
    /// O(1) check).
    /// </summary>
    private async Task<(int Matches, int Games)> ReconcileMockMatchesAsync(CancellationToken ct)
    {
        // Quick idempotency probe: pick any pro display name and check
        // if they have any match records. If yes, the seed has run before.
        var probe = await playerRepository.GetByDisplayNameAsync("pro_svb", ct);
        if (probe is not null)
        {
            var existingMatches = await matchRepository.ListForPlayerAsync(
                probe.PlayerId, status: null, skip: 0, limit: 1, ct);
            if (existingMatches.Count > 0) return (0, 0);
        }

        var venuesByName = (await venueRepository.GetAllAsync(includePrivate: true, ct))
            .ToDictionary(v => v.Name, StringComparer.OrdinalIgnoreCase);

        int matchesCreated = 0;
        int gamesCreated = 0;

        foreach (var template in SeededMockMatches)
        {
            // Resolve players. Skip the whole match if any seat doesn't
            // resolve (template editing safety).
            var players = new List<Player>();
            bool resolved = true;
            foreach (var name in template.PlayerDisplayNames)
            {
                var p = await playerRepository.GetByDisplayNameAsync(name, ct);
                if (p is null) { resolved = false; break; }
                players.Add(p);
            }
            if (!resolved)
            {
                logger.LogWarning(
                    "Skipping mock match — one or more players in template not yet seeded: [{Players}]",
                    string.Join(", ", template.PlayerDisplayNames));
                continue;
            }

            if (!venuesByName.TryGetValue(template.VenueName, out var venue))
            {
                logger.LogWarning(
                    "Skipping mock match at '{Venue}' — venue not found.",
                    template.VenueName);
                continue;
            }

            // Whether to force Efren on this match: explicit template
            // flag, OR if every player in the seat lineup is an
            // Efren-only template (i.e. all pros).
            bool isAllEfrenRoster = template.PlayerDisplayNames
                .All(name => SeededProMockPlayers.Any(p => p.DisplayName == name));
            bool useEfren = template.ForceEfren || isAllEfrenRoster;

            // Generate one Game per seat using each player's Fargo
            // distribution. Persist all games, then create the Match
            // referencing them.
            var when = DateTime.UtcNow.AddDays(-template.DaysAgo);
            var rng = new Random(string.Join("|", template.PlayerDisplayNames).GetHashCode());

            var seatGames = new List<Game>();
            foreach (var player in players)
            {
                var fargo = player.FargoRating ?? 500;
                var scores = GenerateGameFrameScores(fargo, rng, useEfren);
                var game = BuildSeededHistoryGame(
                    player, venue, scores,
                    daysAgo: template.DaysAgo,
                    minutesOffset: rng.Next(0, 60),
                    isEfrenVariant: useEfren);
                await gameRepository.CreateAsync(game, ct);
                seatGames.Add(game);
                gamesCreated++;
            }

            // Pick the winner per the concurrent-match arbiter:
            // highest TotalScore → most PerfectFrames → earliest CompletedAt.
            var winnerGame = seatGames
                .OrderByDescending(g => g.TotalScore)
                .ThenByDescending(g => g.PerfectFrames)
                .ThenBy(g => g.CompletedAt ?? DateTime.MaxValue)
                .First();

            var match = new Match
            {
                Rotation = MatchRotation.Concurrent,
                Format = MatchFormat.Single,
                Target = 1,
                PlayerIds = players.Select(p => p.PlayerId).ToList(),
                GameIds = seatGames.Select(g => g.GameId).ToList(),
                CurrentPlayerSeat = 0,
                BreakMethod = BreakMethod.Lagged,
                VenueId = venue.VenueId,
                Status = MatchStatus.Completed,
                CreatedAt = when,
                CompletedAt = when.AddMinutes(45 + 5 * players.Count),
                WinnerPlayerId = winnerGame.PlayerId,
            };

            await matchRepository.CreateAsync(match, ct);
            matchesCreated++;

            logger.LogInformation(
                "Seeded mock match: {Players} at {Venue} ({DaysAgo}d ago) → winner {Winner} ({Score}/99)",
                string.Join(" vs ", template.PlayerDisplayNames),
                venue.Name, template.DaysAgo,
                players.Single(p => p.PlayerId == winnerGame.PlayerId).DisplayName,
                winnerGame.TotalScore);
        }

        return (matchesCreated, gamesCreated);
    }
}
