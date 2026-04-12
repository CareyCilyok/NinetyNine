using System.Collections.Concurrent;

namespace NinetyNine.Web.Hubs;

/// <summary>
/// In-process connection tracker using <see cref="ConcurrentDictionary"/>.
/// Adequate for single-server deployments. If horizontal scaling is
/// added, replace with a Redis-backed implementation (see AD-6).
/// </summary>
public sealed class HubConnectionTracker : IHubConnectionTracker
{
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _connections = new();
    private readonly object _lock = new();

    public void Register(Guid playerId, string connectionId)
    {
        lock (_lock)
        {
            if (!_connections.TryGetValue(playerId, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                _connections[playerId] = set;
            }
            set.Add(connectionId);
        }
    }

    public void Unregister(Guid playerId, string connectionId)
    {
        lock (_lock)
        {
            if (_connections.TryGetValue(playerId, out var set))
            {
                set.Remove(connectionId);
                if (set.Count == 0)
                    _connections.TryRemove(playerId, out _);
            }
        }
    }

    public IReadOnlyCollection<string> GetConnections(Guid playerId)
    {
        lock (_lock)
        {
            return _connections.TryGetValue(playerId, out var set)
                ? set.ToArray()
                : Array.Empty<string>();
        }
    }

    public IReadOnlyCollection<Guid> GetConnectedPlayerIds()
    {
        lock (_lock)
        {
            return _connections.Keys.ToArray();
        }
    }
}
