using Microsoft.Extensions.Options;
using MongoDB.Driver;
using NinetyNine.Model;

namespace NinetyNine.Repository;

/// <summary>
/// Concrete MongoDB database context. Creates collections and ensures
/// all required indexes exist on construction (idempotent).
/// </summary>
public sealed class NinetyNineDbContext : INinetyNineDbContext
{
    private readonly IMongoDatabase _database;

    public NinetyNineDbContext(IMongoClient client, IOptions<MongoDbSettings> settings)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(settings);

        _database = client.GetDatabase(settings.Value.DatabaseName);
        EnsureIndexes();
    }

    public IMongoCollection<Player> Players =>
        _database.GetCollection<Player>("players");

    public IMongoCollection<Venue> Venues =>
        _database.GetCollection<Venue>("venues");

    public IMongoCollection<Game> Games =>
        _database.GetCollection<Game>("games");

    public IMongoDatabase Database => _database;

    /// <summary>
    /// Creates all required indexes idempotently. MongoDB ignores duplicate
    /// index-creation requests for identical definitions.
    /// </summary>
    private void EnsureIndexes()
    {
        EnsurePlayerIndexes();
        EnsureVenueIndexes();
        EnsureGameIndexes();
    }

    private void EnsurePlayerIndexes()
    {
        var players = Players;
        var indexModels = new List<CreateIndexModel<Player>>
        {
            // Unique index on displayName (case-sensitive)
            new(Builders<Player>.IndexKeys.Ascending(p => p.DisplayName),
                new CreateIndexOptions { Unique = true, Name = "idx_players_displayName_unique" }),

            // Unique compound index on linkedIdentities array fields
            new(Builders<Player>.IndexKeys
                    .Ascending("linkedIdentities.provider")
                    .Ascending("linkedIdentities.providerUserId"),
                new CreateIndexOptions
                {
                    Unique = true,
                    Sparse = true,
                    Name = "idx_players_linkedIdentities_unique"
                })
        };

        players.Indexes.CreateMany(indexModels);
    }

    private void EnsureVenueIndexes()
    {
        var venues = Venues;
        venues.Indexes.CreateOne(
            new CreateIndexModel<Venue>(
                Builders<Venue>.IndexKeys.Ascending(v => v.Name),
                new CreateIndexOptions { Name = "idx_venues_name" }));
    }

    private void EnsureGameIndexes()
    {
        var games = Games;
        var indexModels = new List<CreateIndexModel<Game>>
        {
            new(Builders<Game>.IndexKeys.Ascending(g => g.PlayerId),
                new CreateIndexOptions { Name = "idx_games_playerId" }),

            new(Builders<Game>.IndexKeys.Descending(g => g.WhenPlayed),
                new CreateIndexOptions { Name = "idx_games_whenPlayed_desc" }),

            new(Builders<Game>.IndexKeys.Ascending(g => g.GameState),
                new CreateIndexOptions { Name = "idx_games_gameState" })
        };

        games.Indexes.CreateMany(indexModels);
    }
}
