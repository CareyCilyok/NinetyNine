using Microsoft.Extensions.Logging.Abstractions;
using NinetyNine.Model;
using NinetyNine.Repository.Repositories;
using NinetyNine.Services;

namespace NinetyNine.Services.Tests;

/// <summary>
/// Integration tests for <see cref="FriendService"/> against a real
/// MongoDB Testcontainer. Each test uses fresh Guids so the fixture
/// does not need cleaning between tests.
/// <para>See docs/plans/friends-communities-v1.md Sprint 1 S1.1.</para>
/// </summary>
[Collection(MongoCollection.Name)]
[Trait("Category", "Integration")]
public class FriendServiceTests(MongoFixture fixture)
{
    private IFriendService CreateService(out IPlayerRepository playerRepo)
    {
        var ctx = fixture.CreateDbContext();
        var friendships = new FriendshipRepository(ctx, NullLogger<FriendshipRepository>.Instance);
        var requests = new FriendRequestRepository(ctx, NullLogger<FriendRequestRepository>.Instance);
        playerRepo = new PlayerRepository(ctx, NullLogger<PlayerRepository>.Instance);
        var notifications = new NotificationRepository(ctx, NullLogger<NotificationRepository>.Instance);
        var notifSvc = new NotificationService(notifications, NullLogger<NotificationService>.Instance);
        return new FriendService(friendships, requests, playerRepo, notifSvc, NullLogger<FriendService>.Instance);
    }

    private static async Task<Player> CreatePlayer(IPlayerRepository repo, string? displayName = null)
    {
        var player = new Player
        {
            PlayerId = Guid.NewGuid(),
            DisplayName = displayName ?? "p_" + Guid.NewGuid().ToString("N")[..12],
            EmailAddress = Guid.NewGuid().ToString("N") + "@example.local",
            EmailVerified = true,
            SchemaVersion = 2,
        };
        await repo.CreateAsync(player);
        return player;
    }

    // ── Invariants ──────────────────────────────────────────────────────

    [Fact]
    public async Task SendRequest_RejectsSelfFriendship()
    {
        var svc = CreateService(out var repo);
        var me = await CreatePlayer(repo);

        var result = await svc.SendRequestAsync(me.PlayerId, me.PlayerId);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("SelfFriendship");
    }

    [Fact]
    public async Task SendRequest_RejectsMissingTarget()
    {
        var svc = CreateService(out var repo);
        var me = await CreatePlayer(repo);

        var result = await svc.SendRequestAsync(me.PlayerId, Guid.NewGuid());

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("TargetNotFound");
    }

    [Fact]
    public async Task SendRequest_ToExistingFriend_IsRejected()
    {
        var svc = CreateService(out var repo);
        var me = await CreatePlayer(repo);
        var friend = await CreatePlayer(repo);

        var send = await svc.SendRequestAsync(me.PlayerId, friend.PlayerId);
        await svc.AcceptRequestAsync(send.Value!.RequestId, friend.PlayerId);

        var second = await svc.SendRequestAsync(me.PlayerId, friend.PlayerId);
        second.Success.Should().BeFalse();
        second.ErrorCode.Should().Be("AlreadyFriends");
    }

    [Fact]
    public async Task DuplicatePendingInSameDirection_IsRejected()
    {
        var svc = CreateService(out var repo);
        var me = await CreatePlayer(repo);
        var target = await CreatePlayer(repo);

        var first = await svc.SendRequestAsync(me.PlayerId, target.PlayerId);
        first.Success.Should().BeTrue();

        var second = await svc.SendRequestAsync(me.PlayerId, target.PlayerId);
        second.Success.Should().BeFalse();
        second.ErrorCode.Should().Be("RequestAlreadyPending");
    }

    [Fact]
    public async Task ReversePending_BlocksNewForwardRequest()
    {
        var svc = CreateService(out var repo);
        var a = await CreatePlayer(repo);
        var b = await CreatePlayer(repo);

        // A sends to B first.
        await svc.SendRequestAsync(a.PlayerId, b.PlayerId);

        // B cannot also send to A — they should accept instead.
        var result = await svc.SendRequestAsync(b.PlayerId, a.PlayerId);
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("RequestAlreadyPending");
    }

    // ── Accept / decline / cancel lifecycle ─────────────────────────────

