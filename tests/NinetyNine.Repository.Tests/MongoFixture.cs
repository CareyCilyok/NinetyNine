using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using NinetyNine.Repository;
using Testcontainers.MongoDb;

namespace NinetyNine.Repository.Tests;

/// <summary>
/// xUnit class fixture that starts a real MongoDB container via Testcontainers.
/// Shared across all test classes in the <see cref="MongoCollection"/> collection.
/// Each test class gets a fresh database to avoid cross-test pollution.
/// </summary>
public sealed class MongoFixture : IAsyncLifetime
{
    private readonly MongoDbContainer _container = new MongoDbBuilder()
        .WithImage("mongo:7")
        .Build();

    public IMongoClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        BsonConfiguration.Register();
        Client = new MongoClient(_container.GetConnectionString());
    }

    /// <summary>
    /// Returns a fresh database with a unique name so test classes are isolated.
    /// Indexes are created by <see cref="NinetyNineDbContext"/>.
    /// </summary>
    public IMongoDatabase GetFreshDatabase()
    {
        var dbName = $"ninetynine_test_{Guid.NewGuid():N}";
        return Client.GetDatabase(dbName);
    }

    /// <summary>
    /// Creates a <see cref="INinetyNineDbContext"/> backed by a fresh database.
    /// </summary>
    public INinetyNineDbContext CreateDbContext()
    {
        var db = GetFreshDatabase();
        var settings = Options.Create(new MongoDbSettings
        {
            ConnectionString = _container.GetConnectionString(),
            DatabaseName = db.DatabaseNamespace.DatabaseName
        });
        return new NinetyNineDbContext(Client, settings);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
