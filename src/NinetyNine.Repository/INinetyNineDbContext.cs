using MongoDB.Driver;
using NinetyNine.Model;

namespace NinetyNine.Repository;

/// <summary>
/// Provides access to the MongoDB collections used by the NinetyNine application.
/// </summary>
public interface INinetyNineDbContext
{
    IMongoCollection<Player> Players { get; }
    IMongoCollection<Venue> Venues { get; }
    IMongoCollection<Game> Games { get; }
    IMongoDatabase Database { get; }
}
