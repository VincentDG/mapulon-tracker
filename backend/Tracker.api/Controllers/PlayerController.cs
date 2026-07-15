using MediatR;
using Microsoft.AspNetCore.Mvc;
using Tracker.Api.Features;

namespace Tracker.Api.Controllers;

[ApiController]
[Route("api/players")]
public class PlayerController : ControllerBase
{
    private readonly IMediator _mediator;

    public PlayerController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetLeaderboard()
    {
        var result = await _mediator.Send(new GetLeaderboardQuery());
        return Ok(result);
    }

    [HttpPost("link-riot")]
    public async Task<IActionResult> LinkRiotId([FromBody] LinkRiotRequest request)
    {
        try
        {
            var command = new LinkRiotIdCommand(request.DiscordId, request.RiotName, request.RiotTag);
            var result = await _mediator.Send(command);
            
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}

public class LinkRiotRequest
{
    public string DiscordId {get; set;} = string.Empty;
    public string RiotName {get; set;} = string.Empty;
    public string RiotTag {get; set;} = string.Empty;
}