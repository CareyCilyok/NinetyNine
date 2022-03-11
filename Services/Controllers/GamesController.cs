using AutoFixture;
using Microsoft.AspNetCore.Mvc;
using NinetyNine.Model;

namespace Services.Controllers;

[ApiController]
[Route("api/v0/[controller]")]
public class GamesController : ControllerBase
{
    private static Fixture _fixture = new Fixture();

    private IEnumerable<Game> _games;

    public GamesController()
    {
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());
        _games = _fixture.CreateMany<Game>(15);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Game>>> GetGames()
    {
        return _games.ToArray();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Game>> GetGame(Guid id)
    {
        return Ok();
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateGame(Guid id, Game game)
    {
        return NotFound();
    }

    [HttpPost]
    public async Task<ActionResult<Game>> CreateGame(Game newGame)
    {

        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteGame(Guid Id)
    {
        var game = _games.FirstOrDefault(x => x.GameId == Id);

        if (game == null)
        {
            return NotFound();
        }
        
        return Ok();
    }
}