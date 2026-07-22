using Microsoft.Extensions.Hosting;                     
using Microsoft.Extensions.Logging;                     
using Microsoft.Extensions.DependencyInjection;         
using Microsoft.EntityFrameworkCore;     
using Tracker.Api.Data;
using Tracker.Api.Services;
using Tracker.Api.Models;

namespace Tracker.Api.Services;

public class RankUpdateWorker : BackgroundService        
{
    private readonly IServiceProvider _services;         
    private readonly ILogger<RankUpdateWorker> _logger; 
    private readonly IConfiguration _configuration; 
    private readonly TimeSpan _updateInterval = TimeSpan.FromMinutes(15);

    public RankUpdateWorker(IServiceProvider services, ILogger<RankUpdateWorker> logger, IConfiguration configuration)
    {
        _services = services;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TFT Rank Update Background Worker is starting up...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Alarm triggered: Syncing live database records with Riot API...");
                await UpdateAllPlayersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while running the automated rank update loop.");
            }

            // Sleep peacefully for 30 minutes before the next check
            await Task.Delay(_updateInterval, stoppingToken);
        }
    }  

    private async Task UpdateAllPlayersAsync(CancellationToken stoppingToken)
    {
        // Background workers run outside normal HTTP request cycles, 
        // so we create a temporary scope to safely talk to our SQL Server instance.
        using var scope = _services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var riotService = scope.ServiceProvider.GetRequiredService<RiotApiService>();

        // Load the configured set start timestamp
        var setStartTimestamp = _configuration.GetValue<long>("TftConfig:SetStartTimestamp");

        // Fetch only players who have linked accounts and opted into the competition
        var players = await context.Players.Where(p => p.IsOptedIn && !string.IsNullOrEmpty(p.RiotPuuid)).ToListAsync(stoppingToken);
        _logger.LogInformation($"Found {players.Count} opted-in profiles to update.");

        foreach (var player in players)
        {
            _logger.LogInformation($"Checking live stats for: {player.DiscordUsername} ({player.RiotName}#{player.RiotTag})");
            
            // 1. Sync Summoner Icon
            var summonerData = await riotService.GetSummonerByPuuidAsync(player.RiotPuuid, "sg2");
            if (summonerData != null)
            {
                player.ProfileIconId = summonerData.ProfileIconId;
            }
            await Task.Delay(300, stoppingToken); // API throttling

            // 2. Fetch live data from Riot matching your verified 'sg2' server path
            var tftRankings = await riotService.GetTftRankByPuuidAsync(player.RiotPuuid, "sg2");
            if (tftRankings != null)
            {
                var soloQueue = tftRankings.FirstOrDefault(r => r.QueueType == "RANKED_TFT");
                if (soloQueue != null)
                {
                    if (player.CurrentTier != soloQueue.Tier || player.CurrentRank != soloQueue.Rank)
                    {
                        _logger.LogInformation($"🏆 RANK CHANGE: {player.DiscordUsername} shifted to {soloQueue.Tier} {soloQueue.Rank}!");
                    }

                    player.CurrentTier = soloQueue.Tier;
                    player.CurrentRank = soloQueue.Rank;
                    player.CurrentLP = soloQueue.LeaguePoints;
                }
                else
                {
                    player.CurrentTier = "UNRANKED";
                    player.CurrentRank = "";
                    player.CurrentLP = 0;
                }
            }

            // Pause briefly to respect Riot's rate limits
            await Task.Delay(300, stoppingToken); 

             // 1. Clean up stale matches from previous sets for this player
            var setStartDateTime = DateTimeOffset.FromUnixTimeSeconds(setStartTimestamp).UtcDateTime;
            var staleMatches = await context.Matches
                .Where(m => m.PlayerPuuid == player.RiotPuuid && m.MatchEndTime < setStartDateTime)
                .ToListAsync(stoppingToken);
            if (staleMatches.Count > 0)
            {
                context.Matches.RemoveRange(staleMatches);
                await context.SaveChangesAsync(stoppingToken);
                _logger.LogInformation($"Cleaned up {staleMatches.Count} stale matches from previous sets for {player.DiscordUsername}.");
            }

            // 3. Paginate & Sync Match Cache (Singapore/SEA servers use "sea" cluster)
            var matchIds = new List<string>();
            int startOffset = 0;
            const int pageSize = 100;
            while (true)
            {
                var pageMatchIds = await riotService.GetTftMatchIdsAsync(player.RiotPuuid, setStartTimestamp, startOffset, pageSize, "sea");
                if (pageMatchIds == null || pageMatchIds.Count == 0)
                {
                    break;
                }
                matchIds.AddRange(pageMatchIds);
                // If we got less than page size, we reached the end of the history
                if (pageMatchIds.Count < pageSize)
                {
                    break;
                }
                startOffset += pageSize;
                
                // Brief delay between index page requests
                await Task.Delay(500, stoppingToken);
            }
            if (matchIds.Count > 0)
            {
                // Fetch already cached match IDs for this player PUUID
                var cachedMatchIds = await context.Matches
                    .Where(m => m.PlayerPuuid == player.RiotPuuid)
                    .Select(m => m.MatchId)
                    .ToListAsync(stoppingToken);
                // Find only newly played matches
                var newMatchIds = matchIds.Except(cachedMatchIds).ToList();
                _logger.LogInformation($"Player has {newMatchIds.Count} new matches to download.");
                // Adaptive delay: slow down to 1.25s if importing a lot to prevent rate limit (429) bans
                int delayMs = newMatchIds.Count > 20 ? 1250 : 300;
                foreach (var newMatchId in newMatchIds)
                {
                    var matchDetails = await riotService.GetTftMatchDetailsAsync(newMatchId, "sea");
                    if (matchDetails != null)
                    {
                        var participant = matchDetails.Info.Participants.FirstOrDefault(p => p.Puuid == player.RiotPuuid);
                        if (participant != null)
                        {
                            bool isRanked = matchDetails.Info.QueueId == 1100;
                            var matchEntity = new TftMatch
                            {
                                MatchId = newMatchId,
                                PlayerPuuid = player.RiotPuuid,
                                Placement = isRanked ? participant.Placement : -1, // Use -1 as sentinel for non-ranked matches
                                MatchEndTime = DateTimeOffset.FromUnixTimeMilliseconds(matchDetails.Info.GameDatetime).UtcDateTime
                            };
                            context.Matches.Add(matchEntity);
                        }
                    }
                    // Respect rate-limiting pacing
                    await Task.Delay(delayMs, stoppingToken);
                }
            if (newMatchIds.Count > 0)
                {
                    await context.SaveChangesAsync(stoppingToken);
                }
                // Recalculate AVP and games played across all cached ranked matches (Placement > 0)
                var allRankedMatches = await context.Matches
                    .Where(m => m.PlayerPuuid == player.RiotPuuid && m.Placement > 0)
                    .ToListAsync(stoppingToken);
                if (allRankedMatches.Count > 0)
                {
                    player.GamesPlayed = allRankedMatches.Count;
                    player.AVP = Math.Round(allRankedMatches.Average(m => m.Placement), 2);
                }
                else
                {
                    player.GamesPlayed = 0;
                    player.AVP = 0.0;
                }
                await context.SaveChangesAsync(stoppingToken);
            }
        }
        
        _logger.LogInformation("Database sync complete. All ranks up to date!");
    }
}