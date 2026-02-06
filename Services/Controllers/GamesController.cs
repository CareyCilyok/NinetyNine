using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NinetyNine.Model;

namespace Services.Controllers;

[ApiController]
[ApiVersion("0.0")]
[Route("api/{v:apiVersion}/[controller]")]
[Authorize]
public class GamesController : ControllerBase
{
    private readonly NinetyNineContext _context;
    private readonly ILogger<GamesController> _logger;

    public GamesController(NinetyNineContext dbContext, ILogger<GamesController> logger)
    {
        _context = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Get all games with pagination, filtering, and sorting
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<Game>>> GetGames(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? playerId = null,
        [FromQuery] Guid? venueId = null,
        [FromQuery] GameState? gameState = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? orderBy = "WhenPlayed",
        [FromQuery] bool orderByDesc = true)
    {
        _logger.LogInformation("Getting games - Page: {Page}, Size: {Size}", pageNumber, pageSize);

        // Validate pagination parameters
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.Games.AsQueryable();

        // Apply filters
        if (playerId.HasValue)
        {
            query = query.Where(g => g.PlayerId == playerId.Value);
        }

        if (venueId.HasValue)
        {
            query = query.Where(g => g.VenueId == venueId.Value);
        }

        if (gameState.HasValue)
        {
            query = query.Where(g => g.GameState == gameState.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(g => g.WhenPlayed >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(g => g.WhenPlayed <= toDate.Value);
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Apply sorting
        query = orderBy?.ToLowerInvariant() switch
        {
            "whenplayed" => orderByDesc ? query.OrderByDescending(g => g.WhenPlayed) : query.OrderBy(g => g.WhenPlayed),
            "gamestate" => orderByDesc ? query.OrderByDescending(g => g.GameState) : query.OrderBy(g => g.GameState),
            "totalscore" => orderByDesc ? query.OrderByDescending(g => g.TotalScore) : query.OrderBy(g => g.TotalScore),
            _ => query.OrderByDescending(g => g.WhenPlayed)
        };

        // Apply pagination
        var games = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var response = new PaginatedResponse<Game>
        {
            Data = games,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };

        _logger.LogInformation("Retrieved {Count} games out of {Total}", games.Count, totalCount);

        return Ok(response);
    }

    /// <summary>
    /// Get a specific game by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Game>> GetGame(Guid id)
    {
        _logger.LogInformation("Getting game: {GameId}", id);

        var game = await _context.Games
            .Include(g => g.Frames)
            .FirstOrDefaultAsync(g => g.GameId == id);

        if (game == null)
        {
            _logger.LogWarning("Game not found: {GameId}", id);
            return NotFound(new { Message = "Game not found", GameId = id });
        }

        return game;
    }

    /// <summary>
    /// Update an existing game
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateGame(Guid id, Game game)
    {
        if (id != game.GameId)
        {
            _logger.LogWarning("Game ID mismatch: URL {UrlId} vs Body {BodyId}", id, game.GameId);
            return BadRequest(new { Message = "Game ID mismatch" });
        }

        var existingGame = await _context.Games.FindAsync(id);

        if (existingGame == null)
        {
            _logger.LogWarning("Game not found for update: {GameId}", id);
            return NotFound(new { Message = "Game not found", GameId = id });
        }

        _logger.LogInformation("Updating game: {GameId}", id);

        // Update properties from the incoming game object
        _context.Entry(existingGame).CurrentValues.SetValues(game);

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Game updated successfully: {GameId}", id);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "Concurrency error updating game: {GameId}", id);
            return Conflict(new { Message = "The game was modified by another user", GameId = id });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error updating game: {GameId}", id);
            return StatusCode(500, new { Message = "Error updating game" });
        }

        return NoContent();
    }

    /// <summary>
    /// Create a new game
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Game>> CreateGame(Game newGame)
    {
        _logger.LogInformation("Creating new game for player: {PlayerId}", newGame.PlayerId);

        if (newGame.GameId == Guid.Empty)
        {
            newGame.GameId = Guid.NewGuid();
        }

        try
        {
            _context.Games.Add(newGame);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Game created successfully: {GameId}", newGame.GameId);

            return CreatedAtAction(nameof(GetGame), new { id = newGame.GameId }, newGame);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error creating game");
            return StatusCode(500, new { Message = "Error creating game" });
        }
    }

    /// <summary>
    /// Delete a game
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteGame(Guid id)
    {
        _logger.LogInformation("Deleting game: {GameId}", id);

        var game = await _context.Games.FindAsync(id);

        if (game == null)
        {
            _logger.LogWarning("Game not found for deletion: {GameId}", id);
            return NotFound(new { Message = "Game not found", GameId = id });
        }

        try
        {
            _context.Games.Remove(game);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Game deleted successfully: {GameId}", id);

            return NoContent();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error deleting game: {GameId}", id);
            return StatusCode(500, new { Message = "Error deleting game" });
        }
    }

    /// <summary>
    /// Get games for a specific player
    /// </summary>
    [HttpGet("player/{playerId}")]
    public async Task<ActionResult<PaginatedResponse<Game>>> GetPlayerGames(
        Guid playerId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        return await GetGames(pageNumber, pageSize, playerId: playerId);
    }

    /// <summary>
    /// Get games for a specific venue
    /// </summary>
    [HttpGet("venue/{venueId}")]
    public async Task<ActionResult<PaginatedResponse<Game>>> GetVenueGames(
        Guid venueId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        return await GetGames(pageNumber, pageSize, venueId: venueId);
    }
}

/// <summary>
/// Paginated response wrapper for list endpoints
/// </summary>
public class PaginatedResponse<T>
{
    public IEnumerable<T> Data { get; set; } = Enumerable.Empty<T>();
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage => PageNumber < TotalPages;
    public bool HasPreviousPage => PageNumber > 1;
}