    [Fact]
    public async Task Accept_CreatesFriendshipAndFlipsStatus()
    {
        var svc = CreateService(out var repo);
        var a = await CreatePlayer(repo);
        var b = await CreatePlayer(repo);

        var send = await svc.SendRequestAsync(a.PlayerId, b.PlayerId, "hey");
        send.Success.Should().BeTrue();

        var accept = await svc.AcceptRequestAsync(send.Value!.RequestId, b.PlayerId);
        accept.Success.Should().BeTrue();
        accept.Value.Should().NotBeNull();

        (await svc.AreFriendsAsync(a.PlayerId, b.PlayerId)).Should().BeTrue();
        (await svc.AreFriendsAsync(b.PlayerId, a.PlayerId)).Should().BeTrue();
    }

    [Fact]
    public async Task Accept_OnlyRecipientCanAccept()
    {
        var svc = CreateService(out var repo);
        var a = await CreatePlayer(repo);
        var b = await CreatePlayer(repo);
        var bystander = await CreatePlayer(repo);

        var send = await svc.SendRequestAsync(a.PlayerId, b.PlayerId);

        var wrongAcceptor = await svc.AcceptRequestAsync(send.Value!.RequestId, bystander.PlayerId);
        wrongAcceptor.Success.Should().BeFalse();
        wrongAcceptor.ErrorCode.Should().Be("NotAuthorized");

        var senderTryingToAccept = await svc.AcceptRequestAsync(send.Value.RequestId, a.PlayerId);
        senderTryingToAccept.Success.Should().BeFalse();
        senderTryingToAccept.ErrorCode.Should().Be("NotAuthorized");
    }

