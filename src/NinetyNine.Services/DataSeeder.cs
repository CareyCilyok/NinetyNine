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

    /// <summary>
    /// Canonical list of seeded venues. Real Huntsville-area venues that
    /// Carey, George, and friends play at, plus a private home-table entry
    /// and a handful of nearby venues within about a 100-mile radius of
    /// Huntsville, AL. The venue reconcile pass inserts any definition
    /// from this list whose Name is missing from the database, so adding
    /// a new venue here is a one-line change that takes effect on the
    /// next app restart.
    /// </summary>
    private static readonly (string Name, string Address, string PhoneNumber, bool Private)[] SeededVenueDefinitions =
    [
        // Private home table — shared across seeded test players so their
        // seeded games have a plausible private setting. Real address is
        // deliberately omitted from dev data.
        ("Carey's Home Table", "Huntsville, AL", "", true),

        // ── Huntsville, AL — primary play venues ──────────────────────
        ("Bumpers Billiards of Huntsville",
         "4925 University Dr NW, Huntsville, AL 35816", "256-721-1495", false),

        // "Steve's Lounge" in the user's ask is the colloquial name;
        // Yelp/Facebook list it as "Steve's Cue and Grill" (formerly
        // "Steve's Billiards & Lounge").
        ("Steve's Cue and Grill",
         "2322 Memorial Pkwy SW, Huntsville, AL 35801", "256-539-8919", false),

        ("Chips & Salsa Sports Bar & Grill",
         "10300 Bailey Cove Rd SE Ste 10, Huntsville, AL 35803", "256-880-1202", false),

        ("Good Timez Billiards",
         "6241 University Dr NW Ste D, Huntsville, AL 35806", "", false),

        // ── Athens, AL (~20 mi from Huntsville) ───────────────────────
        ("Limestone Legends Billiards",
         "111 S Marion St, Athens, AL 35611", "256-258-9244", false),

        // ── Decatur, AL (~25 mi) ──────────────────────────────────────
        ("6 Pockets Billiards",
         "1819 Bassett Ave SE, Decatur, AL 35601", "256-686-3171", false),

        // ── Scottsboro, AL (~40 mi) ───────────────────────────────────
        ("Ron's City Billiards",
         "120 N Broad St, Scottsboro, AL 35768", "", false),

        // ── Florence, AL (~65 mi) ─────────────────────────────────────
        ("Tennessee Street Billiards & Grill",
         "118 E Tennessee St, Florence, AL 35630", "", false),

        // ── Birmingham, AL (~100 mi) ──────────────────────────────────
        // Iron City Billiards is the largest pool hall in Birmingham;
        // notable APA host venue.
        ("Iron City Billiards",
         "800 Gadsden Hwy, Birmingham, AL 35235", "", false),
        ("All In One Billiards",
         "4841 Avenue R, Birmingham, AL 35208", "", false),
        ("The Break",
         "1001 20th St S, Birmingham, AL 35205", "", false),

        // ── Nashville, TN (~110 mi) ───────────────────────────────────
        // Melrose Billiard Parlor: iconic Nashville pool hall, est. 1944.
        ("Melrose Billiard Parlor",
         "2600 8th Ave S Ste 108, Nashville, TN 37204", "", false),

        // ── Montgomery, AL (~200 mi) ──────────────────────────────────
        // 8 Ball Billiards: 20+ Diamond tables, across from Eastdale Mall.
        ("8 Ball Billiards",
         "163 Eastern Blvd, Montgomery, AL 36117", "334-649-1104", false),
        ("The Breakroom",
         "465 Eastern Blvd, Montgomery, AL 36117", "", false),

        // ── Atlanta, GA (~200 mi) ─────────────────────────────────────
        ("MrCues II Billiards",
         "3541 Chamblee-Tucker Rd, Atlanta, GA 30341", "", false),
        ("Yer Mom",
         "931 Monroe Dr NE Ste 205, Atlanta, GA 30308", "", false),
    ];

    public async Task SeedAsync(CancellationToken ct = default)
    {
        // Profile visibility heal: migrate any player with legacy bool
        // visibility flags into the new Audience enum fields. Runs for
        // every player (not just seeded test players) and is idempotent:
        // once SchemaVersion reaches 2 the player is skipped.
        // See docs/plans/friends-communities-v1.md Sprint 0 S0.5.
        var visibilityHealed = await HealProfileVisibilityAsync(ct);

        // Heal pass: existing test players whose password hash is empty get
        // their hash populated in place. This handles the upgrade case where
        // players were seeded before the password-hashing field existed
        // (their PasswordHash is still "").
        var healed = await HealExistingTestPlayersAsync(ct);

        // Venue reconcile pass: insert any canonical seeded venue whose
        // Name is missing from the database. Runs on every startup and is
        // idempotent — existing venues (even with stale addresses from
        // earlier seed generations) are left alone. This is how new
        // venues added to SeededVenueDefinitions land in an already-
        // populated dev database without a full reseed.
        var addedVenues = await ReconcileSeededVenuesAsync(ct);

        // Idempotent creation: if the primary test player already exists,
        // we've already seeded at some point — skip the rest of the seed.
        var existing = await playerRepository.GetByDisplayNameAsync(
            IDataSeeder.TestPlayerDisplayNames[0], ct);
        if (existing is not null)
        {
            var parts = new List<string>();
            if (visibilityHealed > 0) parts.Add($"migrated {visibilityHealed} player(s) to Audience enum");
            if (healed > 0) parts.Add($"healed {healed} test player(s)");
            if (addedVenues > 0) parts.Add($"added {addedVenues} venue(s)");
            if (parts.Count > 0)
                logger.LogInformation("Seed skipped — test players already exist. {Parts}.",
                    string.Join(", ", parts));
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

        // ── Load the venues we just reconciled so seeded games can
        //    reference them by name. ─────────────────────────────────────
        var venuesByName = (await venueRepository.GetAllAsync(includePrivate: true, ct))
            .ToDictionary(v => v.Name, StringComparer.OrdinalIgnoreCase);
        var home = venuesByName["Carey's Home Table"];
        var bumpers = venuesByName["Bumpers Billiards of Huntsville"];
        var steves = venuesByName["Steve's Cue and Grill"];
        var chips = venuesByName["Chips & Salsa Sports Bar & Grill"];

        // ── Completed games (realistic scatter across real venues) ─────
        // Each int[9] is the frame score per frame (0–11). The seeder splits
        // each into BreakBonus + BallCount by giving break bonus when the
        // frame score is ≥ 3 (roughly matches the break-bonus awarded rate).
        var completedGames = new (Player player, Venue venue, int[] scores, int daysAgo)[]
        {
            (carey,  bumpers, [6, 9, 4, 11, 7, 5, 8, 10, 6],  3),
            (carey,  home,    [5, 7, 11, 3, 9, 6, 8, 4, 10],  7),
            (george, steves,  [4, 8, 6, 7, 5, 9, 3, 11, 7],   3),
            (george, chips,   [7, 5, 10, 4, 6, 8, 5, 7, 9],  12),
            (careyB, bumpers, [3, 6, 8, 5, 7, 4, 9, 6, 5],    3),
            (careyB, home,    [8, 10, 6, 9, 7, 5, 11, 8, 4], 18),
        };

        foreach (var (player, venue, scores, daysAgo) in completedGames)
        {
            var game = BuildCompletedGame(player, venue, scores, daysAgo);
            await gameRepository.CreateAsync(game, ct);
        }

        // ── One in-progress game for Carey (frames 1-3 complete) ────────────
        var inProgress = BuildInProgressGame(
            carey, bumpers, completedScores: [7, 4, 9], hoursAgo: 1);
        await gameRepository.CreateAsync(inProgress, ct);

        logger.LogInformation(
            "Seed complete: 3 players, {VenueCount} venues, 6 completed games, 1 in-progress game.",
            SeededVenueDefinitions.Length);
    }

    /// <summary>
    /// Inserts any canonical seeded venue whose Name is missing from the
    /// database. Idempotent — venues that already exist are left unchanged
    /// even if their address or phone differs from the canonical definition
    /// (so user edits in the UI are preserved). Returns the number of
    /// venues added.
    /// </summary>
    private async Task<int> ReconcileSeededVenuesAsync(CancellationToken ct)
    {
        var existingNames = (await venueRepository.GetAllAsync(includePrivate: true, ct))
            .Select(v => v.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        int added = 0;
        foreach (var (name, address, phone, isPrivate) in SeededVenueDefinitions)
        {
            if (existingNames.Contains(name)) continue;

            var venue = new Venue
            {
                Name = name,
                Address = address,
                PhoneNumber = phone,
                Private = isPrivate,
            };
            await venueRepository.CreateAsync(venue, ct);
            added++;
            logger.LogInformation("Reconciled seeded venue: {Name}", name);
        }
        return added;
    }

    /// <summary>
    /// Migrates every player from schema version 1 (bool visibility flags)
    /// to schema version 2 (Audience enum). Idempotent: skips any player
    /// already at <see cref="Player.SchemaVersion"/> &gt;= 2.
    /// <para>
    /// Migration map (locked by fork D in docs/plans/friends-communities-v1.md):
    /// </para>
    /// <list type="bullet">
    /// <item>EmailAddress bool → <see cref="Audience.Friends"/> when true, <see cref="Audience.Private"/> when false</item>
    /// <item>PhoneNumber bool → Friends when true, Private when false</item>
    /// <item>RealName bool → Friends when true, Private when false</item>
    /// <item>Avatar bool → <b>Public</b> when true, Private when false (the
    /// one documented exception; preserves existing avatar-visible behavior)</item>
    /// </list>
    /// <para>
    /// The <c>true → Friends</c> map for Email/Phone/RealName is strictly
    /// tighter than the old bool semantics ("visible to everyone"),
    /// implementing the security auditor's "no silent widening" rule.
    /// Migrated players get <see cref="Player.MigrationBannerDismissed"/>
    /// set to <c>false</c> so the Edit Profile page can show a one-time
    /// notice in Sprint 3.
    /// </para>
    /// </summary>
    private async Task<int> HealProfileVisibilityAsync(CancellationToken ct)
    {
        var players = await playerRepository.ListAllAsync(ct);
        int migrated = 0;

        foreach (var player in players)
        {
            if (player.SchemaVersion >= 2) continue;

            // Map legacy bool flags to Audience enum per fork D.
            player.Visibility.EmailAudience = player.Visibility.EmailAddress
                ? Audience.Friends
                : Audience.Private;
            player.Visibility.PhoneAudience = player.Visibility.PhoneNumber
                ? Audience.Friends
                : Audience.Private;
            player.Visibility.RealNameAudience = player.Visibility.RealName
                ? Audience.Friends
                : Audience.Private;
            player.Visibility.AvatarAudience = player.Visibility.Avatar
                ? Audience.Public    // Exception: preserves default-visible avatars.
                : Audience.Private;

            player.SchemaVersion = 2;
            player.MigrationBannerDismissed = false;

            await playerRepository.UpdateAsync(player, ct);
            migrated++;

            logger.LogInformation(
                "Migrated visibility for player {DisplayName}: " +
                "email={Email}, phone={Phone}, realName={RealName}, avatar={Avatar}",
                player.DisplayName,
                player.Visibility.EmailAudience,
                player.Visibility.PhoneAudience,
                player.Visibility.RealNameAudience,
                player.Visibility.AvatarAudience);
        }

        if (migrated > 0)
            logger.LogInformation("Profile visibility heal: migrated {Count} player(s) to SchemaVersion 2.", migrated);

        return migrated;
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

            // DEF-008: earlier seeds set Visibility.RealName = true for all
            // three test players. The new default is false (the safe default
            // for the upcoming Friends/Communities audience model). Reset
            // only the seeded test players — never touch real user accounts.
            var needsRealNameReset = player.Visibility.RealName;

            if (!needsEmail && !needsHash && !needsRealNameReset) continue;

            if (needsEmail)
                player.EmailAddress = $"{displayName}@example.local";

            if (needsHash)
                player.PasswordHash = passwordHasher.HashPassword(player, DevPassword);

            if (needsRealNameReset)
                player.Visibility.RealName = false;

            player.EmailVerified = true;
            await playerRepository.UpdateAsync(player, ct);
            healed++;
            logger.LogInformation(
                "Healed seeded test player {DisplayName}: {Fields}",
                displayName,
                (needsEmail ? "email " : "")
                  + (needsHash ? "passwordHash " : "")
                  + (needsRealNameReset ? "visibility.realName" : ""));
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
                // RealName defaults to false (the ProfileVisibility default)
                // because the upcoming Friends/Communities features will add
                // finer-grained audience controls and the safe default for
                // every new sharing dimension is the most-private option.
                // See docs/defects.md DEF-008.
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
