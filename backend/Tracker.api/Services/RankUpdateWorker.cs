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

        // Load the configured set start timestamp (defaults to Set 12 start timestamp if omitted)
        var setStartTimestamp = _configuration.GetValue<long>("TftConfig:SetStartTimestamp");
        if (setStartTimestamp == 0)
        {
            setStartTimestamp = 1722470400; // July 31, 2024
        }


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

            // 3. Sync Match Cache & Calculate AVP
            var matchIds = await riotService.GetTftMatchIdsAsync(player.RiotPuuid, setStartTimestamp, count: 200, "asia");
            if (matchIds != null && matchIds.Count > 0)
            {
                // Fetch already cached match IDs for this player PUUID
                var cachedMatchIds = await context.Matches
                    .Where(m => m.PlayerPuuid == player.RiotPuuid)
                    .Select(m => m.MatchId)
                    .ToListAsync(stoppingToken);
                // Find only newly played matches
                var newMatchIds = matchIds.Except(cachedMatchIds).ToList();
                _logger.LogInformation($"Player has {newMatchIds.Count} new matches to download.");
                foreach (var newMatchId in newMatchIds)
                {
                    var matchDetails = await riotService.GetTftMatchDetailsAsync(newMatchId, "asia");
                    if (matchDetails != null)
                    {
                        var participant = matchDetails.Info.Participants.FirstOrDefault(p => p.Puuid == player.RiotPuuid);
                        if (participant != null)
                        {
                            var matchEntity = new TftMatch
                            {
                                MatchId = newMatchId,
                                PlayerPuuid = player.RiotPuuid,
                                Placement = participant.Placement,
                                MatchEndTime = DateTimeOffset.FromUnixTimeMilliseconds(matchDetails.Info.GameDatetime).UtcDateTime
                            };
                            context.Matches.Add(matchEntity);
                        }
                    }
                    await Task.Delay(300, stoppingToken); // Throttling downloads
                }
                // Save new matches to the database before aggregates
                await context.SaveChangesAsync(stoppingToken);
                // Calculate games played and AVP from all cached matches in DB
                var allMatches = await context.Matches
                    .Where(m => m.PlayerPuuid == player.RiotPuuid)
                    .ToListAsync(stoppingToken);
                if (allMatches.Count > 0)
                {
                    player.GamesPlayed = allMatches.Count;
                    player.AVP = Math.Round(allMatches.Average(m => m.Placement), 2);
                }
            }
            await context.SaveChangesAsync(stoppingToken);
        }
        
        _logger.LogInformation("Database sync complete. All ranks up to date!");
    }
}