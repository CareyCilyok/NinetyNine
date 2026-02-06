using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NinetyNine.Model;

namespace Services.Controllers;

[ApiController]
[ApiVersion("0.0")]
[Route("api/{v:apiVersion}/[controller]")]
[Authorize]
public class PlayersController : ControllerBase
{
    private readonly NinetyNineContext _context;
    private readonly ILogger<PlayersController> _logger;

    public PlayersController(NinetyNineContext dbContext, ILogger<PlayersController> logger)
    {
        _context = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Get all players with pagination and search
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<Player>>> GetPlayers(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? orderBy = "Username",
        [FromQuery] bool orderByDesc = false)
    {
        _logger.LogInformation("Getting players - Page: {Page}, Size: {Size}", pageNumber, pageSize);

        // Validate pagination parameters
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.Players.AsQueryable();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(p =>
                p.Username.ToLower().Contains(searchLower) ||
                p.FirstName.ToLower().Contains(searchLower) ||
                p.LastName.ToLower().Contains(searchLower) ||
                p.EmailAddress.ToLower().Contains(searchLower));
        }

        if (isActive.HasValue)
        {
            query = query.Where(p => p.IsActive == isActive.Value);
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Apply sorting
        query = orderBy?.ToLowerInvariant() switch
        {
            "username" => orderByDesc ? query.OrderByDescending(p => p.Username) : query.OrderBy(p => p.Username),
            "firstname" => orderByDesc ? query.OrderByDescending(p => p.FirstName) : query.OrderBy(p => p.FirstName),
            "lastname" => orderByDesc ? query.OrderByDescending(p => p.LastName) : query.OrderBy(p => p.LastName),
            "createdat" => orderByDesc ? query.OrderByDescending(p => p.CreatedAt) : query.OrderBy(p => p.CreatedAt),
            "lastloginat" => orderByDesc ? query.OrderByDescending(p => p.LastLoginAt) : query.OrderBy(p => p.LastLoginAt),
            _ => orderByDesc ? query.OrderByDescending(p => p.Username) : query.OrderBy(p => p.Username)
        };

        // Apply pagination
        var players = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var response = new PaginatedResponse<Player>
        {
            Data = players,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };

        _logger.LogInformation("Retrieved {Count} players out of {Total}", players.Count, totalCount);

        return Ok(response);
    }

    /// <summary>
    /// Get a specific player by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Player>> GetPlayer(Guid id)
    {
        _logger.LogInformation("Getting player: {PlayerId}", id);

        var player = await _context.Players.FindAsync(id);

        if (player == null)
        {
            _logger.LogWarning("Player not found: {PlayerId}", id);
            return NotFound(new { Message = "Player not found", PlayerId = id });
        }

        return player;
    }

    /// <summary>
    /// Get player statistics
    /// </summary>
    [HttpGet("{id}/stats")]
    public async Task<ActionResult<PlayerStats>> GetPlayerStats(Guid id)
    {
        _logger.LogInformation("Getting player stats: {PlayerId}", id);

        var player = await _context.Players.FindAsync(id);

        if (player == null)
        {
            _logger.LogWarning("Player not found for stats: {PlayerId}", id);
            return NotFound(new { Message = "Player not found", PlayerId = id });
        }

        var games = await _context.Games
            .Where(g => g.PlayerId == id && g.GameState == GameState.Completed)
            .Include(g => g.Frames)
            .ToListAsync();

        var stats = new PlayerStats
        {
            PlayerId = id,
            TotalGames = games.Count,
            TotalScore = games.Sum(g => g.TotalScore),
            AverageScore = games.Count > 0 ? games.Average(g => g.TotalScore) : 0,
            HighestScore = games.Count > 0 ? games.Max(g => g.TotalScore) : 0,
            LowestScore = games.Count > 0 ? games.Min(g => g.TotalScore) : 0,
            PerfectFrames = games.SelectMany(g => g.Frames).Count(f => f.FrameScore == 11),
            TotalFrames = games.SelectMany(g => g.Frames).Count()
        };

        return Ok(stats);
    }

    /// <summary>
    /// Update an existing player
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePlayer(Guid id, Player player)
    {
        if (id != player.PlayerId)
        {
            _logger.LogWarning("Player ID mismatch: URL {UrlId} vs Body {BodyId}", id, player.PlayerId);
            return BadRequest(new { Message = "Player ID mismatch" });
        }

        var existingPlayer = await _context.Players.FindAsync(id);

        if (existingPlayer == null)
        {
            _logger.LogWarning("Player not found for update: {PlayerId}", id);
            return NotFound(new { Message = "Player not found", PlayerId = id });
        }

        _logger.LogInformation("Updating player: {PlayerId}", id);

        // Preserve password fields - don't allow direct update
        player.PasswordHash = existingPlayer.PasswordHash;
        player.PasswordSalt = existingPlayer.PasswordSalt;

        _context.Entry(existingPlayer).CurrentValues.SetValues(player);

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Player updated successfully: {PlayerId}", id);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "Concurrency error updating player: {PlayerId}", id);
            return Conflict(new { Message = "The player was modified by another user", PlayerId = id });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error updating player: {PlayerId}", id);
            return StatusCode(500, new { Message = "Error updating player" });
        }

        return NoContent();
    }

