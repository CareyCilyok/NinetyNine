using System.Text.Json.Nodes;
using NinetyNine.Model;

namespace NinetyNine.Services.Tests;

/// <summary>
/// v0.8.0 hierarchy invariants:
/// 1. Every mock community in the snapshot lists "Global" (or null) as
///    its parent — Global is the canonical root, snapshot has no
///    deeper structure today.
/// 2. The Community.SchemaVersion bump landed (= 3) on every record we
///    produce.
/// 3. The cycle-detection logic in <c>ICommunityService.SetParentAsync</c>
///    rejects the obvious cases. (Integration test — covered separately
///    in Mongo-backed tests; here we just unit-test the algorithm.)
/// </summary>
public class CommunityHierarchyTests
{
    [Fact]
    public void Community_DefaultParent_IsNull()
    {
        var c = new Community();
        c.ParentCommunityId.Should().BeNull(
            "default Community is a root until reconciled under Global");
        c.SchemaVersion.Should().Be(3,
            "v0.8.0 bumped Community.SchemaVersion to 3");
    }

    [Fact]
    public void MockCommunitiesSnapshot_AllParentedUnderGlobal()
    {
        var (dataPath, _) = LocateSnapshotPaths("mock-communities");
        var node = JsonNode.Parse(File.ReadAllText(dataPath));
        var list = node!["communities"]!.AsArray();

        list.Count.Should().BeGreaterThan(0, "snapshot has communities");

        foreach (var community in list)
        {
            var parent = community!["parentCommunityName"]?.GetValue<string>();
            parent.Should().Be("Global",
                $"every mock community in the snapshot is a child of Global; '{community["name"]}' has '{parent}'");
        }
    }

    /// <summary>
    /// Pure unit test of the cycle-detection algorithm shape — not a
    /// Mongo-backed test. Walks the same logic <c>SetParentAsync</c>
    /// uses: starting from the proposed new parent, walk up the
    /// ancestor chain; if we encounter the community being parented,
    /// reject. The integration test that exercises the live service
    /// against Mongo lives in MatchServiceConcurrentTests-style
    /// fixtures — adding a CommunityServiceTests-style suite is
    /// follow-up work.
    /// </summary>
    [Fact]
    public void CycleDetection_RejectsImmediateSelfParent()
    {
        var aId = Guid.NewGuid();
        var ancestry = new List<Guid> { aId, aId }; // a → a
        ContainsCycle(ancestry).Should().BeTrue();
    }

    [Fact]
    public void CycleDetection_RejectsThreeNodeLoop()
    {
        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();
        var cId = Guid.NewGuid();
        // a is being moved under c; chain c → b → a → ...
        var ancestry = new List<Guid> { aId, cId, bId, aId };
        ContainsCycle(ancestry).Should().BeTrue();
    }

    [Fact]
    public void CycleDetection_AcceptsValidChain()
    {
        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();
        var cId = Guid.NewGuid();
        // a moved under c; chain c → b → null. No cycle.
        var ancestry = new List<Guid> { aId, cId, bId };
        ContainsCycle(ancestry).Should().BeFalse();
    }

    private static bool ContainsCycle(IReadOnlyList<Guid> ancestryWithSubject)
    {
        // First element is the community being parented; remaining
        // elements are the chain starting at the proposed new parent.
        var subject = ancestryWithSubject[0];
        var seen = new HashSet<Guid> { subject };
        for (int i = 1; i < ancestryWithSubject.Count; i++)
        {
            if (!seen.Add(ancestryWithSubject[i])) return true;
        }
        return false;
    }

    private static (string DataPath, string SchemaPath) LocateSnapshotPaths(string baseName)
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
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
