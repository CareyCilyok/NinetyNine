using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NinetyNine.Model;

namespace Services.Controllers;

[ApiController]
[ApiVersion("0.0")]
[Route("api/{v:apiVersion}/[controller]")]
public class PlayersController : ControllerBase
{
    private readonly NinetyNineContext _context;

    public PlayersController(NinetyNineContext dbContext)
    {
        _context = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Player>>> GetPlayers()
    {
        return await _context.Players.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Player>> GetPlayer(Guid id)
    {
        var player = await _context.Players.FindAsync(id);
        
        if (player == null)
        {
            return NotFound();
        }
        
        return player;
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePlayer(Guid id, Player player)
    {
        if (id != player.PlayerId)
        {
            return BadRequest();
        }

        var oldPlayer= await _context.Players.FindAsync(id);

        if (oldPlayer == null)
        {
            return NotFound();
        }

        oldPlayer = player;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException) when (!PlayerExists(id))
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPost]
    public async Task<ActionResult<Player>> CreatePlayer(Player newPlayer)
    {
        _context.Players.Add(newPlayer);
        await _context.SaveChangesAsync();
        
        return CreatedAtAction(nameof(GetPlayer), new {id = newPlayer.PlayerId}, newPlayer);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePlayer(Guid id)
    {
        var player = await _context.Players.FindAsync(id);

        if (player == null)
        {
            return NotFound();
        }

        _context.Players.Remove(player);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private bool PlayerExists(Guid id)
    {
        return _context.Players.Any(x => x.PlayerId == id);
    }
}