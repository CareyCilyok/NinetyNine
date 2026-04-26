using System.Text.Json;
using NinetyNine.Model;

namespace NinetyNine.Services.SeedData;

/// <summary>
/// Exports the in-memory mock seed data (player templates, community
/// templates, generated game histories, generated match histories) to
/// the four canonical JSON snapshot files under
/// <c>src/NinetyNine.Services/SeedData/</c>.
///
/// <para>
/// Determinism: per-player RNG seeds are computed from a stable FNV-1a
/// hash of the player's DisplayName, so re-running the exporter on any
/// machine produces byte-identical output (modulo legitimate template
/// changes). Do not switch to <c>string.GetHashCode()</c> — .NET 8
/// randomizes it per process, which would make every regen produce a
/// different snapshot.
/// </para>
///
/// <para>
/// Invoked via the env-gated test
/// <c>NinetyNine.Services.Tests.MockDataSnapshotRegen.RegenerateSnapshot</c>.
/// </para>
/// </summary>
public static class MockDataExporter
{
    private const string PlayersFileName     = "mock-players.json";
    private const string CommunitiesFileName = "mock-communities.json";
    private const string GamesFileName       = "mock-games.json";
    private const string MatchesFileName     = "mock-matches.json";

    /// <summary>
    /// Writes the four snapshot files under <paramref name="outputDir"/>.
    /// Returns the file paths in the order
    /// (players, communities, games, matches).
    /// </summary>
    public static (string Players, string Communities, string Games, string Matches)
        WriteSnapshot(string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        var amateurs = MockDataTemplates.Amateurs;
        var pros = MockDataTemplates.Pros;
        var allPlayers = amateurs.Concat(pros).ToArray();
        var playersByName = allPlayers.ToDictionary(p => p.DisplayName);

        // ── Players ────────────────────────────────────────────────────
        // Schema + description live in the sibling .schema.json file.
        var playersFile = new MockPlayersFile(
            Amateurs: amateurs.Select(ToPlayerRecord).ToArray(),
            Pros:     pros.Select(ToPlayerRecord).ToArray());

        var playersPath = Path.Combine(outputDir, PlayersFileName);
        File.WriteAllText(playersPath,
            JsonSerializer.Serialize(playersFile, MockDataSnapshot.JsonOptions));

        // ── Communities ────────────────────────────────────────────────
        var communitiesFile = new MockCommunitiesFile(
            Communities: MockDataTemplates.Communities
                .Select(c => new MockCommunityRecord(
                    Name: c.Name,
                    Slug: c.Slug,
                    Description: c.Description,
                    Visibility: "public",
                    ParentCommunityName: c.ParentCommunityName,
                    MemberDisplayNames: c.MemberDisplayNames.ToArray()))
                .ToArray());

        var communitiesPath = Path.Combine(outputDir, CommunitiesFileName);
        File.WriteAllText(communitiesPath,
            JsonSerializer.Serialize(communitiesFile, MockDataSnapshot.JsonOptions));

        // ── Games ──────────────────────────────────────────────────────
        // Generate per-player game histories using the same
        // distribution helper the live seeder uses, with a stable
        // FNV-1a seed so the snapshot is reproducible across machines.
        var gameRecords = new List<MockGameRecord>();
        foreach (var template in allPlayers)
        {
            int gamesCount = template.EfrenOnly
                ? MockDataTemplates.GamesPerPro
                : MockDataTemplates.GamesPerAmateur;

            var rng = new Random(StableSeed(template.DisplayName));

            for (int g = 0; g < gamesCount; g++)
            {
                bool efren =
                    template.EfrenOnly ||
                    (template.FargoRating >= 620 &&
                     rng.NextDouble() < MockDataTemplates.EfrenAdoptionRateForStrongAmateurs);

                var venue = PickSeedVenueForFargo(template.FargoRating, rng);
                var scores = DataSeeder.GenerateGameFrameScores(
                    template.FargoRating, rng, efren);
                var daysAgo = rng.Next(0, MockDataTemplates.GameHistoryWindowDays);
                var minutesOffset = rng.Next(0, 24 * 60);

                gameRecords.Add(new MockGameRecord(
                    PlayerDisplayName: template.DisplayName,
                    PlayerFargoRating: template.FargoRating,
                    VenueName: venue,
                    TableSize: venue == MockDataTemplates.PrivateHomeVenueName
                        ? "sevenFoot" : "nineFoot",
                    IsEfrenVariant: efren,
                    DaysAgo: daysAgo,
                    MinutesOffset: minutesOffset,
                    FrameScores: scores,
                    TotalScore: scores.Sum()));
            }
        }

        var gamesFile = new MockGamesFile(Games: gameRecords);

        var gamesPath = Path.Combine(outputDir, GamesFileName);
        File.WriteAllText(gamesPath,
            JsonSerializer.Serialize(gamesFile, MockDataSnapshot.JsonOptions));

        // ── Matches ────────────────────────────────────────────────────
        var matchRecords = new List<MockMatchRecord>();
        foreach (var template in MockDataTemplates.Matches)
        {
            // Resolve players. Skip the whole match if any seat doesn't
            // resolve (template-edit safety, mirrors the live seeder).
            var seats = new List<MockPlayerTemplate>();
            bool resolved = true;
            foreach (var name in template.PlayerDisplayNames)
            {
                if (!playersByName.TryGetValue(name, out var p)) { resolved = false; break; }
                seats.Add(p);
            }
            if (!resolved) continue;

            bool isAllPros = template.PlayerDisplayNames
                .All(n => pros.Any(p => p.DisplayName == n));
            bool useEfren = template.ForceEfren || isAllPros;

            var rng = new Random(
                StableSeed(string.Join("|", template.PlayerDisplayNames)));
            var perSeatScores = new List<int[]>();
            int bestTotal = -1;
            string winnerName = seats[0].DisplayName;

            foreach (var seat in seats)
            {
                var scores = DataSeeder.GenerateGameFrameScores(
                    seat.FargoRating, rng, useEfren);
                perSeatScores.Add(scores);
                var total = scores.Sum();
                if (total > bestTotal)
                {
                    bestTotal = total;
                    winnerName = seat.DisplayName;
                }
            }

            matchRecords.Add(new MockMatchRecord(
                Rotation: "concurrent",
                VenueName: template.VenueName,
                DaysAgo: template.DaysAgo,
                IsEfrenVariant: useEfren,
                PlayerDisplayNames: template.PlayerDisplayNames.ToArray(),
                PlayerFrameScores: perSeatScores,
                WinnerDisplayName: winnerName));
        }

        var matchesFile = new MockMatchesFile(Matches: matchRecords);

        var matchesPath = Path.Combine(outputDir, MatchesFileName);
        File.WriteAllText(matchesPath,
            JsonSerializer.Serialize(matchesFile, MockDataSnapshot.JsonOptions));

        return (playersPath, communitiesPath, gamesPath, matchesPath);
    }

