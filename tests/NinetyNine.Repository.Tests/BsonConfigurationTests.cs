using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using NinetyNine.Model;
using NinetyNine.Repository;

namespace NinetyNine.Repository.Tests;

[Collection(MongoCollection.Name)]
[Trait("Category", "Integration")]
public class BsonConfigurationTests(MongoFixture fixture)
{
    private readonly IMongoDatabase _db = fixture.GetFreshDatabase();

    [Fact]
    public async Task Player_RoundTrip_PreservesAllFields()
    {
        var collection = _db.GetCollection<Player>("players");
        var player = new Player
        {
            PlayerId = Guid.NewGuid(),
            DisplayName = "TestPlayer",
            EmailAddress = "test@example.com",
            PhoneNumber = "555-1234",
            FirstName = "Test",
            MiddleName = "M",
            LastName = "Player",
            Visibility = new ProfileVisibility { EmailAddress = true, RealName = true },
            Avatar = new AvatarRef
            {
                StorageKey = "507f1f77bcf86cd799439011",
                ContentType = "image/png",
                WidthPx = 512,
                HeightPx = 512,
                SizeBytes = 12345
            },
            LinkedIdentities =
            [
                new LinkedIdentity { Provider = "Google", ProviderUserId = "google-sub-123" }
            ]
        };

        await collection.InsertOneAsync(player);
        var retrieved = await collection.Find(Builders<Player>.Filter.Eq(p => p.PlayerId, player.PlayerId))
            .FirstOrDefaultAsync();

        retrieved.Should().NotBeNull();
        retrieved!.PlayerId.Should().Be(player.PlayerId);
        retrieved.DisplayName.Should().Be("TestPlayer");
        retrieved.EmailAddress.Should().Be("test@example.com");
        retrieved.PhoneNumber.Should().Be("555-1234");
        retrieved.FirstName.Should().Be("Test");
        retrieved.MiddleName.Should().Be("M");
        retrieved.LastName.Should().Be("Player");
        retrieved.Visibility.EmailAddress.Should().BeTrue();
        retrieved.Visibility.RealName.Should().BeTrue();
        retrieved.Avatar.Should().NotBeNull();
        retrieved.Avatar!.StorageKey.Should().Be("507f1f77bcf86cd799439011");
        retrieved.Avatar.ContentType.Should().Be("image/png");
        retrieved.LinkedIdentities.Should().HaveCount(1);
        retrieved.LinkedIdentities[0].Provider.Should().Be("Google");
        retrieved.LinkedIdentities[0].ProviderUserId.Should().Be("google-sub-123");
    }

    [Fact]
    public async Task Game_RoundTrip_PreservesAllFieldsIncludingFrames()
    {
        var collection = _db.GetCollection<Game>("games");
        var game = new Game
        {
            GameId = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(),
            VenueId = Guid.NewGuid(),
            TableSize = TableSize.SevenFoot,
            Notes = "test game"
        };
        game.InitializeFrames();  // sets GameState = InProgress
        game.CurrentFrameNumber = 2;
        // Complete frame 1 so we have embedded frame data
        var frame1 = game.Frames[0];
        frame1.BreakBonus = 1;
        frame1.BallCount = 7;
        frame1.IsCompleted = true;
        frame1.IsActive = false;
        frame1.RunningTotal = 8;
        frame1.CompletedAt = DateTime.UtcNow;

        await collection.InsertOneAsync(game);
        var retrieved = await collection.Find(Builders<Game>.Filter.Eq(g => g.GameId, game.GameId))
            .FirstOrDefaultAsync();

        retrieved.Should().NotBeNull();
        retrieved!.GameId.Should().Be(game.GameId);
        retrieved.PlayerId.Should().Be(game.PlayerId);
        retrieved.TableSize.Should().Be(TableSize.SevenFoot);
        retrieved.GameState.Should().Be(GameState.InProgress);
        retrieved.CurrentFrameNumber.Should().Be(2);
        retrieved.Notes.Should().Be("test game");
        retrieved.Frames.Should().HaveCount(9);
        retrieved.Frames[0].BreakBonus.Should().Be(1);
        retrieved.Frames[0].BallCount.Should().Be(7);
        retrieved.Frames[0].RunningTotal.Should().Be(8);
        retrieved.Frames[0].IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task Game_ComputedProperties_NotStoredInDocument()
    {
        var collection = _db.GetCollection<Game>("games");
        var bsonCollection = _db.GetCollection<BsonDocument>("games");

        var game = new Game { GameId = Guid.NewGuid(), PlayerId = Guid.NewGuid(), VenueId = Guid.NewGuid() };
        game.InitializeFrames();
        await collection.InsertOneAsync(game);

        var raw = await bsonCollection.Find(
            Builders<BsonDocument>.Filter.Eq("_id", game.GameId.ToString()))
            .FirstOrDefaultAsync();

        raw.Should().NotBeNull();
        // Computed properties must not be persisted
        raw!.Contains("totalScore").Should().BeFalse("totalScore is a computed property");
        raw.Contains("runningTotal").Should().BeFalse("runningTotal is a computed property");
        raw.Contains("isInProgress").Should().BeFalse("isInProgress is a computed property");
        raw.Contains("isCompleted").Should().BeFalse("isCompleted is a computed property");
        raw.Contains("averageScore").Should().BeFalse("averageScore is a computed property");
        raw.Contains("bestFrame").Should().BeFalse("bestFrame is a computed property");
        raw.Contains("perfectFrames").Should().BeFalse("perfectFrames is a computed property");
        raw.Contains("isPerfectGame").Should().BeFalse("isPerfectGame is a computed property");
    }

    [Fact]
    public async Task Guid_SerializedAsString()
    {
        var collection = _db.GetCollection<Player>("players");
        var bsonCollection = _db.GetCollection<BsonDocument>("players");

        var playerId = Guid.NewGuid();
        var player = new Player { PlayerId = playerId, DisplayName = "GuidTest" };
        await collection.InsertOneAsync(player);

        var raw = await bsonCollection.Find(
            Builders<BsonDocument>.Filter.Eq("displayName", "GuidTest"))
            .FirstOrDefaultAsync();

        raw.Should().NotBeNull();
        // _id should be the string representation of the GUID
        var idField = raw!["_id"];
        idField.BsonType.Should().Be(BsonType.String, "GUIDs are configured to serialize as strings");
        idField.AsString.Should().Be(playerId.ToString());
    }

    [Fact]
    public async Task Enums_SerializedAsString()
    {
        var collection = _db.GetCollection<Game>("games");
        var bsonCollection = _db.GetCollection<BsonDocument>("games");

        var game = new Game
        {
            GameId = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(),
            VenueId = Guid.NewGuid(),
            TableSize = TableSize.SevenFoot
        };
        game.InitializeFrames();  // sets GameState = InProgress
        await collection.InsertOneAsync(game);

        var raw = await bsonCollection.Find(
            Builders<BsonDocument>.Filter.Eq("_id", game.GameId.ToString()))
            .FirstOrDefaultAsync();

        raw.Should().NotBeNull();
        raw!["gameState"].BsonType.Should().Be(BsonType.String);
        raw["gameState"].AsString.Should().Be("InProgress");
        raw["tableSize"].BsonType.Should().Be(BsonType.String);
        raw["tableSize"].AsString.Should().Be("SevenFoot");
    }
}
