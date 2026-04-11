using NinetyNine.Model;

namespace NinetyNine.Model.Tests;

/// <summary>
/// Locks in the numeric ordering of the <see cref="Audience"/> enum so that
/// <c>relationship &gt;= fieldAudience</c> relationship checks in
/// <c>IPlayerService.GetProfileForViewerAsync</c> remain correct when the
/// enum is extended later (e.g., a future <c>World</c> tier).
/// <para>See docs/plans/friends-communities-v1.md Sprint 0 S0.1.</para>
/// </summary>
public class AudienceTests
{
    [Fact]
    public void Audience_IsOrderedMostPrivateFirst()
    {
        ((int)Audience.Private).Should().Be(0);
        ((int)Audience.Friends).Should().Be(1);
        ((int)Audience.Communities).Should().Be(2);
        ((int)Audience.Public).Should().Be(3);
    }

    [Fact]
    public void Audience_RelationshipComparison_UsesIntOrdering()
    {
        // A friend (relationship = Friends) can see a field whose required
        // audience is Private or Friends, but not Communities or Public.
        var friendRelationship = Audience.Friends;

        (friendRelationship >= Audience.Private).Should().BeTrue();
        (friendRelationship >= Audience.Friends).Should().BeTrue();
        (friendRelationship >= Audience.Communities).Should().BeFalse();
        (friendRelationship >= Audience.Public).Should().BeFalse();
    }

    [Fact]
    public void ProfileVisibility_NewInstance_UsesLockedDefaults()
    {
        var v = new ProfileVisibility();

        v.EmailAudience.Should().Be(Audience.Private,
            "email defaults to most-private per locked fork D");
        v.PhoneAudience.Should().Be(Audience.Private,
            "phone defaults to most-private per locked fork D");
        v.RealNameAudience.Should().Be(Audience.Private,
            "real name defaults to most-private per locked fork D (and DEF-008)");
        v.AvatarAudience.Should().Be(Audience.Public,
            "avatar is the one documented exception — preserves existing behavior");
    }
}
