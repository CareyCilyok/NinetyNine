namespace NinetyNine.Repository.Tests;

/// <summary>
/// xUnit collection definition so all repository test classes share the same
/// MongoDB Testcontainer instance, avoiding the cost of starting a new container
/// per test class.
/// </summary>
[CollectionDefinition(Name)]
public sealed class MongoCollection : ICollectionFixture<MongoFixture>
{
    public const string Name = "Mongo";
}
