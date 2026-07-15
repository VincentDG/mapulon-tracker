using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Tracker.Api.Data;
using Tracker.Api.Models;

namespace Tracker.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }
    
    [HttpGet("login-discord")]
    public IActionResult LoginDiscord()
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(DiscordCallback))
        };

        return Challenge(properties, "Discord");
    }

    [HttpGet("callback")]
    public async Task<IActionResult> DiscordCallback()
    {
        var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!result.Succeeded)
        {
            return BadRequest("Discord authentication failed.");
        }

        var claims = result.Principal?.Identities.FirstOrDefault()?.Claims;

        var discordId = claims?.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        var discordUsername = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? "Unknown User";

        if (string.IsNullOrEmpty(discordId))
        {
            return BadRequest("Could not retrieve Discord ID.");
        }

        var existingPlayer = await _context.Players
            .FirstOrDefaultAsync(p => p.DiscordId == discordId);
        
        if (existingPlayer == null)
        {
            var newPlayer = new Player
            {
                DiscordId = discordId,
                DiscordUsername = discordUsername,
                RiotName = "",
                RiotTag = "",
                RiotPuuid = "",
                CurrentTier = "UNRANKED"
            };

            _context.Players.Add(newPlayer);
            await _context.SaveChangesAsync();
        }

        var frontendUrl = _configuration["RedirectUrls:Frontend"] ?? "http://localhost:4200";
        return Redirect(frontendUrl);
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        if (User.Identity == null || !User.Identity.IsAuthenticated)
        {
            return Unauthorized("Not logged in.");
        }
        var claims = User.Identities.FirstOrDefault()?.Claims;
        var discordId = claims?.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(discordId))
        {
            return Unauthorized("Discord ID claim missing.");
        }
        var player = await _context.Players.FirstOrDefaultAsync(p => p.DiscordId == discordId);
        if (player == null)
        {
            return NotFound("Player profile not found in database.");
        }
        return Ok(new
        {
            player.DiscordId,
            player.DiscordUsername,
            player.RiotName,
            player.RiotTag,
            player.CurrentTier,
            player.CurrentRank,
            player.CurrentLP
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { Message = "Logged out successfully." });
    }
}