    /// <summary>
    /// Create a new player (admin only - normal registration via /auth/register)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Player>> CreatePlayer(Player newPlayer)
    {
        _logger.LogInformation("Creating new player: {Username}", newPlayer.Username);

        if (newPlayer.PlayerId == Guid.Empty)
        {
            newPlayer.PlayerId = Guid.NewGuid();
        }

        // Check for duplicate email
        if (await _context.Players.AnyAsync(p => p.EmailAddress == newPlayer.EmailAddress))
        {
            _logger.LogWarning("Duplicate email during player creation: {Email}", newPlayer.EmailAddress);
            return Conflict(new { Message = "A player with this email already exists" });
        }

        try
        {
            _context.Players.Add(newPlayer);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Player created successfully: {PlayerId}", newPlayer.PlayerId);

            return CreatedAtAction(nameof(GetPlayer), new { id = newPlayer.PlayerId }, newPlayer);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error creating player");
            return StatusCode(500, new { Message = "Error creating player" });
        }
    }

    /// <summary>
    /// Delete a player
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePlayer(Guid id)
    {
        _logger.LogInformation("Deleting player: {PlayerId}", id);

        var player = await _context.Players.FindAsync(id);

        if (player == null)
        {
            _logger.LogWarning("Player not found for deletion: {PlayerId}", id);
            return NotFound(new { Message = "Player not found", PlayerId = id });
        }

        try
        {
            _context.Players.Remove(player);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Player deleted successfully: {PlayerId}", id);

            return NoContent();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error deleting player: {PlayerId}", id);
            return StatusCode(500, new { Message = "Error deleting player" });
        }
    }

    /// <summary>
    /// Deactivate a player (soft delete)
    /// </summary>
    [HttpPost("{id}/deactivate")]
    public async Task<IActionResult> DeactivatePlayer(Guid id)
    {
        _logger.LogInformation("Deactivating player: {PlayerId}", id);

        var player = await _context.Players.FindAsync(id);

        if (player == null)
        {
            return NotFound(new { Message = "Player not found", PlayerId = id });
        }

        player.IsActive = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Player deactivated: {PlayerId}", id);

        return Ok(new { Message = "Player deactivated successfully" });
    }

    /// <summary>
    /// Reactivate a player
    /// </summary>
    [HttpPost("{id}/activate")]
    public async Task<IActionResult> ActivatePlayer(Guid id)
    {
        _logger.LogInformation("Activating player: {PlayerId}", id);

        var player = await _context.Players.FindAsync(id);

        if (player == null)
        {
            return NotFound(new { Message = "Player not found", PlayerId = id });
        }

        player.IsActive = true;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Player activated: {PlayerId}", id);

        return Ok(new { Message = "Player activated successfully" });
    }
}

/// <summary>
/// Player statistics summary
/// </summary>
public class PlayerStats
{
    public Guid PlayerId { get; set; }
    public int TotalGames { get; set; }
    public int TotalScore { get; set; }
    public double AverageScore { get; set; }
    public int HighestScore { get; set; }
    public int LowestScore { get; set; }
    public int PerfectFrames { get; set; }
    public int TotalFrames { get; set; }
    public double PerfectFramePercentage => TotalFrames > 0 ? (double)PerfectFrames / TotalFrames * 100 : 0;
}
