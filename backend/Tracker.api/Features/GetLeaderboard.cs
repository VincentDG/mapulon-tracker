using MediatR;
using Microsoft.EntityFrameworkCore;
using Tracker.Api.Data;

namespace Tracker.Api.Features;

public record GetLeaderboardQuery : IRequest<List<LeaderboardPlayerDto>>;

public record LeaderboardPlayerDto(
    string DiscordUsername,
    string DiscordGlobalName,
    string DiscordAvatarHash,
    string DiscordId,
    string RiotName,
    string RiotTag,
    int ProfileIconId,
    int GamesPlayed,
    double AVP,
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
        // Fetch all opted-in players who have linked a Riot account
        var players = await _context.Players
            .Where(p => p.IsOptedIn && !string.IsNullOrEmpty(p.RiotPuuid))
            .Select(p => new LeaderboardPlayerDto(
                p.DiscordUsername,
                p.DiscordGlobalName,
                p.DiscordAvatarHash,
                p.DiscordId,
                p.RiotName,
                p.RiotTag,
                p.ProfileIconId,
                p.GamesPlayed,
                p.AVP,
                p.CurrentTier,
                p.CurrentRank,
                p.CurrentLP
            ))
            .ToListAsync(cancellationToken);
        
        // Sort players in-memory based on TFT rank
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
        "I" => 4,
        "II" => 3,
        "III" => 2,
        "IV" => 1,
        _ => 0
    };
}