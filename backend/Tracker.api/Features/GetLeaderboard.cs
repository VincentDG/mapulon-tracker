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
    int CurrentLP,
    bool HasHitMasterThisSet,
    DateTime? TimestampHitMaster,
    double? AVPOnHitMaster
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
            .ToListAsync(cancellationToken);
        var leaderboardList = new List<LeaderboardPlayerDto>();
        foreach (var p in players)
        {
            double? avpOnHitMaster = null;
            if (p.HasHitMasterThisSet && p.TimestampHitMaster.HasValue)
            {
                // Calculate AVP dynamically at the exact point they reached Master
                var matchesBeforeMaster = await _context.Matches
                    .Where(m => m.PlayerPuuid == p.RiotPuuid && m.Placement > 0 && m.MatchEndTime <= p.TimestampHitMaster.Value)
                    .Select(m => m.Placement)
                    .ToListAsync(cancellationToken);
                if (matchesBeforeMaster.Any())
                {
                    avpOnHitMaster = Math.Round(matchesBeforeMaster.Average(), 2);
                }
            }
            leaderboardList.Add(new LeaderboardPlayerDto(
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
                p.CurrentLP,
                p.HasHitMasterThisSet,
                p.TimestampHitMaster,
                avpOnHitMaster
            ));
        }
        
        // Sort players in-memory based on TFT rank
        var sortedLeaderboard = leaderboardList
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