using Microsoft.Extensions.Logging;
using NinetyNine.Model;

namespace NinetyNine.Services;

/// <summary>
/// Mock-data extension for <see cref="DataSeeder"/>: adds 25+ amateur
/// players spread across the Fargo skill spectrum, 7 pros (Efren Reyes
/// + 6 of the current top-rated players in the world), and 5 themed
/// communities tying them together. Game and match histories live in
/// <c>DataSeeder.MockHistory.cs</c>.
///
/// <para>
/// Score modeling and pro list were vetted by the project's pool SME
/// (the <c>poolplayer</c> agent / "George"). See the per-bracket means
/// + std-deviations in <c>DataSeeder.MockHistory.cs</c> when generating
/// games — the rosters here only carry the FargoRating, not the score
/// distributions themselves.
/// </para>
///
/// <para>
/// Loose APA SL → Fargo crosswalk for Huntsville-area league players
/// who recognize APA numbers (8-ball; 9-ball SL ranges differ slightly):
/// </para>
/// <list type="bullet">
///   <item>SL 2 ≈ Fargo 300–350 (rec / brand-new)</item>
///   <item>SL 3 ≈ Fargo 380–420 (developing)</item>
///   <item>SL 4 ≈ Fargo 430–480 (mid amateur, B-ish)</item>
///   <item>SL 5 ≈ Fargo 480–530 (consistent B)</item>
///   <item>SL 6 ≈ Fargo 550–620 (strong amateur, B+)</item>
///   <item>SL 7 ≈ Fargo 630–700+ (advanced A-)</item>
/// </list>
/// </summary>
public sealed partial class DataSeeder
{
    /// <summary>
    /// Template for a seeded mock player. <see cref="EfrenOnly"/> drives
    /// game-history generation in <c>DataSeeder.MockHistory.cs</c> —
    /// <c>true</c> means every game seeded for this player is the Efren
    /// variant (no ball-in-hand after break). All current pros use Efren
    /// only, per the user directive in the v0.6.0 milestone. Some
    /// strong amateurs play it occasionally; see the history generator.
    /// </summary>
    private sealed record MockPlayerTemplate(
        string DisplayName,
        string FirstName,
        string? LastName,
        int FargoRating,
        bool EfrenOnly = false);

    /// <summary>
    /// 26 amateur players spread across the Fargo skill brackets. Names
    /// are fabricated; ratings cluster by bracket so the integration
    /// tests have realistic skill diversity to query against
    /// (leaderboards by bracket, friendship pairings between similarly-
    /// rated players, community membership filters).
    /// </summary>
    private static readonly MockPlayerTemplate[] SeededAmateurMockPlayers =
    [
        // ── Rec / Beginner — Fargo 270–349 (4) ──────────────────────
        new("rookie_brad",   "Brad",    "Henson",   310),
        new("learner_alice", "Alice",   "Park",     325),
        new("newbie_marcus", "Marcus",  "Vaughn",   340),
        new("jules_fresh",   "Jules",   "Mason",    295),

        // ── Developing C — Fargo 350–449 (5) ────────────────────────
        new("danny_c",  "Danny", "Reilly",    380),
        new("c_marie",  "Marie", "Thompson",  410),
        new("c_jason",  "Jason", "Holt",      425),
        new("c_nina",   "Nina",  "Park",      395),
        new("c_ricky",  "Ricky", "Andrews",   440),

        // ── Mid Amateur B — Fargo 450–549 (6) ───────────────────────
        new("b_chris",    "Chris",    "Bauer",     480),
        new("b_kaitlyn",  "Kaitlyn",  "Greer",     510),
        new("b_doug",     "Doug",     "Newman",    530),
        new("b_priya",    "Priya",    "Rao",       495),
        new("b_marco",    "Marco",    "Belluschi", 540),
        new("b_jen",      "Jen",      "Sayers",    470),

        // ── Strong Amateur B+ — Fargo 550–619 (4) ───────────────────
        new("bp_kevin",  "Kevin",  "Davila",   575),
        new("bp_anh",    "Anh",    "Tran",     600),
        new("bp_dale",   "Dale",   "Henson",   580),
        new("bp_sam",    "Sam",    "Whitlock", 615),

        // ── Advanced A- — Fargo 620–699 (4) ─────────────────────────
        new("a_mike",     "Mike",    "Cardenas",   650),
        new("a_sarah",    "Sarah",   "Olstead",    685),
        new("a_jorge",    "Jorge",   "Espinosa",   670),
        new("a_quentin",  "Quentin", "Brooks",     695),

        // ── Strong A+ — Fargo 700–749 (2) ───────────────────────────
        new("ap_riley",  "Riley", "Tanaka",  720),
        new("ap_dom",    "Dom",   "Costa",   745),

        // ── Elite Amateur — Fargo 750–799 (1) ───────────────────────
        new("elite_trent",  "Trent", "Marshall", 775),
    ];

