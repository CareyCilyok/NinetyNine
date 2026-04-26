using System.Text.Json.Nodes;
using Json.Schema;
using NinetyNine.Model;

namespace NinetyNine.Services.Tests;

/// <summary>
/// Enforces the project rule: the data model and its JSON Schema are
/// the ground truth for mock test data. Drift between
/// <c>src/NinetyNine.Services/SeedData/mock-*.json</c> and the sibling
/// <c>mock-*.schema.json</c> fails CI.
///
/// <para>
/// Companion check: every <c>mock-games.json</c> game must satisfy
/// <see cref="Frame"/> + <see cref="Game"/> runtime invariants — every
/// frame score in [0, 11], every game total in [0, 99], exactly nine
/// frames per game. These are model-layer rules that JSON Schema can
/// only partially express; the tests here belt-and-suspender both.
/// </para>
///
/// <para>
/// When you edit a Model class or one of the schema files, re-run the
/// snapshot regen (REGEN_MOCK_SNAPSHOT=1) and re-run these tests.
/// If a model field changes shape, the data file must be regenerated
/// AND the schema file must be hand-edited to match.
/// </para>
/// </summary>
public class MockDataSchemaValidationTests
{
    private static readonly EvaluationOptions Strict = new()
    {
        OutputFormat = OutputFormat.List,
    };

    [Theory]
    [InlineData("mock-players")]
    [InlineData("mock-communities")]
    [InlineData("mock-games")]
    [InlineData("mock-matches")]
    public void DataFile_ValidatesAgainstSchema(string baseName)
    {
        var (dataPath, schemaPath) = LocateSeedDataPaths(baseName);

        File.Exists(dataPath).Should().BeTrue($"data file should exist: {dataPath}");
        File.Exists(schemaPath).Should().BeTrue($"schema file should exist: {schemaPath}");

        var schema = JsonSchema.FromFile(schemaPath);
        var data = JsonNode.Parse(File.ReadAllText(dataPath));

        var result = schema.Evaluate(data, Strict);

        if (!result.IsValid)
        {
            var errors = string.Join("\n  - ",
                CollectErrors(result));
            Assert.Fail(
                $"{baseName}.json failed schema validation against {baseName}.schema.json:\n  - {errors}");
        }
    }

    /// <summary>
    /// Runtime-invariant pass on the games snapshot: every game must
    /// have nine frames, each in [0, 11], summing to [0, 99]. The JSON
    /// Schema asserts these too via per-element <c>minimum</c>/<c>maximum</c>
    /// + <c>minItems</c>/<c>maxItems</c>, but doing it here as well
    /// guards against schema drift.
    /// </summary>
    [Fact]
    public void GamesSnapshot_AllFramesAndTotals_RespectModelInvariants()
    {
        var (dataPath, _) = LocateSeedDataPaths("mock-games");
        var node = JsonNode.Parse(File.ReadAllText(dataPath));
        var games = node!["games"]!.AsArray();

        foreach (var game in games)
        {
            var frames = game!["frameScores"]!.AsArray();
            frames.Count.Should().Be(9, "every game has exactly 9 frames");

            int sum = 0;
            foreach (var f in frames)
            {
                var v = f!.GetValue<int>();
                v.Should().BeInRange(0, 11, "Frame.IsValidScore: BreakBonus(0..1) + BallCount(0..10), capped at 11");
                sum += v;
            }

            sum.Should().BeInRange(0, 99, "Game total cannot exceed 99 (model invariant)");
            game["totalScore"]!.GetValue<int>().Should().Be(sum,
                "denormalized totalScore must match the sum of frameScores");
        }
    }

    /// <summary>
    /// Runtime-invariant pass on the matches snapshot: each seat in
    /// playerFrameScores must align 1:1 with playerDisplayNames; each
    /// seat is a 9-frame array with the same per-frame and per-game
    /// bounds; the winner's score is the max (with the canonical
    /// tie-break order applied — same arbiter MatchService uses).
    /// </summary>
    [Fact]
    public void MatchesSnapshot_SeatsAlign_AndWinnerSatisfiesArbiter()
    {
        var (dataPath, _) = LocateSeedDataPaths("mock-matches");
        var node = JsonNode.Parse(File.ReadAllText(dataPath));
        var matches = node!["matches"]!.AsArray();

        foreach (var match in matches)
        {
            var names = match!["playerDisplayNames"]!.AsArray();
            var seatScores = match["playerFrameScores"]!.AsArray();
            var winnerName = match["winnerDisplayName"]!.GetValue<string>();

            seatScores.Count.Should().Be(names.Count,
                "playerFrameScores must align with playerDisplayNames row-for-row");

            int bestTotal = -1;
            string bestName = "";
            for (int i = 0; i < names.Count; i++)
            {
                var nm = names[i]!.GetValue<string>();
                var frames = seatScores[i]!.AsArray();
                frames.Count.Should().Be(9, "each seat has 9 frames");

                int total = 0;
                foreach (var f in frames)
                {
                    var v = f!.GetValue<int>();
                    v.Should().BeInRange(0, 11);
                    total += v;
                }
                total.Should().BeInRange(0, 99);

                if (total > bestTotal) { bestTotal = total; bestName = nm; }
            }

            // Strict arbiter: highest TotalScore is the unambiguous winner
            // (the snapshot's RNG seed yields no ties at this scope, so the
            // tie-break path isn't exercised here — and that's fine; the
            // unit tests in MatchServiceWinnerTests cover the tie-break
            // ladder explicitly).
            winnerName.Should().Be(bestName,
                $"winnerDisplayName must be the seat with the highest TotalScore " +
                $"(or first-by-arbiter on ties); got winner={winnerName} top={bestName}@{bestTotal}");
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static (string DataPath, string SchemaPath) LocateSeedDataPaths(string baseName)
    {
        var root = FindRepoRoot(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException(
                "Could not locate repo root from " + AppContext.BaseDirectory);
        var seedDir = Path.Combine(root, "src", "NinetyNine.Services", "SeedData");
        return (
            Path.Combine(seedDir, baseName + ".json"),
            Path.Combine(seedDir, baseName + ".schema.json"));
    }

    private static string? FindRepoRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")) ||
                File.Exists(Path.Combine(dir.FullName, "NinetyNine.sln")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        return null;
    }

    private static IEnumerable<string> CollectErrors(EvaluationResults results)
    {
        if (results.Errors is { Count: > 0 })
        {
            foreach (var (path, msg) in results.Errors)
                yield return $"{results.InstanceLocation}: {path}: {msg}";
        }
        foreach (var child in results.Details)
            foreach (var e in CollectErrors(child))
                yield return e;
    }
}
