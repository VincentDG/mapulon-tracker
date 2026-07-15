using MediatR;
using Microsoft.EntityFrameworkCore;
using Tracker.Api.Data;
using Tracker.Api.Services;

namespace Tracker.Api.Features;

public record LinkRiotIdCommand(string DiscordId, string RiotName, string RiotTag) : IRequest<LinkRiotIdResponse>;

public record LinkRiotIdResponse (string Message, string VerifiedRiotId, string Rank);

public class LinkRiotIdCommandHandler : IRequestHandler<LinkRiotIdCommand, LinkRiotIdResponse>
{
    private readonly AppDbContext _context;
    private readonly RiotApiService _riotApiService;

    public LinkRiotIdCommandHandler(AppDbContext context, RiotApiService riotApiService)
    {
        _context = context;
        _riotApiService = riotApiService;
    }

    public async Task<LinkRiotIdResponse> Handle(LinkRiotIdCommand request, CancellationToken cancellationToken)
    {
        var player = await _context.Players.FirstOrDefaultAsync(p => p.DiscordId == request.DiscordId, cancellationToken);
        if (player == null)
        {
            throw new KeyNotFoundException("Player profile not found. Please log in via Discord first.");
        }

        var riotAccount = await _riotApiService.GetAccountByRiotIdAsync(request.RiotName, request.RiotTag);
        if (riotAccount == null)
        {
            throw new ArgumentException("Could not find that Riot ID. Verify spelling or check your API key.");
        }

        player.RiotName = riotAccount.GameName;
        player.RiotTag = riotAccount.TagLine;
        player.RiotPuuid = riotAccount.Puuid;

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

