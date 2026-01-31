using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NinetyNine.Model;
using NinetyNine.Repository;
using Xunit;

namespace NinetyNine.Repository.Tests;

public class LocalContextTests : IDisposable
{
    private readonly SqliteTestHelper _helper;
    private readonly LocalContext _context;

    public LocalContextTests()
    {
        _helper = new SqliteTestHelper();
        _context = _helper.CreateContext();
    }

    [Fact]
    public void LocalContext_CanAddAndRetrieveGame()
    {
        // Arrange
        var game = new Game
        {
            GameId = Guid.NewGuid(),
            WhenPlayed = DateTime.UtcNow,
            CompletedAt = null,
            GameState = GameState.InProgress
        };

        // Act
        _context.Games.Add(game);
        _context.SaveChanges();

        var retrievedGame = _context.Games.FirstOrDefault();

        // Assert
        retrievedGame.Should().NotBeNull();
        retrievedGame!.GameId.Should().NotBeEmpty();
        retrievedGame.WhenPlayed.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void LocalContext_CanAddAndRetrievePlayer()
    {
        // Arrange
        var player = new Player
        {
            PlayerId = Guid.NewGuid(),
            FirstName = "Test",
            LastName = "Player",
            EmailAddress = "test@example.com"
        };

        // Act
        _context.Players.Add(player);
        _context.SaveChanges();

        var retrievedPlayer = _context.Players.FirstOrDefault();

        // Assert
        retrievedPlayer.Should().NotBeNull();
        retrievedPlayer!.PlayerId.Should().NotBeEmpty();
        retrievedPlayer.Name.Should().Be("Test Player");
        retrievedPlayer.EmailAddress.Should().Be("test@example.com");
    }

    [Fact]
    public void LocalContext_CanAddAndRetrieveVenue()
    {
        // Arrange
        var venue = new Venue
        {
            VenueId = Guid.NewGuid(),
            Name = "Test Venue",
            Address = "123 Test St"
        };

        // Act
        _context.Venues.Add(venue);
        _context.SaveChanges();

        var retrievedVenue = _context.Venues.FirstOrDefault();

        // Assert
        retrievedVenue.Should().NotBeNull();
        retrievedVenue!.VenueId.Should().NotBeEmpty();
        retrievedVenue.Name.Should().Be("Test Venue");
        retrievedVenue.Address.Should().Be("123 Test St");
    }

    [Fact]
    public void LocalContext_CanHandleGameWithFrames()
    {
        // Arrange
        var game = new Game
        {
            GameId = Guid.NewGuid(),
            WhenPlayed = DateTime.UtcNow,
            GameState = GameState.InProgress
        };

        game.InitializeFrames();

        // Complete first two frames
        game.Frames[0].BreakBonus = 1;
        game.Frames[0].BallCount = 9;
        game.Frames[0].CompleteFrame(0);

        game.Frames[1].BreakBonus = 0;
        game.Frames[1].BallCount = 8;
        game.Frames[1].CompleteFrame(10);

        // Act
        _context.Games.Add(game);
        _context.SaveChanges();

        // Use a fresh context to ensure we're querying from the database
        using var freshContext = _helper.CreateFreshContext();
        var retrievedGame = freshContext.Games
            .Include(g => g.Frames)
            .FirstOrDefault(g => g.GameId == game.GameId);

        // Assert
        retrievedGame.Should().NotBeNull();
        retrievedGame!.Frames.Should().HaveCount(9);
        retrievedGame.TotalScore.Should().Be(18); // (1+9) + (0+8) = 18

        var firstFrame = retrievedGame.Frames.First(f => f.FrameNumber == 1);
        firstFrame.FrameScore.Should().Be(10);
        firstFrame.BallCount.Should().Be(9);
        firstFrame.BreakBonus.Should().Be(1);
    }

    [Fact]
    public void LocalContext_CanUpdateGame()
    {
        // Arrange
        var game = new Game
        {
            GameId = Guid.NewGuid(),
            WhenPlayed = DateTime.UtcNow,
            GameState = GameState.NotStarted
        };

        _context.Games.Add(game);
        _context.SaveChanges();

        // Act
        game.GameState = GameState.InProgress;
        _context.SaveChanges();

        // Use fresh context to verify persistence
        using var freshContext = _helper.CreateFreshContext();
        var retrievedGame = freshContext.Games.FirstOrDefault(g => g.GameId == game.GameId);

        // Assert
        retrievedGame.Should().NotBeNull();
        retrievedGame!.GameState.Should().Be(GameState.InProgress);
    }

    [Fact]
    public void LocalContext_CanDeleteGame()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var game = new Game
        {
            GameId = gameId,
            WhenPlayed = DateTime.UtcNow
        };

        _context.Games.Add(game);
        _context.SaveChanges();

        // Act
        _context.Games.Remove(game);
        _context.SaveChanges();

        // Use fresh context to verify deletion
        using var freshContext = _helper.CreateFreshContext();
        var retrievedGame = freshContext.Games.FirstOrDefault(g => g.GameId == gameId);

        // Assert
        retrievedGame.Should().BeNull();
    }

    [Fact]
    public void LocalContext_DeleteGameCascadesToFrames()
    {
        // Arrange
        var game = new Game
        {
            GameId = Guid.NewGuid(),
            WhenPlayed = DateTime.UtcNow,
            GameState = GameState.InProgress
        };

        game.InitializeFrames();

        _context.Games.Add(game);
        _context.SaveChanges();

        // Verify frames were added
        var frameCount = _context.Frames.Count(f => f.GameId == game.GameId);
        frameCount.Should().Be(9);

        // Act - delete the game
        _context.Games.Remove(game);
        _context.SaveChanges();

        // Assert - frames should also be deleted (cascade)
        using var freshContext = _helper.CreateFreshContext();
        var remainingFrames = freshContext.Frames.Count(f => f.GameId == game.GameId);
        remainingFrames.Should().Be(0);
    }

    public void Dispose()
    {
        _context.Dispose();
        _helper.Dispose();
    }
}
