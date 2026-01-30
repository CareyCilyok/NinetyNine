using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NinetyNine.Model;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace NinetyNine.Services.Tests;

public class GamesControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private const string ApiVersion = "0.0";

    public GamesControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    private async Task<string> GetAuthTokenAsync()
    {
        var loginRequest = new { Email = "test@example.com", Password = "password123" };
        var response = await _client.PostAsJsonAsync($"/api/{ApiVersion}/auth/login", loginRequest);
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return result?.AccessToken ?? string.Empty;
    }

    private void SetAuthHeader(string token)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task GetGames_ShouldReturnSuccessWithAuth()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        SetAuthHeader(token);

        // Act
        var response = await _client.GetAsync($"/api/{ApiVersion}/games");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetGames_ShouldReturnUnauthorized_WhenNoAuth()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.GetAsync($"/api/{ApiVersion}/games");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostGame_ShouldCreateGame_WhenValidDataProvided()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        SetAuthHeader(token);

        var newGame = new Game
        {
            GameId = Guid.NewGuid(),
            WhenPlayed = DateTime.UtcNow,
            GameState = GameState.NotStarted
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/{ApiVersion}/games", newGame);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdGame = await response.Content.ReadFromJsonAsync<Game>();
        createdGame.Should().NotBeNull();
        createdGame!.GameId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetGameById_ShouldReturnGame_WhenGameExists()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        SetAuthHeader(token);

        var newGame = new Game
        {
            GameId = Guid.NewGuid(),
            WhenPlayed = DateTime.UtcNow
        };
        var createResponse = await _client.PostAsJsonAsync($"/api/{ApiVersion}/games", newGame);
        var createdGame = await createResponse.Content.ReadFromJsonAsync<Game>();

        // Act
        var response = await _client.GetAsync($"/api/{ApiVersion}/games/{createdGame!.GameId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var retrievedGame = await response.Content.ReadFromJsonAsync<Game>();
        retrievedGame.Should().NotBeNull();
        retrievedGame!.GameId.Should().Be(createdGame.GameId);
    }

    [Fact]
    public async Task GetGameById_ShouldReturnNotFound_WhenGameDoesNotExist()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        SetAuthHeader(token);

        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/{ApiVersion}/games/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteGame_ShouldRemoveGame_WhenGameExists()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        SetAuthHeader(token);

        var newGame = new Game
        {
            GameId = Guid.NewGuid(),
            WhenPlayed = DateTime.UtcNow
        };
        var createResponse = await _client.PostAsJsonAsync($"/api/{ApiVersion}/games", newGame);
        var createdGame = await createResponse.Content.ReadFromJsonAsync<Game>();

        // Act
        var deleteResponse = await _client.DeleteAsync($"/api/{ApiVersion}/games/{createdGame!.GameId}");

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify game is deleted
        var getResponse = await _client.GetAsync($"/api/{ApiVersion}/games/{createdGame.GameId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateGame_ShouldModifyGame_WhenValidDataProvided()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        SetAuthHeader(token);

        var newGame = new Game
        {
            GameId = Guid.NewGuid(),
            WhenPlayed = DateTime.UtcNow,
            GameState = GameState.NotStarted
        };
        var createResponse = await _client.PostAsJsonAsync($"/api/{ApiVersion}/games", newGame);
        var createdGame = await createResponse.Content.ReadFromJsonAsync<Game>();

        // Modify the game
        createdGame!.GameState = GameState.InProgress;

        // Act
        var updateResponse = await _client.PutAsJsonAsync($"/api/{ApiVersion}/games/{createdGame.GameId}", createdGame);

        // Assert
        updateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private class LoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
        public string TokenType { get; set; } = string.Empty;
    }
}
