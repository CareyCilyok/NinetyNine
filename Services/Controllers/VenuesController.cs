using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NinetyNine.Model;

namespace Services.Controllers;

[ApiController]
[ApiVersion("0.0")]
[Route("api/{v:apiVersion}/[controller]")]
[Authorize]
public class VenuesController : ControllerBase
{
    private readonly NinetyNineContext _context;
    private readonly ILogger<VenuesController> _logger;

    public VenuesController(NinetyNineContext dbContext, ILogger<VenuesController> logger)
    {
        _context = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Get all venues with pagination and search
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedResponse<Venue>>> GetVenues(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? isPrivate = null,
        [FromQuery] string? orderBy = "Name",
        [FromQuery] bool orderByDesc = false)
    {
        _logger.LogInformation("Getting venues - Page: {Page}, Size: {Size}", pageNumber, pageSize);

        // Validate pagination parameters
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.Venues.AsQueryable();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(v =>
                v.Name.ToLower().Contains(searchLower) ||
                (v.Address != null && v.Address.ToLower().Contains(searchLower)));
        }

        if (isPrivate.HasValue)
        {
            query = query.Where(v => v.Private == isPrivate.Value);
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Apply sorting
        query = orderBy?.ToLowerInvariant() switch
        {
            "name" => orderByDesc ? query.OrderByDescending(v => v.Name) : query.OrderBy(v => v.Name),
            "address" => orderByDesc ? query.OrderByDescending(v => v.Address) : query.OrderBy(v => v.Address),
            _ => orderByDesc ? query.OrderByDescending(v => v.Name) : query.OrderBy(v => v.Name)
        };

        // Apply pagination
        var venues = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var response = new PaginatedResponse<Venue>
        {
            Data = venues,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };

        _logger.LogInformation("Retrieved {Count} venues out of {Total}", venues.Count, totalCount);

        return Ok(response);
    }

    /// <summary>
    /// Get a specific venue by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Venue>> GetVenue(Guid id)
    {
        _logger.LogInformation("Getting venue: {VenueId}", id);

        var venue = await _context.Venues.FindAsync(id);

        if (venue == null)
        {
            _logger.LogWarning("Venue not found: {VenueId}", id);
            return NotFound(new { Message = "Venue not found", VenueId = id });
        }

        return venue;
    }

    /// <summary>
    /// Get venue statistics
    /// </summary>
    [HttpGet("{id}/stats")]
    public async Task<ActionResult<VenueStats>> GetVenueStats(Guid id)
    {
        _logger.LogInformation("Getting venue stats: {VenueId}", id);

        var venue = await _context.Venues.FindAsync(id);

        if (venue == null)
        {
            _logger.LogWarning("Venue not found for stats: {VenueId}", id);
            return NotFound(new { Message = "Venue not found", VenueId = id });
        }

        var games = await _context.Games
            .Where(g => g.VenueId == id && g.GameState == GameState.Completed)
            .Include(g => g.Frames)
            .ToListAsync();

        var uniquePlayers = games.Select(g => g.PlayerId).Distinct().Count();

        var stats = new VenueStats
        {
            VenueId = id,
            VenueName = venue.Name,
            TotalGames = games.Count,
            UniquePlayersCount = uniquePlayers,
            TotalScore = games.Sum(g => g.TotalScore),
            AverageScore = games.Count > 0 ? games.Average(g => g.TotalScore) : 0,
            HighestScore = games.Count > 0 ? games.Max(g => g.TotalScore) : 0,
            LastGameDate = games.Count > 0 ? games.Max(g => g.WhenPlayed) : null
        };

        return Ok(stats);
    }

    /// <summary>
    /// Update an existing venue
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateVenue(Guid id, Venue venue)
    {
        if (id != venue.VenueId)
        {
            _logger.LogWarning("Venue ID mismatch: URL {UrlId} vs Body {BodyId}", id, venue.VenueId);
            return BadRequest(new { Message = "Venue ID mismatch" });
        }

        var existingVenue = await _context.Venues.FindAsync(id);

        if (existingVenue == null)
        {
            _logger.LogWarning("Venue not found for update: {VenueId}", id);
            return NotFound(new { Message = "Venue not found", VenueId = id });
        }

        _logger.LogInformation("Updating venue: {VenueId}", id);

        _context.Entry(existingVenue).CurrentValues.SetValues(venue);

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Venue updated successfully: {VenueId}", id);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "Concurrency error updating venue: {VenueId}", id);
            return Conflict(new { Message = "The venue was modified by another user", VenueId = id });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error updating venue: {VenueId}", id);
            return StatusCode(500, new { Message = "Error updating venue" });
        }

        return NoContent();
    }

