using Microsoft.Extensions.Logging;
using NinetyNine.Model;
using NinetyNine.Repository.Repositories;

namespace NinetyNine.Services;

/// <summary>
/// Default <see cref="IFriendService"/> implementation. Enforces every
/// invariant and rate limit from the plan; talks to
/// <see cref="IFriendshipRepository"/>, <see cref="IFriendRequestRepository"/>,
/// and <see cref="IPlayerRepository"/> (the latter for target-exists checks).
/// </summary>
/// <remarks>
/// Rate limits are evaluated at the service layer (not middleware) because
/// they need to count documents across direction, status, and time windows.
/// At this scale (tens of players, low thousands of requests) the listing
/// approach is cheaper and simpler than adding dedicated count queries.
/// </remarks>
public sealed class FriendService(
    IFriendshipRepository friendships,
    IFriendRequestRepository requests,
    IPlayerRepository players,
    ILogger<FriendService> logger) : IFriendService
{
    // Rate-limit thresholds — locked in the plan's Sprint 1 S1.1 acceptance criteria.
    private const int MaxOutboundPending = 10;
    private const int MaxSentPer24Hours = 20;
    private const int MaxSentPerTargetPer30Days = 3;
    private static readonly TimeSpan DeclineCooldown = TimeSpan.FromDays(90);
    private static readonly TimeSpan RequestWindow24h = TimeSpan.FromHours(24);
    private static readonly TimeSpan RequestWindow30d = TimeSpan.FromDays(30);

    public async Task<ServiceResult<FriendRequest>> SendRequestAsync(
        Guid fromPlayerId,
        Guid toPlayerId,
        string? message = null,
        CancellationToken ct = default)
    {
        if (fromPlayerId == toPlayerId)
            return ServiceResult<FriendRequest>.Fail(
                "SelfFriendship", "You cannot send a friend request to yourself.");

        // Target must exist. Constant-time error message so the search
        // surface cannot be used as an enumeration oracle.
        var target = await players.GetByIdAsync(toPlayerId, ct);
        if (target is null)
            return ServiceResult<FriendRequest>.Fail(
                "TargetNotFound", "That player was not found.");

        // Already friends — short-circuit before hitting the request store.
        var existingFriendship = await friendships.GetByPairAsync(fromPlayerId, toPlayerId, ct);
        if (existingFriendship is not null)
            return ServiceResult<FriendRequest>.Fail(
                "AlreadyFriends", "You are already friends with this player.");

        // Pending in either direction → no new request.
        var pendingForward = await requests.GetPendingAsync(fromPlayerId, toPlayerId, ct);
        if (pendingForward is not null)
            return ServiceResult<FriendRequest>.Fail(
                "RequestAlreadyPending", "You already have a pending request to this player.");

        var pendingReverse = await requests.GetPendingAsync(toPlayerId, fromPlayerId, ct);
        if (pendingReverse is not null)
            return ServiceResult<FriendRequest>.Fail(
                "RequestAlreadyPending",
                "This player already sent you a friend request. Accept it instead.");

        // Rate limits.
        var outbound = await requests.ListOutgoingAsync(fromPlayerId, ct: ct);
        var pendingOutbound = outbound.Count(r => r.Status == FriendRequestStatus.Pending);
        if (pendingOutbound >= MaxOutboundPending)
            return ServiceResult<FriendRequest>.Fail(
                "FriendRequestRateLimited",
                $"You already have {MaxOutboundPending} pending friend requests. " +
                "Wait for responses or cancel some before sending more.");

        var now = DateTime.UtcNow;

        var sentLast24h = outbound.Count(r => r.CreatedAt >= now - RequestWindow24h);
        if (sentLast24h >= MaxSentPer24Hours)
            return ServiceResult<FriendRequest>.Fail(
                "FriendRequestRateLimited",
                $"You have sent {MaxSentPer24Hours} friend requests in the last 24 hours. " +
                "Please wait before sending more.");

        var sentToTargetLast30d = outbound.Count(r =>
            r.ToPlayerId == toPlayerId && r.CreatedAt >= now - RequestWindow30d);
        if (sentToTargetLast30d >= MaxSentPerTargetPer30Days)
            return ServiceResult<FriendRequest>.Fail(
                "FriendRequestRateLimited",
                "You have already sent this player the maximum number of requests recently.");

        // 90-day cooldown after a decline — the target opted out; do not
        // let the sender flood them.
        var mostRecentDeclined = outbound
            .Where(r => r.ToPlayerId == toPlayerId && r.Status == FriendRequestStatus.Declined)
            .OrderByDescending(r => r.RespondedAt ?? r.CreatedAt)
            .FirstOrDefault();
        if (mostRecentDeclined is not null)
        {
            var declinedAt = mostRecentDeclined.RespondedAt ?? mostRecentDeclined.CreatedAt;
            if (now - declinedAt < DeclineCooldown)
                return ServiceResult<FriendRequest>.Fail(
                    "FriendRequestCooldown",
                    "This player declined a recent friend request. You can try again later.");
        }

        var request = new FriendRequest
        {
            FromPlayerId = fromPlayerId,
            ToPlayerId = toPlayerId,
            Status = FriendRequestStatus.Pending,
            Message = TrimMessage(message),
            CreatedAt = now,
        };

        try
        {
            await requests.CreateAsync(request, ct);
        }
        catch (MongoDB.Driver.MongoWriteException ex) when (IsDuplicateKey(ex))
        {
            // Raced another request from the same pair through the partial
            // unique index. Treat as a benign duplicate-pending error.
            logger.LogInformation(
                "Race: a pending request from {From} to {To} already exists.",
                fromPlayerId, toPlayerId);
            return ServiceResult<FriendRequest>.Fail(
                "RequestAlreadyPending", "You already have a pending request to this player.");
        }

        logger.LogInformation(
            "Friend request {RequestId} sent from {From} to {To}",
            request.RequestId, fromPlayerId, toPlayerId);

        return ServiceResult<FriendRequest>.Ok(request);
    }

    public async Task<ServiceResult> CancelRequestAsync(
        Guid requestId,
        Guid byPlayerId,
        CancellationToken ct = default)
    {
        var request = await requests.GetByIdAsync(requestId, ct);
        if (request is null)
            return ServiceResult.Fail("RequestNotFound", "That friend request does not exist.");

        if (request.FromPlayerId != byPlayerId)
            return ServiceResult.Fail("NotAuthorized", "Only the sender can cancel a friend request.");

        if (request.Status != FriendRequestStatus.Pending)
            return ServiceResult.Fail(
                "RequestNotPending",
                "That friend request is no longer pending — nothing to cancel.");

        await requests.UpdateStatusAsync(
            requestId, FriendRequestStatus.Cancelled, DateTime.UtcNow, ct);

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<Friendship>> AcceptRequestAsync(
        Guid requestId,
        Guid byPlayerId,
        CancellationToken ct = default)
    {
        var request = await requests.GetByIdAsync(requestId, ct);
        if (request is null)
            return ServiceResult<Friendship>.Fail(
                "RequestNotFound", "That friend request does not exist.");

        if (request.ToPlayerId != byPlayerId)
            return ServiceResult<Friendship>.Fail(
                "NotAuthorized", "Only the recipient can accept a friend request.");

        if (request.Status != FriendRequestStatus.Pending)
            return ServiceResult<Friendship>.Fail(
                "RequestNotPending", "That friend request is no longer pending.");

        // Compensating-idempotent sequence (no Mongo transaction required):
        //   1. Create the Friendship first. The unique index prevents dupes.
        //   2. Flip the request to Accepted. Retried on crash — idempotent.
        // If step 1 fails because the pair is somehow already friends,
        // swallow it and still flip the request so the inbox clears.
        var friendship = Friendship.Create(
            request.FromPlayerId,
            request.ToPlayerId,
            initiatedBy: request.FromPlayerId,
            via: "request");

        try
        {
            await friendships.CreateAsync(friendship, ct);
        }
        catch (MongoDB.Driver.MongoWriteException ex) when (IsDuplicateKey(ex))
        {
            logger.LogInformation(
                "Friendship between {A} and {B} already existed at accept time; " +
                "falling back to the existing edge.",
                friendship.PlayerAId, friendship.PlayerBId);
            friendship = await friendships.GetByPairAsync(
                request.FromPlayerId, request.ToPlayerId, ct)
                ?? friendship;
        }

        await requests.UpdateStatusAsync(
            requestId, FriendRequestStatus.Accepted, DateTime.UtcNow, ct);

        // Best-effort: if there is a reverse-direction Pending request
        // (the other party also clicked "Send"), auto-resolve it.
        var reversePending = await requests.GetPendingAsync(
            request.ToPlayerId, request.FromPlayerId, ct);
        if (reversePending is not null)
        {
            await requests.UpdateStatusAsync(
                reversePending.RequestId, FriendRequestStatus.Accepted, DateTime.UtcNow, ct);
            logger.LogInformation(
                "Auto-resolved reverse pending request {RequestId}", reversePending.RequestId);
        }

        logger.LogInformation(
            "Friend request {RequestId} accepted; friendship {FriendshipId} created",
            requestId, friendship.FriendshipId);

        return ServiceResult<Friendship>.Ok(friendship);
    }

    public async Task<ServiceResult> DeclineRequestAsync(
        Guid requestId,
        Guid byPlayerId,
        CancellationToken ct = default)
    {
        var request = await requests.GetByIdAsync(requestId, ct);
        if (request is null)
            return ServiceResult.Fail("RequestNotFound", "That friend request does not exist.");

        if (request.ToPlayerId != byPlayerId)
            return ServiceResult.Fail(
                "NotAuthorized", "Only the recipient can decline a friend request.");

        if (request.Status != FriendRequestStatus.Pending)
            return ServiceResult.Fail(
                "RequestNotPending", "That friend request is no longer pending.");

        await requests.UpdateStatusAsync(
            requestId, FriendRequestStatus.Declined, DateTime.UtcNow, ct);

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> RemoveFriendAsync(
        Guid playerId,
        Guid otherPlayerId,
        CancellationToken ct = default)
    {
        if (playerId == otherPlayerId)
            return ServiceResult.Fail(
                "SelfFriendship", "You cannot unfriend yourself.");

        await friendships.DeleteAsync(playerId, otherPlayerId, ct);
        return ServiceResult.Ok();
    }

    public async Task<IReadOnlyList<Player>> ListFriendsAsync(
        Guid playerId,
        CancellationToken ct = default)
    {
        var edges = await friendships.ListForPlayerAsync(playerId, ct);
        if (edges.Count == 0)
            return Array.Empty<Player>();

        var otherIds = edges.Select(e => e.OtherParty(playerId)).Distinct().ToList();

        // At Sprint 1 scale (tens of players) N round-trips are cheap and
        // simpler than a batched $in query. Revisit if this ever grows.
        var results = new List<Player>(otherIds.Count);
        foreach (var id in otherIds)
        {
            var p = await players.GetByIdAsync(id, ct);
            if (p is not null) results.Add(p);
        }

        return results
            .OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList()
            .AsReadOnly();
    }

    public async Task<IReadOnlyList<FriendRequest>> ListIncomingRequestsAsync(
        Guid playerId,
        CancellationToken ct = default)
        => await requests.ListIncomingAsync(playerId, FriendRequestStatus.Pending, ct);

    public async Task<IReadOnlyList<FriendRequest>> ListOutgoingRequestsAsync(
        Guid playerId,
        CancellationToken ct = default)
        => await requests.ListOutgoingAsync(playerId, FriendRequestStatus.Pending, ct);

    public async Task<bool> AreFriendsAsync(
        Guid playerAId,
        Guid playerBId,
        CancellationToken ct = default)
    {
        if (playerAId == playerBId) return false;
        var edge = await friendships.GetByPairAsync(playerAId, playerBId, ct);
        return edge is not null;
    }

    public async Task<RelationshipState> GetRelationshipAsync(
        Guid viewerId,
        Guid targetId,
        CancellationToken ct = default)
    {
        if (viewerId == targetId) return RelationshipState.Self;

        if (await AreFriendsAsync(viewerId, targetId, ct))
            return RelationshipState.Friends;

        if (await requests.GetPendingAsync(viewerId, targetId, ct) is not null)
            return RelationshipState.RequestSent;

        if (await requests.GetPendingAsync(targetId, viewerId, ct) is not null)
            return RelationshipState.RequestReceived;

        return RelationshipState.None;
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static string? TrimMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;
        var trimmed = message.Trim();
        return trimmed.Length > 280 ? trimmed[..280] : trimmed;
    }

    private static bool IsDuplicateKey(MongoDB.Driver.MongoWriteException ex)
        => ex.WriteError?.Category == MongoDB.Driver.ServerErrorCategory.DuplicateKey;
}
