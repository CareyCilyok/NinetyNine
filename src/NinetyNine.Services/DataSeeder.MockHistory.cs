using Microsoft.Extensions.Logging;
using NinetyNine.Model;

namespace NinetyNine.Services;

/// <summary>
/// Game-history generator for the seeded mock roster. Produces per-frame
/// scores drawn from a per-bracket distribution that matches the
/// per-game means and standard deviations vetted by the project's pool
/// SME (poolplayer agent / "George"):
///
/// <list type="table">
///   <listheader><term>Fargo</term><description>Mean (std dev) per game</description></listheader>
///   <item><term>270–349 (Rec)</term>      <description>~12 (±6)</description></item>
///   <item><term>350–449 (C)</term>        <description>~22 (±8)</description></item>
///   <item><term>450–549 (B)</term>        <description>~38 (±10)</description></item>
///   <item><term>550–619 (B+)</term>       <description>~52 (±11)</description></item>
///   <item><term>620–699 (A-)</term>       <description>~62 (±10)</description></item>
///   <item><term>700–749 (A+)</term>       <description>~71 (±9)</description></item>
///   <item><term>750–799 (Elite)</term>    <description>~79 (±8)</description></item>
///   <item><term>800–850 (Pro)</term>      <description>~87 (±6)</description></item>
/// </list>
///
/// <para>
/// Per-frame distributions below were tuned so the natural sum-of-9 lands
/// on each bracket's expected per-game mean within ±2 points. Std-dev is
/// approximate (the natural variance of 9 i.i.d. draws ≈ √9 × σ_frame).
/// </para>
///
/// <para>
/// Efren-mode penalty curve (also from George): pro-tier players lose
/// ~12-13% of their per-game total, A-tier ~10%, B+ ~10%, B ~8%,
/// C ~5%. Applied per-frame via a stochastic downgrade (with
/// probability = penalty fraction, knock the generated frame score down
/// by 1-3 points, clamped to [0, 11]).
/// </para>
/// </summary>
public sealed partial class DataSeeder
{
    /// <summary>
    /// Number of games to seed per mock amateur (standard mode). The
    /// game-history reconcile pass skips any player who already has
    /// games — so this only fires on first startup. Increase to make
    /// stats / leaderboard pages more interesting at the cost of
    /// slower first-run seed time.
    /// </summary>
    private const int GamesPerMockAmateur = 8;

    /// <summary>
    /// Number of games per pro. Lower than amateurs because every pro
    /// game is Efren-variant and they all sit at the top of any Efren
    /// leaderboard regardless of count — extra games here just inflate
    /// startup time without changing the demo.
    /// </summary>
    private const int GamesPerMockPro = 5;

    /// <summary>
    /// Window over which seeded games are date-spread, in days. Each
    /// game gets a random WhenPlayed in [now-N, now], so the History
    /// page shows realistic time scatter.
    /// </summary>
    private const int GameHistoryWindowDays = 90;

    /// <summary>
    /// Probability that an A-tier or above amateur (Fargo ≥ 620) plays
    /// a given game in Efren mode. Pros are 100% Efren regardless of
    /// this constant. Below A-tier the constant doesn't apply — those
    /// players never play Efren in seeded data (it's too hard).
    /// </summary>
    private const double EfrenAdoptionRateForStrongAmateurs = 0.35;

    /// <summary>
    /// Per-frame score distribution for one Fargo bracket. The fields
    /// are CDF probabilities — they must sum to 1.0 (within rounding).
    /// </summary>
    private readonly record struct FrameDistribution(
        double P0, double P1To3, double P4To7, double P8To10, double P11);

