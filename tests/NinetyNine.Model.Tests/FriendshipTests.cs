using NinetyNine.Model;

namespace NinetyNine.Model.Tests;

/// <summary>
/// Locks in the <see cref="Friendship"/> canonical-ordering invariant:
/// <see cref="Friendship.PlayerAId"/> is always the lexicographically
/// smaller of the two Guids, so the unique edge index
/// <c>{PlayerAId, PlayerBId}</c> (and its derived <c>PlayerIdsKey</c>)
/// correctly dedupes regardless of argument order.
/// <para>See docs/plans/friends-communities-v1.md Sprint 0 S0.2.</para>
/// </summary>
public class FriendshipTests
{
    [Fact]
    public void Create_OrdersPlayersCanonically()
    {
        var low = new Guid("00000000-0000-0000-0000-000000000001");
        var high = new Guid("ff000000-0000-0000-0000-000000000000");

        var fromLowHigh = Friendship.Create(low, high);
        var fromHighLow = Friendship.Create(high, low);

        fromLowHigh.PlayerAId.Should().Be(low);
        fromLowHigh.PlayerBId.Should().Be(high);
        fromHighLow.PlayerAId.Should().Be(low, "order must not depend on argument order");
        fromHighLow.PlayerBId.Should().Be(high);

        fromLowHigh.PlayerIdsKey.Should().Be(fromHighLow.PlayerIdsKey,
            "PlayerIdsKey must be deterministic regardless of argument order");
    }

    [Fact]
    public void Create_RejectsSelfFriendship()
    {
        var p = Guid.NewGuid();
        var act = () => Friendship.Create(p, p);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void OtherParty_ReturnsTheOpposite()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var f = Friendship.Create(a, b);

        f.OtherParty(a).Should().Be(b);
        f.OtherParty(b).Should().Be(a);
    }

    [Fact]
    public void OtherParty_RejectsNonParticipant()
    {
        var f = Friendship.Create(Guid.NewGuid(), Guid.NewGuid());
        var act = () => f.OtherParty(Guid.NewGuid());
        act.Should().Throw<ArgumentException>();
    }
}

/// <summary>
/// After the 2026-04-11 principle update the Community model is
/// simpler — <see cref="Community.OwnerPlayerId"/> is always set and
/// non-nullable, venues cannot own a community. The remaining
/// invariant is just "owner is a valid Guid" which the language's
/// non-nullable type already enforces at compile time, so there are
/// no runtime-invariant tests here. See the plan's fork-B reversal
/// and <c>project-pool-players-only</c> memory.
/// </summary>
public class CommunityInvariantTests
{
    [Fact]
    public void Community_DefaultsHaveCorrectShape()
    {
        var c = new Community
        {
            Name = "Test",
            Slug = "test",
            OwnerPlayerId = Guid.NewGuid(),
            CreatedByPlayerId = Guid.NewGuid(),
        };

        c.CommunityId.Should().NotBeEmpty("auto-generated");
        c.Visibility.Should().Be(CommunityVisibility.Public);
        c.SchemaVersion.Should().Be(2,
            "Sprint 3 principle update bumped the default schema version");
        c.OwnerPlayerId.Should().NotBe(Guid.Empty);
    }
}
