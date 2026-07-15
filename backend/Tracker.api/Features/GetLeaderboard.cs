using MediatR;
using Microsoft.EntityFrameworkCore;
using Tracker.Api.Data;

namespace Tracker.Api.Features;

public record GetLeaderboardQuery : IRequest<List<LeaderboardPlayerDto>>;

public record LeaderboardPlayerDto(
    string DiscordUsername,
    string RiotName,
    string RiotTag,
    string CurrentTier,
    string CurrentRank,
    int CurrentLP
);

public class GetLeaderboardQueryHandler : IRequestHandler<GetLeaderboardQuery, List<LeaderboardPlayerDto>>
{
    private readonly AppDbContext _context;

    public GetLeaderboardQueryHandler(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<LeaderboardPlayerDto>> Handle(GetLeaderboardQuery request, CancellationToken cancellationToken)
    {
        // 1. Fetch all players who have linked a Riot account
        var players = await _context.Players
            .Where(p => !string.IsNullOrEmpty(p.CurrentTier) && !string.IsNullOrEmpty(p.CurrentRank))
            .OrderByDescending(p => p.CurrentLP)
            .Select(p => new LeaderboardPlayerDto(
                p.DiscordUsername,
                p.RiotName,
                p.RiotTag,
                p.CurrentTier,
                p.CurrentRank,
                p.CurrentLP
            ))
            .ToListAsync(cancellationToken);
        
        // 2. Sort players in-memory based on TFT rank
        var sortedLeaderboard = players
            .OrderByDescending(p => GetTierValue(p.CurrentTier))
            .ThenByDescending(p => GetRankValue(p.CurrentRank))
            .ThenByDescending(p => p.CurrentLP)
            .ToList();  

        return sortedLeaderboard;
    }

    private static int GetTierValue(string tier) => tier.ToUpper() switch
    {
        "CHALLENGER"  => 10,
        "GRANDMASTER" => 9,
        "MASTER"      => 8,
        "DIAMOND"     => 7,
        "PLATINUM"    => 6,
        "EMERALD"     => 5,
        "GOLD"        => 4,
        "SILVER"      => 3,
        "BRONZE"      => 2,
        "IRON"        => 1,
        _             => 0 // UNRANKED
    };

    private static int GetRankValue(string rank) => rank.ToUpper() switch
    {
        "I" => 1,
        "II" => 2,
        "III" => 3,
        "IV" => 4,
        _ => 0
    };
}