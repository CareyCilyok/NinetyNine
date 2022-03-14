using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NinetyNine.Model;
using NinetyNine.Repository;

namespace Services.Controllers;

[ApiController]
[ApiVersion("0.0")]
[Route("api/{v:apiVersion}/[controller]")]
public class VenuesController : ControllerBase
{
    private readonly LocalContext _context;

    public VenuesController(LocalContext dbContext)
    {
        _context = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Venue>>> GetVenues()
    {
        return await _context.Venues.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Venue>> GetVenue(Guid id)
    {
        var venue = await _context.Venues.FindAsync(id);
        
        if (venue == null)
        {
            return NotFound();
        }
        
        return venue;
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateVenue(Guid id, Venue venue)
    {
        if (id != venue.VenueId)
        {
            return BadRequest();
        }

        var oldVenue= await _context.Venues.FindAsync(id);

        if (oldVenue == null)
        {
            return NotFound();
        }

        oldVenue = venue;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException) when (!VenueExists(id))
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPost]
    public async Task<ActionResult<Venue>> CreateVenue(Venue newVenue)
    {
        _context.Venues.Add(newVenue);
        await _context.SaveChangesAsync();
        
        return CreatedAtAction(nameof(GetVenue), new {id = newVenue.VenueId}, newVenue);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteVenue(Guid id)
    {
        var venue = await _context.Venues.FindAsync(id);

        if (venue == null)
        {
            return NotFound();
        }

        _context.Venues.Remove(venue);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private bool VenueExists(Guid id)
    {
        return _context.Venues.Any(x => x.VenueId == id);
    }
}