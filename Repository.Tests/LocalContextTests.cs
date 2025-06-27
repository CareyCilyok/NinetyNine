using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NinetyNine.Model;
using NinetyNine.Repository;
using Xunit;

namespace NinetyNine.Repository.Tests;

public class LocalContextTests : IDisposable
{
    private readonly LocalContext _context;

    public LocalContextTests()
    {
        var options = new DbContextOptionsBuilder<LocalContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new LocalContext(options);
    }

    [Fact]
    public void LocalContext_CanAddAndRetrieveGame()
    {
        // Arrange
        var game = new Game
        {
            CreatedDate = DateTime.UtcNow,
            CompletedDate = null
        };

        // Act
        _context.Games.Add(game);
        _context.SaveChanges();

        var retrievedGame = _context.Games.FirstOrDefault();

        // Assert
        retrievedGame.Should().NotBeNull();
        retrievedGame!.Id.Should().BeGreaterThan(0);
        retrievedGame.CreatedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void LocalContext_CanAddAndRetrievePlayer()
    {
        // Arrange
        var player = new Player
        {
            Name = "Test Player",
            Email = "test@example.com"
        };

        // Act
        _context.Players.Add(player);
        _context.SaveChanges();

        var retrievedPlayer = _context.Players.FirstOrDefault();

        // Assert
        retrievedPlayer.Should().NotBeNull();
        retrievedPlayer!.Id.Should().BeGreaterThan(0);
        retrievedPlayer.Name.Should().Be("Test Player");
        retrievedPlayer.Email.Should().Be("test@example.com");
    }

    [Fact]
    public void LocalContext_CanAddAndRetrieveVenue()
    {
        // Arrange
        var venue = new Venue
        {
            Name = "Test Venue",
            Address = "123 Test St"
        };

        // Act
        _context.Venues.Add(venue);
        _context.SaveChanges();

        var retrievedVenue = _context.Venues.FirstOrDefault();

        // Assert
        retrievedVenue.Should().NotBeNull();
        retrievedVenue!.Id.Should().BeGreaterThan(0);
        retrievedVenue.Name.Should().Be("Test Venue");
        retrievedVenue.Address.Should().Be("123 Test St");
    }

    [Fact]
    public void LocalContext_CanHandleGameWithFrames()
    {
        // Arrange
        var game = new Game
        {
            CreatedDate = DateTime.UtcNow
        };
        
        var frame1 = new Frame { Score = 10, BallCount = 5, BreakBonus = true };
        var frame2 = new Frame { Score = 8, BallCount = 4, BreakBonus = false };
        
        game.Frames.Add(frame1);
        game.Frames.Add(frame2);

        // Act
        _context.Games.Add(game);
        _context.SaveChanges();

        var retrievedGame = _context.Games
            .Include(g => g.Frames)
            .FirstOrDefault();

        // Assert
        retrievedGame.Should().NotBeNull();
        retrievedGame!.Frames.Should().HaveCount(2);
        retrievedGame.TotalScore.Should().Be(18);
        
        var firstFrame = retrievedGame.Frames.First();
        firstFrame.Score.Should().Be(10);
        firstFrame.BallCount.Should().Be(5);
        firstFrame.BreakBonus.Should().BeTrue();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}