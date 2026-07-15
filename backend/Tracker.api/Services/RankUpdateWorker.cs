using Microsoft.Extensions.Hosting;                     // idk
using Microsoft.Extensions.Logging;                     // idk
using Microsoft.Extensions.DependencyInjection;         // idk
using Microsoft.EntityFrameworkCore;     
using Tracker.Api.Data;
using Tracker.Api.Services;

namespace Tracker.Api.Services;

public class RankUpdateWorker : BackgroundService        // What is BackgroundService?
{
    private readonly IServiceProvider _services;         // What is IServiceProvider?
    private readonly ILogger<RankUpdateWorker> _logger;  // What is ILogger?
    private readonly TimeSpan _updateInterval = TimeSpan.FromMinutes(30); // What is TimeSpan

    public RankUpdateWorker(IServiceProvider services, ILogger<RankUpdateWorker> logger)
    {
        _services = services;
        _logger = logger;
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

        // 1. Fetch all players who have linked their Riot account
        var players = await context.Players.Where(p => !string.IsNullOrEmpty(p.RiotPuuid)).ToListAsync(stoppingToken);
        _logger.LogInformation($"Found {players.Count} active profiles to update.");

        foreach (var player in players)
        {
            _logger.LogInformation($"Checking live stats for: {player.DiscordUsername} ({player.RiotName}#{player.RiotTag})");

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
            await Task.Delay(1000, stoppingToken); 
        }

        // 3. Save updates to SQL Server
        await context.SaveChangesAsync(stoppingToken);
        _logger.LogInformation("Database sync complete. All ranks up to date!");
    }
}