    /// <summary>
    /// Create a new venue
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Venue>> CreateVenue(Venue newVenue)
    {
        _logger.LogInformation("Creating new venue: {VenueName}", newVenue.Name);

        if (newVenue.VenueId == Guid.Empty)
        {
            newVenue.VenueId = Guid.NewGuid();
        }

        try
        {
            _context.Venues.Add(newVenue);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Venue created successfully: {VenueId}", newVenue.VenueId);

            return CreatedAtAction(nameof(GetVenue), new { id = newVenue.VenueId }, newVenue);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error creating venue");
            return StatusCode(500, new { Message = "Error creating venue" });
        }
    }

    /// <summary>
    /// Delete a venue
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteVenue(Guid id)
    {
        _logger.LogInformation("Deleting venue: {VenueId}", id);

        var venue = await _context.Venues.FindAsync(id);

        if (venue == null)
        {
            _logger.LogWarning("Venue not found for deletion: {VenueId}", id);
            return NotFound(new { Message = "Venue not found", VenueId = id });
        }

        // Check if venue has associated games
        var hasGames = await _context.Games.AnyAsync(g => g.VenueId == id);
        if (hasGames)
        {
            _logger.LogWarning("Cannot delete venue with associated games: {VenueId}", id);
            return Conflict(new { Message = "Cannot delete venue with associated games. Delete games first or reassign them." });
        }

        try
        {
            _context.Venues.Remove(venue);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Venue deleted successfully: {VenueId}", id);

            return NoContent();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error deleting venue: {VenueId}", id);
            return StatusCode(500, new { Message = "Error deleting venue" });
        }
    }

    /// <summary>
    /// Search venues by name or address
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<Venue>>> SearchVenues([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { Message = "Search query is required" });
        }

        _logger.LogInformation("Searching venues: {Query}", q);

        var searchLower = q.ToLower();
        var venues = await _context.Venues
            .Where(v =>
                v.Name.ToLower().Contains(searchLower) ||
                (v.Address != null && v.Address.ToLower().Contains(searchLower)))
            .Take(10)
            .ToListAsync();

        return Ok(venues);
    }

    /// <summary>
    /// Get popular venues by game count
    /// </summary>
    [HttpGet("popular")]
    public async Task<ActionResult<IEnumerable<VenueStats>>> GetPopularVenues([FromQuery] int limit = 10)
    {
        _logger.LogInformation("Getting popular venues - Limit: {Limit}", limit);

        limit = Math.Clamp(limit, 1, 50);

        var venueStats = await _context.Games
            .Where(g => g.VenueId != null && g.GameState == GameState.Completed)
            .GroupBy(g => g.VenueId)
            .Select(group => new
            {
                VenueId = group.Key,
                TotalGames = group.Count(),
                TotalScore = group.Sum(g => g.TotalScore),
                AverageScore = group.Average(g => g.TotalScore),
                HighestScore = group.Max(g => g.TotalScore),
                UniquePlayersCount = group.Select(g => g.PlayerId).Distinct().Count(),
                LastGameDate = group.Max(g => g.WhenPlayed)
            })
            .OrderByDescending(v => v.TotalGames)
            .Take(limit)
            .ToListAsync();

        var venueIds = venueStats.Select(v => v.VenueId).ToList();
        var venues = await _context.Venues
            .Where(v => venueIds.Contains(v.VenueId))
            .ToDictionaryAsync(v => v.VenueId);

        var result = venueStats
            .Where(vs => vs.VenueId.HasValue && venues.ContainsKey(vs.VenueId.Value))
            .Select(vs => new VenueStats
            {
                VenueId = vs.VenueId!.Value,
                VenueName = venues[vs.VenueId!.Value].Name,
                TotalGames = vs.TotalGames,
                TotalScore = vs.TotalScore,
                AverageScore = vs.AverageScore,
                HighestScore = vs.HighestScore,
                UniquePlayersCount = vs.UniquePlayersCount,
                LastGameDate = vs.LastGameDate
            })
            .ToList();

        return Ok(result);
    }
}

/// <summary>
/// Venue statistics summary
/// </summary>
public class VenueStats
{
    public Guid VenueId { get; set; }
    public string VenueName { get; set; } = string.Empty;
    public int TotalGames { get; set; }
    public int UniquePlayersCount { get; set; }
    public int TotalScore { get; set; }
    public double AverageScore { get; set; }
    public int HighestScore { get; set; }
    public DateTime? LastGameDate { get; set; }
}
