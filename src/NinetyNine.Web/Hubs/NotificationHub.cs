using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using NinetyNine.Web.Auth;

namespace NinetyNine.Web.Hubs;

/// <summary>
/// SignalR hub for real-time notifications. Authenticated connections
/// only — the hub resolves the player's Guid from claims on connect
/// and registers it with the <see cref="IHubConnectionTracker"/>.
/// <para>
/// Server → client methods:
/// <list type="bullet">
///   <item><c>ReceiveUnreadCount(long count)</c> — unread notification badge update</item>
///   <item><c>ReceiveLeaderboardUpdate()</c> — signals the leaderboard page to refresh</item>
/// </list>
/// </para>
/// <para>See <c>docs/plans/v2-roadmap.md</c> Sprint 8 S8.1.</para>
/// </summary>
[Authorize]
public sealed class NotificationHub(
    IHubConnectionTracker tracker,
    ILogger<NotificationHub> logger) : Hub
{
    public override Task OnConnectedAsync()
    {
        var playerIdClaim = Context.User?.FindFirst(ClaimNames.PlayerId)?.Value;
        if (Guid.TryParse(playerIdClaim, out var playerId))
        {
            tracker.Register(playerId, Context.ConnectionId);
            logger.LogDebug(
                "SignalR connected: player {PlayerId}, connection {ConnectionId}",
                playerId, Context.ConnectionId);
        }
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        var playerIdClaim = Context.User?.FindFirst(ClaimNames.PlayerId)?.Value;
        if (Guid.TryParse(playerIdClaim, out var playerId))
        {
            tracker.Unregister(playerId, Context.ConnectionId);
            logger.LogDebug(
                "SignalR disconnected: player {PlayerId}, connection {ConnectionId}",
                playerId, Context.ConnectionId);
        }
        return base.OnDisconnectedAsync(exception);
    }
}