    /// <summary>
    /// Anchor distributions per Fargo bracket midpoint. The generator
    /// picks the closest by Fargo when scoring a frame.
    /// </summary>
    private static readonly (int FargoAnchor, FrameDistribution Dist)[] FrameDistByFargo =
    [
        // Rec (~310) — heavy zero; rare runs.
        (310, new(P0: 0.50, P1To3: 0.40, P4To7: 0.08, P8To10: 0.02, P11: 0.00)),
        // Developing C (~400) — better contact rate, no real position game.
        (400, new(P0: 0.30, P1To3: 0.45, P4To7: 0.20, P8To10: 0.05, P11: 0.00)),
        // Mid Amateur B (~500) — George's anchor.
        (500, new(P0: 0.12, P1To3: 0.34, P4To7: 0.36, P8To10: 0.15, P11: 0.03)),
        // Strong Amateur B+ (~585) — runs come together more often.
        (585, new(P0: 0.06, P1To3: 0.20, P4To7: 0.40, P8To10: 0.28, P11: 0.06)),
        // Advanced A- (~660) — most frames are 4+, occasional perfect.
        (660, new(P0: 0.03, P1To3: 0.12, P4To7: 0.35, P8To10: 0.42, P11: 0.08)),
        // Strong A+ (~725) — controlled position; misses are clusters.
        (725, new(P0: 0.02, P1To3: 0.05, P4To7: 0.22, P8To10: 0.58, P11: 0.13)),
        // Elite Amateur (~775) — near-pro efficiency.
        (775, new(P0: 0.01, P1To3: 0.02, P4To7: 0.10, P8To10: 0.65, P11: 0.22)),
        // Touring Pro (~840).
        (840, new(P0: 0.005, P1To3: 0.005, P4To7: 0.04, P8To10: 0.50, P11: 0.45)),
    ];

    private static FrameDistribution DistributionFor(int fargo)
    {
        var closest = FrameDistByFargo[0];
        var bestDelta = Math.Abs(fargo - closest.FargoAnchor);
        for (int i = 1; i < FrameDistByFargo.Length; i++)
        {
            var delta = Math.Abs(fargo - FrameDistByFargo[i].FargoAnchor);
            if (delta < bestDelta)
            {
                closest = FrameDistByFargo[i];
                bestDelta = delta;
            }
        }
        return closest.Dist;
    }

    /// <summary>
    /// Per-frame Efren penalty fraction. Higher Fargo = bigger penalty,
    /// per George's modeling — pros lose more from BIH removal because
    /// their position game is what BIH lets them run. C players lose
    /// almost nothing because they weren't running out anyway.
    /// </summary>
    private static double EfrenPerFramePenalty(int fargo) => fargo switch
    {
        >= 800 => 0.13,   // Touring pro
        >= 750 => 0.12,   // Elite amateur / fading pro
        >= 700 => 0.11,
        >= 620 => 0.10,
        >= 550 => 0.10,
        >= 450 => 0.08,
        >= 350 => 0.05,
        _      => 0.03,
    };

    /// <summary>
    /// Generates a single frame score in [0, 11] given the player's
    /// Fargo and whether the game is Efren-variant. Uses the bracket's
    /// anchor distribution for the standard draw, then for Efren mode
    /// multiplies the score by (1 - bracket_penalty) and rounds
    /// stochastically — preserves expected value exactly so the
    /// Efren per-game mean matches George's modeling
    /// (e.g., B-bracket 38 standard → 35 Efren ≈ 8% drop).
    /// </summary>
    private static int GenerateFrameScore(int fargo, Random rng, bool efren)
    {
        var dist = DistributionFor(fargo);
        var u = rng.NextDouble();
        int score;
        if (u < dist.P0) score = 0;
        else if (u < dist.P0 + dist.P1To3) score = rng.Next(1, 4);          // 1, 2, 3
        else if (u < dist.P0 + dist.P1To3 + dist.P4To7) score = rng.Next(4, 8);  // 4, 5, 6, 7
        else if (u < dist.P0 + dist.P1To3 + dist.P4To7 + dist.P8To10) score = rng.Next(8, 11); // 8, 9, 10
        else score = 11;

        if (efren && score > 0)
        {
            var penalty = EfrenPerFramePenalty(fargo);
            double penalized = score * (1.0 - penalty);
            int floor = (int)Math.Floor(penalized);
            // Stochastic rounding: bump up by 1 with probability equal to
            // the residual fractional part, so E[rounded] == penalized exactly.
            score = (rng.NextDouble() < (penalized - floor)) ? floor + 1 : floor;
        }

        return Math.Clamp(score, 0, 11);
    }

    /// <summary>
    /// Builds 9 frame scores for one game using the bracket distribution.
    /// Exposed <c>internal</c> so the seed-data tests can verify that
    /// generated per-game scores fall within the expected per-bracket
    /// mean/std-dev bands George specified.
    /// </summary>
    internal static int[] GenerateGameFrameScores(int fargo, Random rng, bool efren)
    {
        var frames = new int[9];
        for (int i = 0; i < 9; i++)
            frames[i] = GenerateFrameScore(fargo, rng, efren);
        return frames;
    }