    /// <summary>
    /// 7 current top pros (or recently-top — Efren is on the back nine
    /// of his career). All are seeded with <see cref="MockPlayerTemplate.EfrenOnly"/>
    /// = true so every history game we generate for them is the Efren
    /// variant — that's where their style differentiates from the
    /// run-out-on-BIH amateur game.
    /// <para>
    /// Ratings triangulated from the AzBilliards forum top-200 thread
    /// (April 2026) and the FargoRate top-100 page. Numbers are
    /// midpoints; precision below ±5 isn't meaningful for seed data.
    /// </para>
    /// </summary>
    private static readonly MockPlayerTemplate[] SeededProMockPlayers =
    [
        new("pro_efren",    "Efren",    "Reyes",         775, EfrenOnly: true),
        new("pro_svb",      "Shane",    "Van Boening",   840, EfrenOnly: true),
        new("pro_filler",   "Joshua",   "Filler",        842, EfrenOnly: true),
        new("pro_ko",       "Pin-Yi",   "Ko",            832, EfrenOnly: true),
        new("pro_shaw",     "Jayson",   "Shaw",          831, EfrenOnly: true),
        new("pro_gorst",    "Fedor",    "Gorst",         825, EfrenOnly: true),
        new("pro_hohmann",  "Thorsten", "Hohmann",       814, EfrenOnly: true),
    ];

    /// <summary>
    /// Convenience accessor — every mock player template (amateur + pro)
    /// in seed order. Used by the reconcile passes and the history
    /// generator.
    /// </summary>
    private static IEnumerable<MockPlayerTemplate> AllMockPlayerTemplates =>
        SeededAmateurMockPlayers.Concat(SeededProMockPlayers);

    // ── Communities ─────────────────────────────────────────────────────

    /// <summary>
    /// Template for a seeded mock community. <see cref="MemberDisplayNames"/>
    /// is resolved against the seeded mock-player roster at reconcile time;
    /// any name that doesn't match an existing player is silently skipped
    /// (so the table can be edited freely without hard-breaking startup).
    /// The first entry in the list is the owner.
    /// </summary>
    private sealed record MockCommunityTemplate(
        string Name,
        string Slug,
        string Description,
        CommunityVisibility Visibility,
        string[] MemberDisplayNames);

    /// <summary>
    /// 5 themed communities that tie the new mock-player roster together.
    /// Each picks members from the appropriate Fargo brackets so the
    /// integration tests have plausible community-vs-community membership
    /// filters to query against.
    /// </summary>
    private static readonly MockCommunityTemplate[] SeededAdditionalCommunities =
    [
        // Local league scene — heavy on B / B+ / C amateurs.
        new("Rocket City Pool League",
            "rocket-city-pool",
            "Huntsville-area APA + open league players — B/B+/C amateurs sharing the Bumpers and Steve's tables.",
            CommunityVisibility.Public,
            ["b_chris", "b_kaitlyn", "b_doug", "bp_kevin", "bp_dale",
             "b_jen", "danny_c", "c_marie", "c_ricky"]),

        // Strong-side regional play — North AL outside Huntsville.
        new("North AL Aces",
            "north-al-aces",
            "Strong-side amateurs from Athens, Decatur, and Florence. Mostly B+ and A- players who travel for tournaments.",
            CommunityVisibility.Public,
            ["a_mike", "a_sarah", "ap_riley", "bp_sam", "bp_anh",
             "b_marco", "elite_trent", "a_quentin"]),

        // Heritage-themed — Reyes is the founding member; mixes amateurs and the pro himself.
        new("Filipino Cue Heritage",
            "filipino-cue-heritage",
            "Honoring Efren Reyes and the Filipino school of cue artistry. Open to anyone who wants to study The Magician's game.",
            CommunityVisibility.Public,
            ["pro_efren", "b_priya", "a_jorge", "ap_dom", "elite_trent"]),

        // Fan/watch-party community — pro-watching amateurs + a couple of pros for flavor.
        new("Mosconi Cup Watch Party",
            "mosconi-watch",
            "Pool fans following the Mosconi Cup and the pro tour. Several pros drop in here too.",
            CommunityVisibility.Public,
            ["a_quentin", "ap_riley", "elite_trent", "b_chris", "bp_dale",
             "pro_svb", "pro_filler", "pro_shaw"]),

        // Format-specific niche community.
        new("99-Ball Devotees",
            "99-ball-devotees",
            "Players who specifically love the P&B 9-frame Ninety-Nine format. Discussion of break strategies, Efren-mode runouts, and scoresheet trivia.",
            CommunityVisibility.Public,
            ["b_doug", "b_marco", "ap_dom", "a_sarah", "bp_anh",
             "pro_efren", "pro_gorst", "pro_hohmann"]),
    ];

