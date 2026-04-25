using NinetyNine.Services.SeedData;

namespace NinetyNine.Services.Tests;

/// <summary>
/// Env-gated regenerator for the JSON mock-data snapshot under
/// <c>src/NinetyNine.Services/SeedData/</c>. Skipped on every normal
/// CI run; opt in by setting <c>REGEN_MOCK_SNAPSHOT=1</c> when running
/// <c>dotnet test</c>:
///
/// <code>
/// REGEN_MOCK_SNAPSHOT=1 dotnet test \
///     tests/NinetyNine.Services.Tests/NinetyNine.Services.Tests.csproj \
///     --filter "FullyQualifiedName~MockDataSnapshotRegen"
/// </code>
///
/// Re-run after editing any template in
/// <c>MockDataExporter.cs / MockDataTemplates</c>; commit the resulting
/// JSON files alongside the template change so the snapshot stays in
/// sync with the live seed code.
/// </summary>
public class MockDataSnapshotRegen
{
    [Fact]
    public void RegenerateSnapshot()
    {
        if (Environment.GetEnvironmentVariable("REGEN_MOCK_SNAPSHOT") != "1")
        {
            // Soft-skip — visible in test output as a passing test that
            // logs the gate. Hard Skip="…" would still show up but the
            // gate message is more useful.
            return;
        }

        // Walk up from the test's bin directory to the repo root, then
        // into the canonical snapshot location. Robust to running from
        // either the project's own bin or a parent invocation.
        var here = AppContext.BaseDirectory;
        var root = FindRepoRoot(here)
            ?? throw new InvalidOperationException(
                "Could not locate repo root from " + here);

        var outputDir = Path.Combine(
            root, "src", "NinetyNine.Services", "SeedData");

        var (players, communities, games, matches) =
            MockDataExporter.WriteSnapshot(outputDir);

        // Sanity: each file should exist and be non-empty.
        foreach (var path in new[] { players, communities, games, matches })
        {
            File.Exists(path).Should().BeTrue($"{path} should be written");
            new FileInfo(path).Length.Should().BeGreaterThan(100,
                $"{path} should contain at least the schema header + a record");
        }
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
}
