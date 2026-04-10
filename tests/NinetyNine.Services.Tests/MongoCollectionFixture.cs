namespace NinetyNine.Services.Tests;

[CollectionDefinition(Name)]
public sealed class MongoCollection : ICollectionFixture<MongoFixture>
{
    public const string Name = "ServicesMongo";
}
