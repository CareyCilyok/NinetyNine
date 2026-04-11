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
/// Locks in the <see cref="Community"/> owner-type / owner-id
/// mutual-exclusion invariant required by the authorization model.
/// </summary>
public class CommunityInvariantTests
{
    [Fact]
    public void AssertOwnerInvariant_PassesForPlayerOwned()
    {
        var c = new Community
        {
            Name = "Test",
            Slug = "test",
            OwnerType = CommunityOwnerType.Player,
            OwnerPlayerId = Guid.NewGuid(),
            CreatedByPlayerId = Guid.NewGuid(),
        };
        c.Invoking(x => x.AssertOwnerInvariant()).Should().NotThrow();
    }

    [Fact]
    public void AssertOwnerInvariant_PassesForVenueOwned()
    {
        var c = new Community
        {
            Name = "Test",
            Slug = "test",
            OwnerType = CommunityOwnerType.Venue,
            OwnerVenueId = Guid.NewGuid(),
            CreatedByPlayerId = Guid.NewGuid(),
        };
        c.Invoking(x => x.AssertOwnerInvariant()).Should().NotThrow();
    }

    [Fact]
    public void AssertOwnerInvariant_FailsWhenPlayerOwnedHasNoPlayerId()
    {
        var c = new Community
        {
            Name = "Test",
            Slug = "test",
            OwnerType = CommunityOwnerType.Player,
            OwnerPlayerId = null,
            CreatedByPlayerId = Guid.NewGuid(),
        };
        c.Invoking(x => x.AssertOwnerInvariant())
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AssertOwnerInvariant_FailsWhenPlayerOwnedAlsoSetsVenueId()
    {
        var c = new Community
        {
            Name = "Test",
            Slug = "test",
            OwnerType = CommunityOwnerType.Player,
            OwnerPlayerId = Guid.NewGuid(),
            OwnerVenueId = Guid.NewGuid(),
            CreatedByPlayerId = Guid.NewGuid(),
        };
        c.Invoking(x => x.AssertOwnerInvariant())
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AssertOwnerInvariant_FailsWhenVenueOwnedHasNoVenueId()
    {
        var c = new Community
        {
            Name = "Test",
            Slug = "test",
            OwnerType = CommunityOwnerType.Venue,
            OwnerVenueId = null,
            CreatedByPlayerId = Guid.NewGuid(),
        };
        c.Invoking(x => x.AssertOwnerInvariant())
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AssertOwnerInvariant_FailsWhenVenueOwnedAlsoSetsPlayerId()
    {
        var c = new Community
        {
            Name = "Test",
            Slug = "test",
            OwnerType = CommunityOwnerType.Venue,
            OwnerVenueId = Guid.NewGuid(),
            OwnerPlayerId = Guid.NewGuid(),
            CreatedByPlayerId = Guid.NewGuid(),
        };
        c.Invoking(x => x.AssertOwnerInvariant())
            .Should().Throw<InvalidOperationException>();
    }
}
