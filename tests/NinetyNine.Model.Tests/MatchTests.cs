using NinetyNine.Model;

namespace NinetyNine.Model.Tests;

/// <summary>
/// Tests for the <see cref="Match"/> entity. Covers default values for
/// both the pre-v0.5.0 head-to-head fields and the new v0.5.0
/// <see cref="MatchRotation"/>/<see cref="Match.CurrentPlayerSeat"/> fields,
/// plus the enum encodings that drive Sequential/Concurrent branching.
/// </summary>
public class MatchTests
{
    [Fact]
    public void Match_Defaults_AreSequentialSingleHeadToHead()
    {
        var match = new Match();

        match.MatchId.Should().NotBeEmpty("MatchId auto-generated");
        match.Rotation.Should().Be(MatchRotation.Sequential,
            "default Rotation must be Sequential so legacy v0.4.x docs and " +
            "new-match form callers without an explicit choice keep " +
            "head-to-head behaviour");
        match.Format.Should().Be(MatchFormat.Single);
        match.Target.Should().Be(1);
        match.PlayerIds.Should().BeEmpty();
        match.GameIds.Should().BeEmpty();
        match.BreakMethod.Should().Be(BreakMethod.Lagged);
        match.TableNumber.Should().BeNull();
        match.Stakes.Should().BeNull();
        match.Status.Should().Be(MatchStatus.Created);
        match.CompletedAt.Should().BeNull();
        match.WinnerPlayerId.Should().BeNull();
        match.CurrentPlayerSeat.Should().Be(0,
            "seat 0 always shoots first in a fresh match");
    }

    [Fact]
    public void MatchRotation_Enum_HasExpectedValues()
    {
        // Sequential MUST be 0 — legacy Match docs persisted before
        // v0.5.0 have no `type` field; a default-valued enum parse on
        // those documents must read as Sequential to preserve behaviour.
        MatchRotation.Sequential.Should().Be((MatchRotation)0,
            "Sequential is the legacy/default rotation; must be 0");
        MatchRotation.Concurrent.Should().Be((MatchRotation)1);
    }

    [Fact]
    public void MatchFormat_Enum_HasExpectedValues()
    {
        MatchFormat.Single.Should().Be((MatchFormat)0);
        MatchFormat.RaceTo.Should().Be((MatchFormat)1);
        MatchFormat.BestOf.Should().Be((MatchFormat)2);
    }

    [Fact]
    public void MatchStatus_Enum_HasExpectedValues()
    {
        MatchStatus.Created.Should().Be((MatchStatus)0);
        MatchStatus.InProgress.Should().Be((MatchStatus)1);
        MatchStatus.Completed.Should().Be((MatchStatus)2);
        MatchStatus.Abandoned.Should().Be((MatchStatus)3);
    }

    [Fact]
    public void Match_CanRepresentConcurrentFourPlayer()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var p3 = Guid.NewGuid();
        var p4 = Guid.NewGuid();

        var match = new Match
        {
            Rotation = MatchRotation.Concurrent,
            PlayerIds = [p1, p2, p3, p4],
            CurrentPlayerSeat = 2,
            VenueId = Guid.NewGuid(),
            Status = MatchStatus.InProgress,
        };

        match.Rotation.Should().Be(MatchRotation.Concurrent);
        match.PlayerIds.Should().HaveCount(4);
        match.CurrentPlayerSeat.Should().Be(2);
        match.PlayerIds[match.CurrentPlayerSeat].Should().Be(p3,
            "CurrentPlayerSeat indexes into PlayerIds in seating order");
    }
}
