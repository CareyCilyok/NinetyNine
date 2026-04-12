using Microsoft.AspNetCore.SignalR;
using NinetyNine.Services;
using NinetyNine.Web.Hubs;

namespace NinetyNine.Web.Services;

/// <summary>
/// Background service that polls for notification changes and pushes
/// updates to connected SignalR clients. Runs on a configurable
/// interval (default 30 seconds).
/// <para>
/// For each connected player, queries the unread notification count
/// and sends <c>ReceiveUnreadCount</c> if the count has changed
/// since the last poll. Also detects leaderboard changes and
/// broadcasts <c>ReceiveLeaderboardUpdate</c> to all connected clients.
/// </para>
/// <para>See <c>docs/plans/v2-roadmap.md</c> Sprint 8 S8.3.</para>
/// </summary>
public sealed class NotificationPollerService(
    IServiceScopeFactory scopeFactory,
    IHubContext<NotificationHub> hubContext,
    IHubConnectionTracker tracker,
    IConfiguration configuration,
    ILogger<NotificationPollerService> logger) : BackgroundService
{
    private readonly Dictionary<Guid, long> _lastKnownCounts = new();
    private DateTime _lastLeaderboardCheck = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = configuration.GetValue("SignalR:PollIntervalSeconds", 30);
        var interval = TimeSpan.FromSeconds(intervalSeconds);

        logger.LogInformation(
            "NotificationPollerService started with {Interval}s interval", intervalSeconds);

        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await PollNotificationsAsync(stoppingToken);
                await PollLeaderboardAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "NotificationPollerService tick failed");
            }
        }

        logger.LogInformation("NotificationPollerService stopped");
    }

    private async Task PollNotificationsAsync(CancellationToken ct)
    {
        var connectedPlayers = tracker.GetConnectedPlayerIds();
        if (connectedPlayers.Count == 0) return;

        using var scope = scopeFactory.CreateScope();
        var notifService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        foreach (var playerId in connectedPlayers)
        {
            var count = await notifService.CountUnreadAsync(playerId, ct);
            var changed = !_lastKnownCounts.TryGetValue(playerId, out var last) || last != count;

            if (changed)
            {
                _lastKnownCounts[playerId] = count;
                var connections = tracker.GetConnections(playerId);
                foreach (var connId in connections)
                {
                    await hubContext.Clients.Client(connId)
                        .SendAsync("ReceiveUnreadCount", count, ct);
                }
            }
        }

        // Clean up stale entries for disconnected players.
        var stale = _lastKnownCounts.Keys
            .Where(id => !connectedPlayers.Contains(id))
            .ToList();
        foreach (var id in stale)
            _lastKnownCounts.Remove(id);
    }

    private async Task PollLeaderboardAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var gameRepo = scope.ServiceProvider
            .GetRequiredService<NinetyNine.Repository.Repositories.IGameRepository>();

        var recent = await gameRepo.GetRecentAsync(limit: 1, ct);
        if (recent.Count == 0) return;

        var latestTime = recent[0].CompletedAt ?? recent[0].WhenPlayed;
        if (latestTime > _lastLeaderboardCheck)
        {
            _lastLeaderboardCheck = latestTime;
            await hubContext.Clients.All.SendAsync("ReceiveLeaderboardUpdate", ct);
        }
    }
}