    // ── Reconcile passes for mock roster + communities ──────────────────

    /// <summary>
    /// Inserts any mock player template (amateur or pro) whose
    /// DisplayName is not yet in the database. Runs on every startup
    /// (idempotent — existing players are left as-is even if the
    /// template's FargoRating has been bumped, since user edits in the
    /// UI should be preserved). Returns the number of players added.
    /// </summary>
    private async Task<int> ReconcileMockPlayerRosterAsync(CancellationToken ct)
    {
        int added = 0;
        foreach (var template in AllMockPlayerTemplates)
        {
            var existing = await playerRepository.GetByDisplayNameAsync(template.DisplayName, ct);
            if (existing is not null) continue;

            var player = new Player
            {
                PlayerId = Guid.NewGuid(),
                DisplayName = template.DisplayName,
                EmailAddress = $"{template.DisplayName}@example.local",
                EmailVerified = true,
                FirstName = template.FirstName,
                LastName = template.LastName,
                FargoRating = template.FargoRating,
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

            await playerRepository.CreateAsync(player, ct);
            added++;
            logger.LogInformation(
                "Seeded mock player: {DisplayName} (Fargo {Fargo}{Efren})",
                template.DisplayName, template.FargoRating,
                template.EfrenOnly ? ", Efren-only" : "");
        }
        return added;
    }

    /// <summary>
    /// Inserts any mock community template not yet in the database, then
    /// reconciles its membership. Idempotent: existing communities are
    /// left unchanged but missing members are added (so editing
    /// <see cref="SeededAdditionalCommunities"/> takes effect on the
    /// next startup without a wipe). Returns the number of communities
    /// created plus the number of memberships added.
    /// </summary>
    private async Task<(int CommunitiesCreated, int MembersAdded)> ReconcileMockCommunitiesAsync(
        CancellationToken ct, Guid? parentCommunityId = null)
    {
        int created = 0;
        int membersAdded = 0;

        foreach (var template in SeededAdditionalCommunities)
        {
            // Resolve the member display names → players. Skip names that
            // don't resolve so an editing pass on the templates can't
            // hard-break startup.
            var members = new List<Player>();
            foreach (var displayName in template.MemberDisplayNames)
            {
                var p = await playerRepository.GetByDisplayNameAsync(displayName, ct);
                if (p is not null) members.Add(p);
            }
            if (members.Count == 0)
            {
                logger.LogWarning(
                    "Skipping mock community '{Name}' — no member display names resolved.",
                    template.Name);
                continue;
            }
            var owner = members[0];

            var community = await communityRepository.GetByNameAsync(template.Name, ct);
            if (community is null)
            {
                community = new Community
                {
                    Name = template.Name,
                    Slug = template.Slug,
                    Description = template.Description,
                    Visibility = template.Visibility,
                    OwnerPlayerId = owner.PlayerId,
                    CreatedByPlayerId = owner.PlayerId,
                    ParentCommunityId = parentCommunityId,
                    CreatedAt = DateTime.UtcNow,
                    SchemaVersion = 3,
                };
                try
                {
                    await communityRepository.CreateAsync(community, ct);
                    created++;
                    logger.LogInformation(
                        "Seeded mock community '{Name}' (owner {Owner}, {Count} initial members)",
                        template.Name, owner.DisplayName, members.Count);
                }
                catch (MongoDB.Driver.MongoWriteException ex)
                    when (ex.WriteError?.Category == MongoDB.Driver.ServerErrorCategory.DuplicateKey)
                {
                    var raced = await communityRepository.GetByNameAsync(template.Name, ct);
                    if (raced is null) throw;
                    community = raced;
                }
            }

            // Membership reconcile: owner is index 0, the rest are members.
            for (int i = 0; i < members.Count; i++)
            {
                var player = members[i];
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
                }
                catch (MongoDB.Driver.MongoWriteException ex)
                    when (ex.WriteError?.Category == MongoDB.Driver.ServerErrorCategory.DuplicateKey)
                {
                    // Raced — benign.
                }
            }
        }

        return (created, membersAdded);
    }
}
