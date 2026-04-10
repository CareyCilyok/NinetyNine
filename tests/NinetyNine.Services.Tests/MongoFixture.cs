using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using NinetyNine.Repository;
using Testcontainers.MongoDb;

namespace NinetyNine.Services.Tests;

/// <summary>
/// xUnit class fixture that starts a real MongoDB container via Testcontainers.
/// Shared across all test classes in the <see cref="MongoCollection"/> collection.
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

    /// <summary>Returns a <see cref="INinetyNineDbContext"/> backed by a fresh database.</summary>
    public INinetyNineDbContext CreateDbContext()
    {
        var dbName = $"ninetynine_svc_{Guid.NewGuid():N}";
        var settings = Options.Create(new MongoDbSettings
        {
            ConnectionString = _container.GetConnectionString(),
            DatabaseName = dbName
        });
        return new NinetyNineDbContext(Client, settings);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