    /// <summary>
    /// FNV-1a 32-bit hash. Stable across processes, runtimes, and
    /// architectures — unlike <see cref="string.GetHashCode"/> which
    /// .NET 8+ randomizes per process. Used as the per-player RNG seed
    /// so snapshots are reproducible.
    /// </summary>
    private static int StableSeed(string s)
    {
        const uint offset = 2166136261u;
        const uint prime  = 16777619u;
        uint hash = offset;
        foreach (char c in s)
        {
            hash ^= c;
            hash *= prime;
        }
        return unchecked((int)hash);
    }

    private static string PickSeedVenueForFargo(int fargo, Random rng)
    {
        // Mirror the live seeder's venue-selection bias: low brackets
        // play more at the home table, higher at real public venues.
        if (fargo < 450 && rng.NextDouble() < 0.4)
            return MockDataTemplates.PrivateHomeVenueName;

        var venues = MockDataTemplates.PublicSeedVenues;
        return venues[rng.Next(venues.Length)];
    }

    private static MockPlayerRecord ToPlayerRecord(MockPlayerTemplate t) =>
        new(
            DisplayName: t.DisplayName,
            FirstName:   t.FirstName,
            LastName:    t.LastName,
            FargoRating: t.FargoRating,
            EfrenOnly:   t.EfrenOnly,
            Bracket:     BracketLabel(t.FargoRating));