    [Fact]
    public async Task Decline_OnlyRecipientCanDecline()
    {
        var svc = CreateService(out var repo);
        var a = await CreatePlayer(repo);
        var b = await CreatePlayer(repo);

        var send = await svc.SendRequestAsync(a.PlayerId, b.PlayerId);

        var senderTryingToDecline = await svc.DeclineRequestAsync(send.Value!.RequestId, a.PlayerId);
        senderTryingToDecline.Success.Should().BeFalse();
        senderTryingToDecline.ErrorCode.Should().Be("NotAuthorized");

        var decline = await svc.DeclineRequestAsync(send.Value.RequestId, b.PlayerId);
        decline.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Cancel_OnlySenderCanCancel()
    {
        var svc = CreateService(out var repo);
        var a = await CreatePlayer(repo);
        var b = await CreatePlayer(repo);

        var send = await svc.SendRequestAsync(a.PlayerId, b.PlayerId);

        var recipientTryingToCancel = await svc.CancelRequestAsync(send.Value!.RequestId, b.PlayerId);
        recipientTryingToCancel.Success.Should().BeFalse();
        recipientTryingToCancel.ErrorCode.Should().Be("NotAuthorized");

        var cancel = await svc.CancelRequestAsync(send.Value.RequestId, a.PlayerId);
        cancel.Success.Should().BeTrue();
    }

    [Fact]
    public async Task TerminalRequests_CannotBeMutated()
    {
        var svc = CreateService(out var repo);
        var a = await CreatePlayer(repo);
        var b = await CreatePlayer(repo);

        var send = await svc.SendRequestAsync(a.PlayerId, b.PlayerId);
        await svc.DeclineRequestAsync(send.Value!.RequestId, b.PlayerId);

        var acceptAgain = await svc.AcceptRequestAsync(send.Value.RequestId, b.PlayerId);
        acceptAgain.Success.Should().BeFalse();
        acceptAgain.ErrorCode.Should().Be("RequestNotPending");
    }

    // ── Unfriend ────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveFriend_IsBidirectionalAndIdempotent()
    {
        var svc = CreateService(out var repo);
        var a = await CreatePlayer(repo);
        var b = await CreatePlayer(repo);

        var send = await svc.SendRequestAsync(a.PlayerId, b.PlayerId);
        await svc.AcceptRequestAsync(send.Value!.RequestId, b.PlayerId);

        await svc.RemoveFriendAsync(a.PlayerId, b.PlayerId);

        (await svc.AreFriendsAsync(a.PlayerId, b.PlayerId)).Should().BeFalse();

        // Idempotent — second call should not throw.
        var second = await svc.RemoveFriendAsync(a.PlayerId, b.PlayerId);
        second.Success.Should().BeTrue();
    }

    [Fact]
    public async Task AfterRemoveFriend_NewRequest_IsAllowed()
    {
        var svc = CreateService(out var repo);
        var a = await CreatePlayer(repo);
        var b = await CreatePlayer(repo);

        var send = await svc.SendRequestAsync(a.PlayerId, b.PlayerId);
        await svc.AcceptRequestAsync(send.Value!.RequestId, b.PlayerId);
        await svc.RemoveFriendAsync(a.PlayerId, b.PlayerId);

        // Can re-request after removal — no cooldown from a voluntary unfriend.
        var reRequest = await svc.SendRequestAsync(a.PlayerId, b.PlayerId);
        reRequest.Success.Should().BeTrue();
    }

    // ── Relationship probe ──────────────────────────────────────────────

    [Fact]
    public async Task GetRelationship_ReturnsSelfForOwnProfile()
    {
        var svc = CreateService(out var repo);
        var me = await CreatePlayer(repo);

        var rel = await svc.GetRelationshipAsync(me.PlayerId, me.PlayerId);
        rel.Should().Be(RelationshipState.Self);
    }

    [Fact]
    public async Task GetRelationship_TracksLifecycleTransitions()
    {
        var svc = CreateService(out var repo);
        var a = await CreatePlayer(repo);
        var b = await CreatePlayer(repo);

        (await svc.GetRelationshipAsync(a.PlayerId, b.PlayerId))
            .Should().Be(RelationshipState.None);

        var send = await svc.SendRequestAsync(a.PlayerId, b.PlayerId);

        (await svc.GetRelationshipAsync(a.PlayerId, b.PlayerId))
            .Should().Be(RelationshipState.RequestSent);
        (await svc.GetRelationshipAsync(b.PlayerId, a.PlayerId))
            .Should().Be(RelationshipState.RequestReceived);

        await svc.AcceptRequestAsync(send.Value!.RequestId, b.PlayerId);

        (await svc.GetRelationshipAsync(a.PlayerId, b.PlayerId))
            .Should().Be(RelationshipState.Friends);
        (await svc.GetRelationshipAsync(b.PlayerId, a.PlayerId))
            .Should().Be(RelationshipState.Friends);
    }

    // ── Rate limits ─────────────────────────────────────────────────────

    [Fact]
    public async Task Max10PendingOutbound_BlocksEleventh()
    {
        var svc = CreateService(out var repo);
        var me = await CreatePlayer(repo);

        for (int i = 0; i < 10; i++)
        {
            var target = await CreatePlayer(repo);
            var result = await svc.SendRequestAsync(me.PlayerId, target.PlayerId);
            result.Success.Should().BeTrue($"request {i + 1} should be allowed");
        }

        var extra = await CreatePlayer(repo);
        var eleventh = await svc.SendRequestAsync(me.PlayerId, extra.PlayerId);
        eleventh.Success.Should().BeFalse();
        eleventh.ErrorCode.Should().Be("FriendRequestRateLimited");
    }

    // ── Listing ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ListFriends_ReturnsMutualFriendsOrderedByDisplayName()
    {
        var svc = CreateService(out var repo);
        var me = await CreatePlayer(repo);

        var zed = await CreatePlayer(repo, "zed_" + Guid.NewGuid().ToString("N")[..8]);
        var ann = await CreatePlayer(repo, "ann_" + Guid.NewGuid().ToString("N")[..8]);
        var mike = await CreatePlayer(repo, "mike_" + Guid.NewGuid().ToString("N")[..8]);

        foreach (var other in new[] { zed, ann, mike })
        {
            var s = await svc.SendRequestAsync(me.PlayerId, other.PlayerId);
            await svc.AcceptRequestAsync(s.Value!.RequestId, other.PlayerId);
        }

        var friends = await svc.ListFriendsAsync(me.PlayerId);
        friends.Select(f => f.DisplayName).Should().ContainInOrder(
            ann.DisplayName, mike.DisplayName, zed.DisplayName);
    }

    [Fact]
    public async Task ListIncomingAndOutgoing_ReturnsOnlyPending()
    {
        var svc = CreateService(out var repo);
        var me = await CreatePlayer(repo);
        var target = await CreatePlayer(repo);

        var send = await svc.SendRequestAsync(me.PlayerId, target.PlayerId);
        (await svc.ListOutgoingRequestsAsync(me.PlayerId))
            .Should().ContainSingle(r => r.RequestId == send.Value!.RequestId);
        (await svc.ListIncomingRequestsAsync(target.PlayerId))
            .Should().ContainSingle(r => r.RequestId == send.Value!.RequestId);

        await svc.DeclineRequestAsync(send.Value!.RequestId, target.PlayerId);
        (await svc.ListIncomingRequestsAsync(target.PlayerId))
            .Should().BeEmpty();
        (await svc.ListOutgoingRequestsAsync(me.PlayerId))
            .Should().BeEmpty();
    }
}
