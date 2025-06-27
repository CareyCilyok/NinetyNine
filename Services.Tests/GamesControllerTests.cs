using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NinetyNine.Model;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace NinetyNine.Services.Tests;

public class GamesControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public GamesControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetGames_ShouldReturnEmptyList_WhenNoGamesExist()
    {
        // Act
        var response = await _client.GetAsync("/api/games");

        // Assert
        response.Should().BeSuccessful();
        
        var games = await response.Content.ReadFromJsonAsync<List<Game>>();
        games.Should().NotBeNull();
        games!.Should().BeEmpty();
    }

    [Fact]
    public async Task PostGame_ShouldCreateGame_WhenValidDataProvided()
    {
        // Arrange
        var newGame = new Game
        {
            CreatedDate = DateTime.UtcNow
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/games", newGame);

        // Assert
        response.Should().BeSuccessful();
        
        var createdGame = await response.Content.ReadFromJsonAsync<Game>();
        createdGame.Should().NotBeNull();
        createdGame!.Id.Should().BeGreaterThan(0);
        createdGame.CreatedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task GetGameById_ShouldReturnGame_WhenGameExists()
    {
        // Arrange - Create a game first
        var newGame = new Game { CreatedDate = DateTime.UtcNow };
        var createResponse = await _client.PostAsJsonAsync("/api/games", newGame);
        var createdGame = await createResponse.Content.ReadFromJsonAsync<Game>();

        // Act
        var response = await _client.GetAsync($"/api/games/{createdGame!.Id}");

        // Assert
        response.Should().BeSuccessful();
        
        var retrievedGame = await response.Content.ReadFromJsonAsync<Game>();
        retrievedGame.Should().NotBeNull();
        retrievedGame!.Id.Should().Be(createdGame.Id);
    }

    [Fact]
    public async Task GetGameById_ShouldReturnNotFound_WhenGameDoesNotExist()
    {
        // Act
        var response = await _client.GetAsync("/api/games/999999");

        // Assert
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteGame_ShouldRemoveGame_WhenGameExists()
    {
        // Arrange - Create a game first
        var newGame = new Game { CreatedDate = DateTime.UtcNow };
        var createResponse = await _client.PostAsJsonAsync("/api/games", newGame);
        var createdGame = await createResponse.Content.ReadFromJsonAsync<Game>();

        // Act
        var deleteResponse = await _client.DeleteAsync($"/api/games/{createdGame!.Id}");

        // Assert
        deleteResponse.Should().BeSuccessful();
        
        // Verify game is deleted
        var getResponse = await _client.GetAsync($"/api/games/{createdGame.Id}");
        getResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }
}