    private static string BracketLabel(int fargo) => fargo switch
    {
        >= 800 => "TouringPro (800–850)",
        >= 750 => "EliteAmateur (750–799)",
        >= 700 => "StrongA+ (700–749)",
        >= 620 => "AdvancedA- (620–699)",
        >= 550 => "StrongAmateurB+ (550–619)",
        >= 450 => "MidAmateurB (450–549)",
        >= 350 => "DevelopingC (350–449)",
        _      => "RecBeginner (270–349)",
    };
}

// ── Public mirror of the templates so the exporter can reach them ──────────
//
// The live seeder (DataSeeder.MockRoster.cs / .MockMatches.cs) keeps its
// templates `private`. Rather than poke holes in that encapsulation, we
// duplicate the templates here in a separate static class. Single source
// of truth at the value level: keep the two lists in sync when editing.
// The exporter test verifies they match before writing the snapshot.

internal sealed record MockPlayerTemplate(
    string DisplayName,
    string FirstName,
    string? LastName,
    int FargoRating,
    bool EfrenOnly = false);

internal sealed record MockCommunityTemplate(
    string Name,
    string Slug,
    string Description,
    string[] MemberDisplayNames,
    string? ParentCommunityName = "Global");

internal sealed record MockMatchTemplate(
    string[] PlayerDisplayNames,
    string VenueName,
    int DaysAgo,
    bool ForceEfren = false);

internal static class MockDataTemplates
{
    public const int GamesPerAmateur = 8;
    public const int GamesPerPro     = 5;
    public const int GameHistoryWindowDays = 90;
    public const double EfrenAdoptionRateForStrongAmateurs = 0.35;

    public const string PrivateHomeVenueName = "Carey's Home Table";
    public static readonly string[] PublicSeedVenues =
    [
        "Bumpers Billiards of Huntsville",
        "Steve's Cue and Grill",
        "Chips & Salsa Sports Bar & Grill",
        "Good Timez Billiards",
        "Limestone Legends Billiards",
        "6 Pockets Billiards",
        "Ron's City Billiards",
        "Tennessee Street Billiards & Grill",
        "Iron City Billiards",
        "All In One Billiards",
        "The Break",
        "Melrose Billiard Parlor",
        "8 Ball Billiards",
        "The Breakroom",
        "MrCues II Billiards",
        "Yer Mom",
    ];

    public static readonly MockPlayerTemplate[] Amateurs =
    [
        new("rookie_brad",   "Brad",    "Henson",   310),
        new("learner_alice", "Alice",   "Park",     325),
        new("newbie_marcus", "Marcus",  "Vaughn",   340),
        new("jules_fresh",   "Jules",   "Mason",    295),

        new("danny_c",  "Danny", "Reilly",    380),
        new("c_marie",  "Marie", "Thompson",  410),
        new("c_jason",  "Jason", "Holt",      425),
        new("c_nina",   "Nina",  "Park",      395),
        new("c_ricky",  "Ricky", "Andrews",   440),

        new("b_chris",    "Chris",    "Bauer",     480),
        new("b_kaitlyn",  "Kaitlyn",  "Greer",     510),
        new("b_doug",     "Doug",     "Newman",    530),
        new("b_priya",    "Priya",    "Rao",       495),
        new("b_marco",    "Marco",    "Belluschi", 540),
        new("b_jen",      "Jen",      "Sayers",    470),

        new("bp_kevin",  "Kevin",  "Davila",   575),
        new("bp_anh",    "Anh",    "Tran",     600),
        new("bp_dale",   "Dale",   "Henson",   580),
        new("bp_sam",    "Sam",    "Whitlock", 615),

        new("a_mike",     "Mike",    "Cardenas",   650),
        new("a_sarah",    "Sarah",   "Olstead",    685),
        new("a_jorge",    "Jorge",   "Espinosa",   670),
        new("a_quentin",  "Quentin", "Brooks",     695),

        new("ap_riley",  "Riley", "Tanaka",  720),
        new("ap_dom",    "Dom",   "Costa",   745),

        new("elite_trent", "Trent", "Marshall", 775),
    ];

