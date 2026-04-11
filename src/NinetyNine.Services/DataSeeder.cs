using Microsoft.Extensions.Logging;
using NinetyNine.Model;
using NinetyNine.Repository.Repositories;

namespace NinetyNine.Services;

/// <summary>
/// Dev-mode data seeder. Populates the database with three test players
/// (matching the Ninety-Nine score card photos in <c>docs/</c>), two venues,
/// and a handful of games in various states so the UX can be prototyped
/// against realistic data.
/// </summary>
public sealed class DataSeeder(
    IPlayerRepository playerRepository,
    IVenueRepository venueRepository,
    IGameRepository gameRepository,
    ILogger<DataSeeder> logger) : IDataSeeder
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        // Idempotent: if any of the test players already exists, assume seeded.
        var existing = await playerRepository.GetByDisplayNameAsync(
            IDataSeeder.TestPlayerDisplayNames[0], ct);
        if (existing is not null)
        {
            logger.LogInformation("Seed skipped — test players already exist.");
            return;
        }

        logger.LogInformation("Seeding test data (development mock mode)…");

        // ── Players ──────────────────────────────────────────────────────────
        // Two players on the original score cards share the first name "Carey"
        // (the primary user and a second Carey). DisplayName must be unique so
        // the second Carey is seeded as "carey_b".
        var carey = CreateTestPlayer("carey", "Carey", "Cilyok");
        var george = CreateTestPlayer("george", "George", null);
        var careyB = CreateTestPlayer("carey_b", "Carey", null);

        await playerRepository.CreateAsync(carey, ct);
        await playerRepository.CreateAsync(george, ct);
        await playerRepository.CreateAsync(careyB, ct);

        // ── Venues ───────────────────────────────────────────────────────────
        var home = new Venue
        {
            Name = "Home Table",
            Address = "42 Corner Pocket Ln",
            Private = true
        };
        var hall = new Venue
        {
            Name = "Summerville Billiards",
            Address = "123 Rail Ave, Summerville SC",
            PhoneNumber = "843-555-0199",
            Private = false
        };

        await venueRepository.CreateAsync(home, ct);
        await venueRepository.CreateAsync(hall, ct);

        // ── Completed games (realistic scatter) ─────────────────────────────
        // Each int[9] is the frame score per frame (0–11). The seeder splits
        // each into BreakBonus + BallCount by giving break bonus when the
        // frame score is ≥ 3 (roughly matches the break-bonus awarded rate).
        var completedGames = new (Player player, Venue venue, int[] scores, int daysAgo)[]
        {
            (carey,  hall, [6, 9, 4, 11, 7, 5, 8, 10, 6],  3),
            (carey,  home, [5, 7, 11, 3, 9, 6, 8, 4, 10],  7),
            (george, hall, [4, 8, 6, 7, 5, 9, 3, 11, 7],   3),
            (george, home, [7, 5, 10, 4, 6, 8, 5, 7, 9],  12),
            (careyB, hall, [3, 6, 8, 5, 7, 4, 9, 6, 5],    3),
            (careyB, home, [8, 10, 6, 9, 7, 5, 11, 8, 4], 18)
        };

        foreach (var (player, venue, scores, daysAgo) in completedGames)
        {
            var game = BuildCompletedGame(player, venue, scores, daysAgo);
            await gameRepository.CreateAsync(game, ct);
        }

        // ── One in-progress game for Carey (frames 1-3 complete) ────────────
        var inProgress = BuildInProgressGame(
            carey, hall, completedScores: [7, 4, 9], hoursAgo: 1);
        await gameRepository.CreateAsync(inProgress, ct);

        logger.LogInformation(
            "Seed complete: 3 players, 2 venues, 6 completed games, 1 in-progress game.");
    }

    private static Player CreateTestPlayer(string displayName, string firstName, string? lastName)
        => new()
        {
            PlayerId = Guid.NewGuid(),
            DisplayName = displayName,
            FirstName = firstName,
            LastName = lastName,
            Visibility = new ProfileVisibility
            {
                RealName = true,
                Avatar = true
            },
            LinkedIdentities =
            [
                new LinkedIdentity
                {
                    Provider = IDataSeeder.MockProvider,
                    ProviderUserId = $"mock-{displayName}",
                    LinkedAt = DateTime.UtcNow
                }
            ],
            CreatedAt = DateTime.UtcNow
        };

    private static Game BuildCompletedGame(
        Player player, Venue venue, int[] frameScores, int daysAgo)
    {
        if (frameScores.Length != 9)
            throw new ArgumentException("Need exactly 9 frame scores.", nameof(frameScores));

        var game = new Game
        {
            PlayerId = player.PlayerId,
            VenueId = venue.VenueId,
            TableSize = venue.Private ? TableSize.SevenFoot : TableSize.NineFoot,
            WhenPlayed = DateTime.UtcNow.AddDays(-daysAgo)
        };
        game.InitializeFrames();

        foreach (var frameScore in frameScores)
        {
            var (breakBonus, ballCount) = SplitFrameScore(frameScore);
            game.CompleteCurrentFrame(breakBonus, ballCount);
        }

        // CompleteCurrentFrame auto-finalizes on frame 9 — back-date CompletedAt.
        game.CompletedAt = game.WhenPlayed.AddMinutes(45);
        return game;
    }

    private static Game BuildInProgressGame(
        Player player, Venue venue, int[] completedScores, int hoursAgo)
    {
        var game = new Game
        {
            PlayerId = player.PlayerId,
            VenueId = venue.VenueId,
            TableSize = TableSize.NineFoot,
            WhenPlayed = DateTime.UtcNow.AddHours(-hoursAgo)
        };
        game.InitializeFrames();

        foreach (var frameScore in completedScores)
        {
            var (breakBonus, ballCount) = SplitFrameScore(frameScore);
            game.CompleteCurrentFrame(breakBonus, ballCount);
        }

        return game;
    }

    /// <summary>
    /// Splits a total frame score into a plausible (BreakBonus, BallCount) pair.
    /// Awards the break bonus when score ≥ 3 and fits within the BallCount cap of 10.
    /// </summary>
    private static (int BreakBonus, int BallCount) SplitFrameScore(int total)
    {
        if (total is < 0 or > 11)
            throw new ArgumentOutOfRangeException(nameof(total));

        // Break bonus goes to 1 when the player pocketed anything off the break.
        // For the seeder we grant it whenever the total is at least 3 points,
        // subject to the BallCount ≤ 10 ceiling.
        if (total >= 3 && total <= 11)
            return (1, total - 1);

        return (0, total);
    }
}