    /// <summary>
    /// Reconcile pass for mock-player game histories. For each mock
    /// player template that is missing game records, generates
    /// <see cref="GamesPerMockAmateur"/> (or <see cref="GamesPerMockPro"/>
    /// for pros) games with realistic scores for the player's Fargo
    /// bracket and persists them via <see cref="IGameRepository"/>.
    /// Idempotent — players who already have any games are skipped.
    /// </summary>
    private async Task<int> ReconcileMockGameHistoriesAsync(CancellationToken ct)
    {
        // Resolve the venue list once. Pick a venue per game: private
        // home table for low-Fargo amateurs, real venues for higher.
        var venues = (await venueRepository.GetAllAsync(includePrivate: true, ct)).ToList();
        if (venues.Count == 0)
        {
            logger.LogWarning("Cannot seed mock game histories — no venues found.");
            return 0;
        }
        var publicVenues = venues.Where(v => !v.Private).ToList();
        var privateVenues = venues.Where(v => v.Private).ToList();

        // Deterministic-ish RNG per player so re-runs produce stable
        // (but bracket-realistic) data. Seeding off the DisplayName
        // means editing a template's name resets that player's games.
        int totalGamesAdded = 0;
        foreach (var template in AllMockPlayerTemplates)
        {
            var player = await playerRepository.GetByDisplayNameAsync(template.DisplayName, ct);
            if (player is null) continue;

            // Skip if this player already has any games — idempotent guard
            // so re-runs don't pile up duplicates.
            var existing = await gameRepository.GetByPlayerAsync(
                player.PlayerId, skip: 0, limit: 1, ct);
            if (existing.Count > 0) continue;

            var rng = new Random(template.DisplayName.GetHashCode());
            int gameCount = template.EfrenOnly ? GamesPerMockPro : GamesPerMockAmateur;

            for (int g = 0; g < gameCount; g++)
            {
                // Decide variant: pros are always Efren; strong amateurs
                // mix it in occasionally; weak amateurs never.
                bool efren =
                    template.EfrenOnly ||
                    (template.FargoRating >= 620 &&
                     rng.NextDouble() < EfrenAdoptionRateForStrongAmateurs);

                // Venue selection: lower brackets play more at home, higher
                // at real venues (tournament/league play).
                Venue venue;
                if (template.FargoRating < 450 && privateVenues.Count > 0
                    && rng.NextDouble() < 0.4)
                {
                    venue = privateVenues[rng.Next(privateVenues.Count)];
                }
                else
                {
                    venue = publicVenues.Count > 0
                        ? publicVenues[rng.Next(publicVenues.Count)]
                        : venues[rng.Next(venues.Count)];
                }

                var scores = GenerateGameFrameScores(template.FargoRating, rng, efren);
                var daysAgo = rng.Next(0, GameHistoryWindowDays);
                var hoursOffset = rng.Next(0, 24 * 60);    // minute precision

                var game = BuildSeededHistoryGame(
                    player, venue, scores, daysAgo, hoursOffset, efren);
                await gameRepository.CreateAsync(game, ct);
                totalGamesAdded++;
            }

            logger.LogInformation(
                "Seeded {Count} game(s) for mock player {DisplayName} (Fargo {Fargo}{Efren})",
                gameCount, template.DisplayName, template.FargoRating,
                template.EfrenOnly ? ", Efren-only" : "");
        }

        return totalGamesAdded;
    }

    private static Game BuildSeededHistoryGame(
        Player player, Venue venue, int[] frameScores,
        int daysAgo, int minutesOffset, bool isEfrenVariant)
    {
        if (frameScores.Length != 9)
            throw new ArgumentException("Need exactly 9 frame scores.", nameof(frameScores));

        var when = DateTime.UtcNow.AddDays(-daysAgo).AddMinutes(-minutesOffset);
        var game = new Game
        {
            PlayerId = player.PlayerId,
            VenueId = venue.VenueId,
            TableSize = venue.Private ? TableSize.SevenFoot : TableSize.NineFoot,
            WhenPlayed = when,
            IsEfrenVariant = isEfrenVariant,
        };
        game.InitializeFrames();

        foreach (var frameScore in frameScores)
        {
            var (breakBonus, ballCount) = SplitFrameScore(frameScore);
            game.CompleteCurrentFrame(breakBonus, ballCount);
        }

        // CompleteCurrentFrame finalizes on frame 9 — back-date CompletedAt
        // to the same WhenPlayed + a plausible game duration (35–55 min).
        game.CompletedAt = when.AddMinutes(35 + (frameScores.Sum() % 20));
        return game;
    }
}