    public static readonly MockPlayerTemplate[] Pros =
    [
        new("pro_efren",    "Efren",    "Reyes",         775, EfrenOnly: true),
        new("pro_svb",      "Shane",    "Van Boening",   840, EfrenOnly: true),
        new("pro_filler",   "Joshua",   "Filler",        842, EfrenOnly: true),
        new("pro_ko",       "Pin-Yi",   "Ko",            832, EfrenOnly: true),
        new("pro_shaw",     "Jayson",   "Shaw",          831, EfrenOnly: true),
        new("pro_gorst",    "Fedor",    "Gorst",         825, EfrenOnly: true),
        new("pro_hohmann",  "Thorsten", "Hohmann",       814, EfrenOnly: true),
    ];

    public static readonly MockCommunityTemplate[] Communities =
    [
        new("Rocket City Pool League",
            "rocket-city-pool",
            "Huntsville-area APA + open league players — B/B+/C amateurs sharing the Bumpers and Steve's tables.",
            ["b_chris", "b_kaitlyn", "b_doug", "bp_kevin", "bp_dale",
             "b_jen", "danny_c", "c_marie", "c_ricky"]),

        new("North AL Aces",
            "north-al-aces",
            "Strong-side amateurs from Athens, Decatur, and Florence. Mostly B+ and A- players who travel for tournaments.",
            ["a_mike", "a_sarah", "ap_riley", "bp_sam", "bp_anh",
             "b_marco", "elite_trent", "a_quentin"]),

        new("Filipino Cue Heritage",
            "filipino-cue-heritage",
            "Honoring Efren Reyes and the Filipino school of cue artistry. Open to anyone who wants to study The Magician's game.",
            ["pro_efren", "b_priya", "a_jorge", "ap_dom", "elite_trent"]),

        new("Mosconi Cup Watch Party",
            "mosconi-watch",
            "Pool fans following the Mosconi Cup and the pro tour. Several pros drop in here too.",
            ["a_quentin", "ap_riley", "elite_trent", "b_chris", "bp_dale",
             "pro_svb", "pro_filler", "pro_shaw"]),

        new("99-Ball Devotees",
            "99-ball-devotees",
            "Players who specifically love the P&B 9-frame Ninety-Nine format. Discussion of break strategies, Efren-mode runouts, and scoresheet trivia.",
            ["b_doug", "b_marco", "ap_dom", "a_sarah", "bp_anh",
             "pro_efren", "pro_gorst", "pro_hohmann"]),
    ];

    public static readonly MockMatchTemplate[] Matches =
    [
        new(["b_chris", "b_kaitlyn"],         "Bumpers Billiards of Huntsville", 2),
        new(["b_doug", "b_marco"],            "Steve's Cue and Grill",            5),
        new(["bp_kevin", "bp_dale"],          "Bumpers Billiards of Huntsville", 8),
        new(["bp_anh", "bp_sam"],             "Iron City Billiards",             14),
        new(["b_priya", "b_jen"],             "Chips & Salsa Sports Bar & Grill", 4),
        new(["c_marie", "c_jason"],           "Good Timez Billiards",            10),
        new(["danny_c", "c_ricky"],           "Limestone Legends Billiards",     16),

        new(["a_mike", "a_sarah"],            "Iron City Billiards",              6),
        new(["a_jorge", "a_quentin"],         "MrCues II Billiards",             11),
        new(["ap_riley", "ap_dom"],           "Melrose Billiard Parlor",          9),

        new(["b_chris", "b_kaitlyn", "bp_kevin"], "Steve's Cue and Grill",       18),
        new(["a_mike", "a_jorge", "a_sarah"],     "Iron City Billiards",         22),
        new(["bp_dale", "bp_sam", "a_quentin"],   "Bumpers Billiards of Huntsville", 25),
        new(["c_nina", "c_jason", "danny_c"],     "Good Timez Billiards",        30),

        new(["b_chris", "b_doug", "b_marco", "bp_anh"],     "Bumpers Billiards of Huntsville", 35),
        new(["a_mike", "ap_riley", "elite_trent", "a_sarah"], "Iron City Billiards",            40),

        new(["pro_svb", "pro_filler"],                       "MrCues II Billiards",   12, ForceEfren: true),
        new(["pro_shaw", "pro_ko"],                          "Melrose Billiard Parlor", 19, ForceEfren: true),
        new(["pro_gorst", "pro_hohmann", "pro_efren"],       "Iron City Billiards",   28, ForceEfren: true),
        new(["pro_svb", "pro_filler", "pro_gorst", "pro_shaw"], "MrCues II Billiards", 45, ForceEfren: true),
    ];
}
