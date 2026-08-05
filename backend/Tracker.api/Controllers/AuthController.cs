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
        var discordAvatarHash = claims?.FirstOrDefault(c => c.Type == "urn:discord:avatar:hash")?.Value ?? string.Empty;
        var discordGlobalName = claims?.FirstOrDefault(c => c.Type == "urn:discord:globalname")?.Value ?? discordUsername;

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
                DiscordAvatarHash = discordAvatarHash,
                DiscordGlobalName = discordGlobalName,
                RiotName = "",
                RiotTag = "",
                RiotPuuid = "",
                CurrentTier = "UNRANKED",
                CurrentRank = "IV",
                CurrentLP = 0,
                HasHitMasterThisSet = false,
                TimestampHitMaster = null,
                IsOptedIn = false,
                GamesPlayed = 0,
                AVP = 0.0
            };

            _context.Players.Add(newPlayer);
        }
        else
        {
            existingPlayer.DiscordUsername = discordUsername;
            existingPlayer.DiscordAvatarHash = discordAvatarHash;
            existingPlayer.DiscordGlobalName = discordGlobalName;
        }

        await _context.SaveChangesAsync();
        
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
        // Determine if current player is an administrator
        var adminIds = _configuration.GetSection("Admins:DiscordIds").Get<string[]>() ?? Array.Empty<string>();
        bool isAdmin = adminIds.Contains(player.DiscordId);

        // Check if registration is open (Midnight GMT+8 of the race day)
        var setStartTimestamp = _configuration.GetValue<long>("TftConfig:SetStartTimestamp");
        var setStartOffset = DateTimeOffset.FromUnixTimeSeconds(setStartTimestamp).ToOffset(TimeSpan.FromHours(8));
        var deadlineOffset = new DateTimeOffset(setStartOffset.Year, setStartOffset.Month, setStartOffset.Day, 0, 0, 0, TimeSpan.FromHours(8));
        bool isRegistrationOpen = DateTimeOffset.UtcNow < deadlineOffset || isAdmin;

        return Ok(new
        {
            player.DiscordId,
            player.DiscordUsername,
            player.DiscordAvatarHash,
            player.DiscordGlobalName,
            player.RiotName,
            player.RiotTag,
            player.ProfileIconId,
            player.CurrentTier,
            player.CurrentRank,
            player.CurrentLP,
            player.IsOptedIn,
            player.GamesPlayed,
            player.AVP,
            IsAdmin = isAdmin,
            IsRegistrationOpen = isRegistrationOpen
        });
    }

[HttpPost("opt-in")]
    public async Task<IActionResult> OptIn()
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

        // Enforce registration deadline (GMT+8 of the race day)
        var setStartTimestamp = _configuration.GetValue<long>("TftConfig:SetStartTimestamp");
        var setStartOffset = DateTimeOffset.FromUnixTimeSeconds(setStartTimestamp).ToOffset(TimeSpan.FromHours(8));
        var deadlineOffset = new DateTimeOffset(setStartOffset.Year, setStartOffset.Month, setStartOffset.Day, 0, 0, 0, TimeSpan.FromHours(8));
        
        var adminIds = _configuration.GetSection("Admins:DiscordIds").Get<string[]>() ?? Array.Empty<string>();
        bool isAdmin = adminIds.Contains(player.DiscordId);

        if (DateTimeOffset.UtcNow >= deadlineOffset && !isAdmin){
            throw new InvalidOperationException("Registration is closed.");
        }
        
        if (string.IsNullOrEmpty(player.RiotPuuid))
        {
            return BadRequest("You must link a Riot ID before opting into the leaderboard.");
        }
        player.IsOptedIn = true;
        await _context.SaveChangesAsync();
        return Ok(new { Message = "Successfully registered for the tournament!" });
    }
    
    [HttpPost("opt-out")]
    public async Task<IActionResult> OptOut()
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

        // Enforce deregistration deadline (GMT+8 of the race day)
        var setStartTimestamp = _configuration.GetValue<long>("TftConfig:SetStartTimestamp");
        var setStartOffset = DateTimeOffset.FromUnixTimeSeconds(setStartTimestamp).ToOffset(TimeSpan.FromHours(8));
        var deadlineOffset = new DateTimeOffset(setStartOffset.Year, setStartOffset.Month, setStartOffset.Day, 0, 0, 0, TimeSpan.FromHours(8));
        var adminIds = _configuration.GetSection("Admins:DiscordIds").Get<string[]>() ?? Array.Empty<string>();
        bool isAdmin = adminIds.Contains(player.DiscordId);

        if (DateTimeOffset.UtcNow >= deadlineOffset && !isAdmin)
        {
            return BadRequest("Deregistration is closed. Registered users cannot deregister once the day of the race has started.");
        }

        player.IsOptedIn = false;
        player.GamesPlayed = 0;
        player.AVP = 0.0;
        // Clean up their match history cache since they are opting out
        if (!string.IsNullOrEmpty(player.RiotPuuid))
        {
            var oldMatches = await _context.Matches.Where(m => m.PlayerPuuid == player.RiotPuuid).ToListAsync();
            if (oldMatches.Any())
            {
                _context.Matches.RemoveRange(oldMatches);
            }
        }
        await _context.SaveChangesAsync();
        return Ok(new { Message = "Successfully deregistered from the tournament." });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { Message = "Logged out successfully." });
    }
}