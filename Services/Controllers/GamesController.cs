using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NinetyNine.Model;

namespace Services.Controllers;

[ApiController]
[ApiVersion("0.0")]
[Route("api/{v:apiVersion}/[controller]")]
public class GamesController : ControllerBase
{
    private readonly NinetyNineContext _context;

    public GamesController(NinetyNineContext dbContext)
    {
        _context = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Game>>> GetGames()
    {
        return await _context.Games.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Game>> GetGame(Guid id)
    {
        var game = await _context.Games.FindAsync(id);
        
        if (game == null)
        {
            return NotFound();
        }
        
        return game;
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateGame(Guid id, Game game)
    {
        if (id != game.GameId)
        {
            return BadRequest();
        }

        var oldGame = await _context.Games.FindAsync(id);

        if (oldGame == null)
        {
            return NotFound();
        }

        oldGame = game;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException) when (!GameExists(id))
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPost]
    public async Task<ActionResult<Game>> CreateGame([Bind("Players, LocationPlayed, WhenPlayed, TableSize, Frames")] Game newGame)
    {
        _context.Games.Add(newGame);
        await _context.SaveChangesAsync();
        
        return CreatedAtAction(nameof(GetGame), new {id = newGame.GameId}, newGame);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteGame(Guid id)
    {
        var game = await _context.Games.FindAsync(id);

        if (game == null)
        {
            return NotFound();
        }

        _context.Games.Remove(game);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private bool GameExists(Guid id)
    {
        return _context.Games.Any(x => x.GameId == id);
    }
}