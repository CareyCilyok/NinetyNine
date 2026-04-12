namespace NinetyNine.Web.Hubs;

/// <summary>
/// Tracks which SignalR connection IDs belong to which player.
/// Singleton — lives for the application lifetime. Supports
/// multiple tabs per player (one player → many connections).
/// </summary>
public interface IHubConnectionTracker
{
    void Register(Guid playerId, string connectionId);
    void Unregister(Guid playerId, string connectionId);

    /// <summary>
    /// Returns all connection IDs for a player, or empty if none connected.
    /// </summary>
    IReadOnlyCollection<string> GetConnections(Guid playerId);

    /// <summary>
    /// Returns all player IDs that have at least one active connection.
    /// </summary>
    IReadOnlyCollection<Guid> GetConnectedPlayerIds();
}
