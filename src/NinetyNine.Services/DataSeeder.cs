using Microsoft.AspNetCore.Identity;
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
    ILogger<DataSeeder> logger,
    IPasswordHasher<Player> passwordHasher) : IDataSeeder
{
    /// <summary>
    /// Known dev password for all seeded test players. Satisfies all five
    /// PasswordValidator rules: length ≥10, uppercase, lowercase, digit, symbol.
    /// </summary>
    private const string DevPassword = "Test1234!a";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        // Heal pass: existing test players whose password hash is empty get
        // their hash populated in place. This handles the upgrade case where
        // players were seeded before the password-hashing field existed
        // (their PasswordHash is still "").
        var healed = await HealExistingTestPlayersAsync(ct);

        // Idempotent creation: if the primary test player already exists,
        // we've already seeded at some point — skip the rest of the seed.
        var existing = await playerRepository.GetByDisplayNameAsync(
            IDataSeeder.TestPlayerDisplayNames[0], ct);
        if (existing is not null)
        {
            if (healed > 0)
                logger.LogInformation(
                    "Seed skipped — test players already exist. Healed {Count} empty password hash(es).",
                    healed);
            else
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

    /// <summary>
    /// Heals existing test players whose PasswordHash is empty. This covers
    /// the upgrade path where players were seeded before WP-11 added
    /// password-hashing support. Returns the number of players healed.
    /// </summary>
    private async Task<int> HealExistingTestPlayersAsync(CancellationToken ct)
    {
        int healed = 0;
        foreach (var displayName in IDataSeeder.TestPlayerDisplayNames)
        {
            var player = await playerRepository.GetByDisplayNameAsync(displayName, ct);
            if (player is null) continue;

            // Also fix stale seeds where the email address wasn't populated
            // (emailAddress was added later and earlier seeds left it blank).
            var needsEmail = string.IsNullOrEmpty(player.EmailAddress);
            var needsHash = string.IsNullOrEmpty(player.PasswordHash);

            if (!needsEmail && !needsHash) continue;

            if (needsEmail)
                player.EmailAddress = $"{displayName}@example.local";

            if (needsHash)
                player.PasswordHash = passwordHasher.HashPassword(player, DevPassword);

            player.EmailVerified = true;
            await playerRepository.UpdateAsync(player, ct);
            healed++;
            logger.LogInformation(
                "Healed seeded test player {DisplayName}: {Fields}",
                displayName,
                (needsEmail ? "email " : "") + (needsHash ? "passwordHash" : ""));
        }
        return healed;
    }

    private Player CreateTestPlayer(string displayName, string firstName, string? lastName)
    {
        var player = new Player
        {
            PlayerId = Guid.NewGuid(),
            DisplayName = displayName,
            EmailAddress = $"{displayName}@example.local",
            EmailVerified = true,
            FirstName = firstName,
            LastName = lastName,
            Visibility = new ProfileVisibility
            {
                RealName = true,
                Avatar = true
            },
            CreatedAt = DateTime.UtcNow
        };
        player.PasswordHash = passwordHasher.HashPassword(player, DevPassword);
        return player;
    }

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
