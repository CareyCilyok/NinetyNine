using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using NinetyNine.Model;

namespace NinetyNine.Services.SeedData;

/// <summary>
/// Read-only JSON snapshot of the mock seed data shipped with the
/// NinetyNine dev seeder. The C# templates in
/// <c>DataSeeder.MockRoster.cs</c> + <c>DataSeeder.MockMatches.cs</c>
/// remain the source of truth for *generation*; this snapshot is the
/// canonical *what was generated* at v0.6.x for cross-environment and
/// cross-agent reading (notably: Claude Design, who can read the repo
/// but cannot run the seeder).
///
/// <para>
/// Regenerate via the env-gated test
/// <c>NinetyNine.Services.Tests.MockDataSnapshotRegen.RegenerateSnapshot</c>
/// (set <c>REGEN_MOCK_SNAPSHOT=1</c> when invoking <c>dotnet test</c>).
/// </para>
/// </summary>
public static class MockDataSnapshot
{
    /// <summary>Schema version for the snapshot files.</summary>
    public const int SchemaVersion = 1;

    /// <summary>JSON serializer settings used by the exporter.</summary>
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Leave en/em-dashes, apostrophes, and other common Unicode
        // alone so the JSON is human-readable on inspection. Safe here:
        // the snapshot files are read by trusted code only.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
        },
    };
}

/// <summary>Top-level shape of <c>mock-players.json</c>.</summary>
public sealed record MockPlayersFile(
    int SchemaVersion,
    string Description,
    IReadOnlyList<MockPlayerRecord> Amateurs,
    IReadOnlyList<MockPlayerRecord> Pros);

public sealed record MockPlayerRecord(
    string DisplayName,
    string FirstName,
    string? LastName,
    int FargoRating,
    bool EfrenOnly,
    string Bracket);

/// <summary>Top-level shape of <c>mock-communities.json</c>.</summary>
public sealed record MockCommunitiesFile(
    int SchemaVersion,
    string Description,
    IReadOnlyList<MockCommunityRecord> Communities);

public sealed record MockCommunityRecord(
    string Name,
    string Slug,
    string Description,
    string Visibility,
    /// <summary>First entry is the owner.</summary>
    IReadOnlyList<string> MemberDisplayNames);

/// <summary>Top-level shape of <c>mock-games.json</c>.</summary>
public sealed record MockGamesFile(
    int SchemaVersion,
    string Description,
    IReadOnlyList<MockGameRecord> Games);

public sealed record MockGameRecord(
    string PlayerDisplayName,
    int PlayerFargoRating,
    string VenueName,
    string TableSize,
    bool IsEfrenVariant,
    int DaysAgo,
    int MinutesOffset,
    /// <summary>Length 9; each value in [0, 11]; sum in [0, 99].</summary>
    int[] FrameScores,
    int TotalScore);

/// <summary>Top-level shape of <c>mock-matches.json</c>.</summary>
public sealed record MockMatchesFile(
    int SchemaVersion,
    string Description,
    IReadOnlyList<MockMatchRecord> Matches);

public sealed record MockMatchRecord(
    string Rotation,
    string VenueName,
    int DaysAgo,
    bool IsEfrenVariant,
    IReadOnlyList<string> PlayerDisplayNames,
    /// <summary>
    /// One <c>int[9]</c> per seat, in seating order. Each row's sum is
    /// the seat's total score; the highest sum wins (ties → most
    /// perfect frames → earliest CompletedAt; same arbiter
    /// <c>MatchService.SelectConcurrentWinner</c> uses).
    /// </summary>
    IReadOnlyList<int[]> PlayerFrameScores,
    string WinnerDisplayName);
