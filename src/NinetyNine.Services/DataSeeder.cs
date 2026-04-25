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
public sealed partial class DataSeeder(
    IPlayerRepository playerRepository,
    IVenueRepository venueRepository,
    IGameRepository gameRepository,
    IFriendshipRepository friendshipRepository,
    IFriendRequestRepository friendRequestRepository,
    ICommunityRepository communityRepository,
    ICommunityMemberRepository communityMemberRepository,
    ICommunityInvitationRepository communityInvitationRepository,
    ICommunityJoinRequestRepository communityJoinRequestRepository,
    IOwnershipTransferRepository ownershipTransferRepository,
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

    /// <summary>
    /// Current canonical schema version for seeded test players. Bump
    /// this whenever the template changes so the reconcile pass detects
    /// stale records. History: 1 = pre-Sprint-0, 2 = Sprint 0 (Audience
    /// enum), 3 = Sprint 6 (reconcile rewrite, legacy bool removal).
    /// </summary>
    private const int CurrentPlayerSchemaVersion = 3;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        // ── Reconcile passes (run every startup, before seed guard) ──

        // 1. Player reconcile: converge every seeded test player to the
        //    current template. SchemaVersion comparison for cheap change
        //    detection. Subsumes the former HealExistingTestPlayersAsync
        //    and HealProfileVisibilityAsync passes.
        var playersReconciled = await ReconcileSeededPlayersAsync(ct);

        // 2. Venue reconcile
        var addedVenues = await ReconcileSeededVenuesAsync(ct);

        // 3. Friendship reconcile
        var addedFriendships = await ReconcileSeededFriendshipsAsync(ct);

        // 4. Community reconcile
        var communityChange = await ReconcileSeededCommunityAsync(ct);

        // 5. Mock roster reconcile (26 amateurs + 7 pros, see DataSeeder.MockRoster.cs).
        //    Runs *after* the original test players + community so the
        //    original seed flow is untouched on a fresh DB.
        var mockPlayersAdded = await ReconcileMockPlayerRosterAsync(ct);
        var (mockCommunitiesCreated, mockCommunityMembersAdded) =
            await ReconcileMockCommunitiesAsync(ct);

        // 6. Expiration sweep
        var expired = await SweepExpiredPendingAsync(ct);

        // ── Seed guard: if players already exist, log reconcile
        //    summary and return. ─────────────────────────────────────
        var existing = await playerRepository.GetByDisplayNameAsync(
            IDataSeeder.TestPlayerDisplayNames[0], ct);
        if (existing is not null)
        {
            var parts = new List<string>();
            if (playersReconciled > 0) parts.Add($"reconciled {playersReconciled} player(s)");
            if (addedVenues > 0) parts.Add($"added {addedVenues} venue(s)");
            if (addedFriendships > 0) parts.Add($"seeded {addedFriendships} friendship(s)");
            if (communityChange.CommunityCreated) parts.Add("created Pocket Sports community");
            if (communityChange.MembersAdded > 0) parts.Add($"added {communityChange.MembersAdded} community member(s)");
            if (communityChange.VenuesAffiliated > 0) parts.Add($"affiliated {communityChange.VenuesAffiliated} venue(s)");
            if (mockPlayersAdded > 0) parts.Add($"added {mockPlayersAdded} mock player(s)");
            if (mockCommunitiesCreated > 0) parts.Add($"created {mockCommunitiesCreated} mock community/ies");
            if (mockCommunityMembersAdded > 0) parts.Add($"added {mockCommunityMembersAdded} mock-community member(s)");
            var totalExpired = expired.FriendRequests + expired.Invitations + expired.JoinRequests + expired.Transfers;
            if (totalExpired > 0) parts.Add($"expired {totalExpired} stale pending item(s)");
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
        // the second Carey is seeded as "carey_b". Fargo ratings are placeholder
        // estimates — these are real people whose FargoRate numbers we don't
        // actually have, so the values land in plausible league-amateur brackets.
        var carey = CreateTestPlayer("carey", "Carey", "Cilyok", fargoRating: 550);
        var george = CreateTestPlayer("george", "George", null, fargoRating: 625);
        var careyB = CreateTestPlayer("carey_b", "Carey", null, fargoRating: 480);

        await playerRepository.CreateAsync(carey, ct);
        await playerRepository.CreateAsync(george, ct);
        await playerRepository.CreateAsync(careyB, ct);

        // ── Seed pre-befriended mutual friendships between the three
        //    test players so /friends has real data on first run.
        //    (The reconcile pass above ran before the players existed, so
        //    it short-circuited; call it again now that the players exist.)
        await ReconcileSeededFriendshipsAsync(ct);

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
    /// Inserts canonical mutual friendships between every pair of seeded
    /// test players so Sprint 1's <c>/friends</c> page shows real data
    /// on first visit. Idempotent via the unique index on
    /// <c>Friendship.PlayerIdsKey</c>: duplicate inserts are caught and
    /// silently ignored. Returns the number of friendships actually
    /// added (zero on steady-state runs).
    /// </summary>
    private async Task<int> ReconcileSeededFriendshipsAsync(CancellationToken ct)
    {
        // Resolve the three seeded display names to PlayerIds. If any of
        // them does not exist yet (fresh DB before the main seed runs),
        // skip — the main seed branch will create them and the pass will
        // succeed on the next startup.
        var players = new List<Player>();
        foreach (var displayName in IDataSeeder.TestPlayerDisplayNames)
        {
            var p = await playerRepository.GetByDisplayNameAsync(displayName, ct);
            if (p is null) return 0;
            players.Add(p);
        }

        int added = 0;
        for (int i = 0; i < players.Count; i++)
        {
            for (int j = i + 1; j < players.Count; j++)
            {
                var a = players[i];
                var b = players[j];

                if (await friendshipRepository.GetByPairAsync(a.PlayerId, b.PlayerId, ct) is not null)
                    continue;

                var friendship = Friendship.Create(
                    a.PlayerId, b.PlayerId, via: "seeder");

                try
                {
                    await friendshipRepository.CreateAsync(friendship, ct);
                    added++;
                    logger.LogInformation(
                        "Seeded friendship: {A} <-> {B}", a.DisplayName, b.DisplayName);
                }
                catch (MongoDB.Driver.MongoWriteException ex)
                    when (ex.WriteError?.Category == MongoDB.Driver.ServerErrorCategory.DuplicateKey)
                {
                    // Raced another reconcile run; benign.
                }
            }
        }

        return added;
    }

    /// <summary>
    /// Seeded canonical community name. Lives in a single place so tests
    /// and future heal passes can reference it by constant.
    /// </summary>
    public const string SeededCommunityName = "Pocket Sports";
    private const string SeededCommunitySlug = "pocket-sports";

    /// <summary>
    /// Tracks which reconcile actions ran, for the "Seed skipped" log line.
    /// </summary>
    private record struct CommunityReconcileChange(
        bool CommunityCreated,
        int MembersAdded,
        int VenuesAffiliated);

    /// <summary>
    /// Ensures the canonical seeded community exists with every seeded
    /// test player as a member (the first test player is Owner, the rest
    /// are Members) and every seeded venue affiliated with it.
    /// Idempotent via the unique name index on communities + the
    /// (player, community) unique index on memberships.
    /// </summary>
    private async Task<CommunityReconcileChange> ReconcileSeededCommunityAsync(CancellationToken ct)
    {
        // Need all seeded test players to exist first — on a fresh DB
        // this pass runs before player creation, so short-circuit until
        // the next restart.
        var seededPlayers = new List<Player>();
        foreach (var displayName in IDataSeeder.TestPlayerDisplayNames)
        {
            var p = await playerRepository.GetByDisplayNameAsync(displayName, ct);
            if (p is null) return default;
            seededPlayers.Add(p);
        }

        var ownerPlayer = seededPlayers[0];

        var community = await communityRepository.GetByNameAsync(SeededCommunityName, ct);
        bool created = false;
        if (community is null)
        {
            community = new Community
            {
                Name = SeededCommunityName,
                Slug = SeededCommunitySlug,
                Description = "The seeded community — every test pool player is a member, every seeded venue is affiliated.",
                Visibility = CommunityVisibility.Public,
                OwnerPlayerId = ownerPlayer.PlayerId,
                CreatedByPlayerId = ownerPlayer.PlayerId,
                CreatedAt = DateTime.UtcNow,
                SchemaVersion = 2,
            };
            try
            {
                await communityRepository.CreateAsync(community, ct);
                created = true;
                logger.LogInformation(
                    "Seeded community '{Name}' (owner {Owner})",
                    SeededCommunityName, ownerPlayer.DisplayName);
            }
            catch (MongoDB.Driver.MongoWriteException ex)
                when (ex.WriteError?.Category == MongoDB.Driver.ServerErrorCategory.DuplicateKey)
            {
                // Raced another reconcile run. Fetch the live doc.
                var racedDoc = await communityRepository.GetByNameAsync(SeededCommunityName, ct);
                if (racedDoc is null) throw;
                community = racedDoc;
            }
        }

        // Memberships: owner first (Owner role), then the rest as Members.
        int membersAdded = 0;
        for (int i = 0; i < seededPlayers.Count; i++)
        {
            var player = seededPlayers[i];
            var role = i == 0 ? CommunityRole.Owner : CommunityRole.Member;

            var existing = await communityMemberRepository.GetMembershipAsync(
                community.CommunityId, player.PlayerId, ct);
            if (existing is not null) continue;

            try
            {
                await communityMemberRepository.AddAsync(new CommunityMembership
                {
                    CommunityId = community.CommunityId,
                    PlayerId = player.PlayerId,
                    Role = role,
                    JoinedAt = DateTime.UtcNow,
                }, ct);
                membersAdded++;
                logger.LogInformation(
                    "Seeded community member: {Player} → {Community} ({Role})",
                    player.DisplayName, SeededCommunityName, role);
            }
            catch (MongoDB.Driver.MongoWriteException ex)
                when (ex.WriteError?.Category == MongoDB.Driver.ServerErrorCategory.DuplicateKey)
            {
                // Raced another reconcile run — benign.
            }
        }

        // Venues: affiliate every venue whose CommunityId is still null.
        // Never touch venues that already point at another community —
        // the user may have manually affiliated something.
        int venuesAffiliated = 0;
        var allVenues = await venueRepository.GetAllAsync(includePrivate: true, ct);
        foreach (var venue in allVenues.Where(v => v.CommunityId is null))
        {
            venue.CommunityId = community.CommunityId;
            await venueRepository.UpdateAsync(venue, ct);
            venuesAffiliated++;
            logger.LogInformation(
                "Seeded community venue affiliation: {Venue} → {Community}",
                venue.Name, SeededCommunityName);
        }

        return new CommunityReconcileChange(created, membersAdded, venuesAffiliated);
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
    /// Reconciles every seeded test player to the current canonical
    /// template. Uses <see cref="CurrentPlayerSchemaVersion"/> for cheap
    /// change detection — if the stored version matches, no writes.
    /// Subsumes the former <c>HealExistingTestPlayersAsync</c> and the
    /// seeded-player portion of <c>HealProfileVisibilityAsync</c>.
    /// Returns the number of players that were updated.
    /// <para>See <c>docs/plans/v2-roadmap.md</c> Sprint 6 S6.1.</para>
    /// </summary>
    private async Task<int> ReconcileSeededPlayersAsync(CancellationToken ct)
    {
        var templates = new (string DisplayName, string FirstName, string? LastName)[]
        {
            ("carey", "Carey", "Cilyok"),
            ("george", "George", null),
            ("carey_b", "Carey", null),
        };

        int reconciled = 0;
        foreach (var (displayName, firstName, lastName) in templates)
        {
            var player = await playerRepository.GetByDisplayNameAsync(displayName, ct);
            if (player is null) continue; // Not yet created — first-run seed will handle it.

            if (player.SchemaVersion >= CurrentPlayerSchemaVersion) continue;

            // Converge all template fields. Immutable fields (PlayerId,
            // CreatedAt) are never touched.
            player.EmailAddress = $"{displayName}@example.local";
            player.EmailVerified = true;
            player.FirstName = firstName;
            player.LastName = lastName;

            if (string.IsNullOrEmpty(player.PasswordHash))
                player.PasswordHash = passwordHasher.HashPassword(player, DevPassword);

            // Visibility: converge to the canonical template defaults.
            player.Visibility.EmailAudience = Audience.Private;
            player.Visibility.PhoneAudience = Audience.Private;
            player.Visibility.RealNameAudience = Audience.Private;
            player.Visibility.AvatarAudience = Audience.Public;

            player.SchemaVersion = CurrentPlayerSchemaVersion;

            await playerRepository.UpdateAsync(player, ct);
            reconciled++;

            logger.LogInformation(
                "Reconciled seeded player {DisplayName} to SchemaVersion {Version}",
                displayName, CurrentPlayerSchemaVersion);
        }

        return reconciled;
    }

    // HealNonSeededPlayerVisibilityAsync removed in Sprint 6 S6.2.
    // The legacy bool→Audience migration was only relevant for pre-Sprint-0
    // players; with the bool flags removed from ProfileVisibility, the
    // migration can no longer run. Non-seeded players at SchemaVersion < 2
    // would need a mongosh-level fix if they ever exist (dev-only scenario).

    private Player CreateTestPlayer(
        string displayName, string firstName, string? lastName, int? fargoRating = null)
    {
        var player = new Player
        {
            PlayerId = Guid.NewGuid(),
            DisplayName = displayName,
            EmailAddress = $"{displayName}@example.local",
            EmailVerified = true,
            FirstName = firstName,
            LastName = lastName,
            FargoRating = fargoRating,
            Visibility = new ProfileVisibility
            {
                EmailAudience = Audience.Private,
                PhoneAudience = Audience.Private,
                RealNameAudience = Audience.Private,
                AvatarAudience = Audience.Public,
            },
            SchemaVersion = CurrentPlayerSchemaVersion,
            CreatedAt = DateTime.UtcNow,
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

    // ── Sprint 4 S4.5: expiration sweep ────────────────────────────────

    private record SweepResult(int FriendRequests, int Invitations, int JoinRequests, int Transfers);

    /// <summary>
    /// Marks stale Pending items as Expired. Runs on every startup;
    /// idempotent since only Pending → Expired transitions happen.
    /// <list type="bullet">
    /// <item>Friend requests > 30 days.</item>
    /// <item>Community invitations > 14 days.</item>
    /// <item>Community join requests > 30 days.</item>
    /// <item>Ownership transfers past <c>ExpiresAt</c>.</item>
    /// </list>
    /// </summary>
    private async Task<SweepResult> SweepExpiredPendingAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        int friendReqs = await ExpireFriendRequestsAsync(now - TimeSpan.FromDays(30), ct);
        int invites = await ExpireInvitationsAsync(now - TimeSpan.FromDays(14), ct);
        int joins = await ExpireJoinRequestsAsync(now - TimeSpan.FromDays(30), ct);
        int xfers = await ExpireTransfersAsync(now, ct);

        if (friendReqs + invites + joins + xfers > 0)
            logger.LogInformation(
                "Expiration sweep: {FriendReqs} friend request(s), {Invites} invitation(s), " +
                "{Joins} join request(s), {Transfers} ownership transfer(s).",
                friendReqs, invites, joins, xfers);

        return new SweepResult(friendReqs, invites, joins, xfers);
    }

    private async Task<int> ExpireFriendRequestsAsync(DateTime cutoff, CancellationToken ct)
    {
        int count = 0;
        // No global "list all pending" exists — iterate seeded test players.
        // Dev-only seeder; production would use a bulk UpdateMany.
        foreach (var displayName in IDataSeeder.TestPlayerDisplayNames)
        {
            var player = await playerRepository.GetByDisplayNameAsync(displayName, ct);
            if (player is null) continue;

            var inbox = await friendRequestRepository.ListIncomingAsync(
                player.PlayerId, FriendRequestStatus.Pending, ct);
            foreach (var req in inbox)
            {
                if (req.CreatedAt < cutoff)
                {
                    await friendRequestRepository.UpdateStatusAsync(
                        req.RequestId, FriendRequestStatus.Expired, DateTime.UtcNow, ct);
                    count++;
                }
            }
        }
        return count;
    }

    private async Task<int> ExpireInvitationsAsync(DateTime cutoff, CancellationToken ct)
    {
        int count = 0;
        foreach (var displayName in IDataSeeder.TestPlayerDisplayNames)
        {
            var player = await playerRepository.GetByDisplayNameAsync(displayName, ct);
            if (player is null) continue;

            var pending = await communityInvitationRepository.ListByInviteeAsync(
                player.PlayerId, CommunityInvitationStatus.Pending, ct);
            foreach (var inv in pending)
            {
                if (inv.CreatedAt < cutoff)
                {
                    await communityInvitationRepository.UpdateStatusAsync(
                        inv.InvitationId, CommunityInvitationStatus.Expired, DateTime.UtcNow, ct);
                    count++;
                }
            }
        }
        return count;
    }

    private async Task<int> ExpireJoinRequestsAsync(DateTime cutoff, CancellationToken ct)
    {
        int count = 0;
        // Iterate communities and check their pending join requests.
        var allCommunities = await communityRepository.SearchPublicByNameAsync("", limit: 1000, ct);
        foreach (var community in allCommunities)
        {
            var pending = await communityJoinRequestRepository.ListPendingByCommunityAsync(community.CommunityId, ct);
            foreach (var req in pending)
            {
                if (req.CreatedAt < cutoff)
                {
                    await communityJoinRequestRepository.UpdateStatusAsync(
                        req.RequestId, CommunityJoinRequestStatus.Expired, DateTime.UtcNow, null, ct);
                    count++;
                }
            }
        }
        return count;
    }

    private async Task<int> ExpireTransfersAsync(DateTime now, CancellationToken ct)
    {
        int count = 0;
        // Check each test player's pending transfers as target.
        foreach (var displayName in IDataSeeder.TestPlayerDisplayNames)
        {
            var player = await playerRepository.GetByDisplayNameAsync(displayName, ct);
            if (player is null) continue;

            var pending = await ownershipTransferRepository.ListPendingForTargetAsync(player.PlayerId, ct);
            foreach (var xfer in pending)
            {
                if (now > xfer.ExpiresAt)
                {
                    xfer.Status = OwnershipTransferStatus.Expired;
                    xfer.RespondedAt = now;
                    await ownershipTransferRepository.UpdateAsync(xfer, ct);
                    count++;
                }
            }
        }
        return count;
    }
}
