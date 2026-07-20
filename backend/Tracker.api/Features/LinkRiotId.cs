using MediatR;
using Microsoft.EntityFrameworkCore;
using Tracker.Api.Data;
using Tracker.Api.Services;

namespace Tracker.Api.Features;

public record LinkRiotIdCommand(
    string RequesterDiscordId,
    string DiscordId, 
    string RiotName, 
    string RiotTag
) : IRequest<LinkRiotIdResponse>;

public record LinkRiotIdResponse (string Message, string VerifiedRiotId, string Rank);

public class LinkRiotIdCommandHandler : IRequestHandler<LinkRiotIdCommand, LinkRiotIdResponse>
{
    private readonly AppDbContext _context;
    private readonly RiotApiService _riotApiService;
    private readonly IConfiguration _configuration;

    public LinkRiotIdCommandHandler(AppDbContext context, RiotApiService riotApiService, IConfiguration configuration)
    {
        _context = context;
        _riotApiService = riotApiService;
        _configuration = configuration;
    }

    public async Task<LinkRiotIdResponse> Handle(LinkRiotIdCommand request, CancellationToken cancellationToken)
    {        
        var player = await _context.Players.FirstOrDefaultAsync(p => p.DiscordId == request.DiscordId, cancellationToken);
        if (player == null)
        {
            throw new KeyNotFoundException("Player profile not found. Please log in via Discord first.");
        }

        // 1. Identify if the requester is an administrator
        var adminIds = _configuration.GetSection("Admins:DiscordIds").Get<string[]>() ?? Array.Empty<string>();
        bool isRequesterAdmin = adminIds.Contains(request.RequesterDiscordId);

        // 2. Validate authorization
        if (!isRequesterAdmin && request.RequesterDiscordId != request.DiscordId){
            throw new UnauthorizedAccessException("You are not authorized to update this profile.");
        }

        // 3. Prevent non-admins from changing Riot ID if they are already competing.
        if (player.IsOptedIn && !isRequesterAdmin){
            throw new InvalidOperationException("You have already opted into the leaderboard and cannot change your linked Riot account. Please contact an admin if you need to update this.");
        }

        var riotAccount = await _riotApiService.GetAccountByRiotIdAsync(request.RiotName, request.RiotTag);
        if (riotAccount == null)
        {
            throw new ArgumentException("Could not find that Riot ID. Verify spelling or check your API key.");
        }

        player.RiotName = riotAccount.GameName;
        player.RiotTag = riotAccount.TagLine;
        player.RiotPuuid = riotAccount.Puuid;

        var summonerData = await _riotApiService.GetSummonerByPuuidAsync(riotAccount.Puuid, "sg2");
        if (summonerData != null){
            player.ProfileIconId = summonerData.ProfileIconId;
        }

        var tftRankings = await _riotApiService.GetTftRankByPuuidAsync(riotAccount.Puuid, "sg2");
        if (tftRankings != null)
        {
            var soloQueue = tftRankings.FirstOrDefault(r => r.QueueType == "RANKED_TFT");
            if (soloQueue != null)
            {
                player.CurrentTier = soloQueue.Tier;
                player.CurrentRank = soloQueue.Rank;
                player.CurrentLP = soloQueue.LeaguePoints;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new LinkRiotIdResponse(
            "Riot Account linked successfully via MediatR!",
            $"{player.RiotName}#{player.RiotTag}",
            $"{player.CurrentTier} {player.CurrentRank} - {player.CurrentLP} LP"
        );
    }
}

