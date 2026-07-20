using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
namespace Tracker.Api.Services;
public class RiotApiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<RiotApiService> _logger;
    public RiotApiService(HttpClient httpClient, IConfiguration configuration, ILogger<RiotApiService> logger)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Riot:ApiKey"]!;
        _logger = logger;
        _httpClient.DefaultRequestHeaders.Add("X-Riot-Token", _apiKey);
    }
    public async Task<RiotAccountDto?> GetAccountByRiotIdAsync(string gameName, string tagLine, string regionCluster = "asia")
    {
        var url = $"https://{regionCluster}.api.riotgames.com/riot/account/v1/accounts/by-riot-id/{gameName}/{tagLine}";
        _logger.LogInformation("Querying Riot Account: {Url}", url);
        try
        {
            var result = await _httpClient.GetFromJsonAsync<RiotAccountDto>(url);
            _logger.LogInformation("Riot Account found: PUUID={Puuid}, Name={Name}#{Tag}", result?.Puuid, result?.GameName, result?.TagLine);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch Riot Account. Status: {StatusCode}, Error: {Error}", ex.StatusCode, ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching Riot Account.");
            return null;
        }
    }
    public async Task<SummonerDto?> GetSummonerByPuuidAsync(string puuid, string platform = "sg2")
    {
        var url = $"https://{platform}.api.riotgames.com/tft/summoner/v1/summoners/by-puuid/{puuid}";
        _logger.LogInformation("Querying Summoner Profile: {Url}", url);
        try
        {
            var result = await _httpClient.GetFromJsonAsync<SummonerDto>(url);
            _logger.LogInformation("Summoner Profile returned: IconId={IconId}", result?.ProfileIconId);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch Summoner profile. Status: {StatusCode}, Error: {Error}", ex.StatusCode, ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching Summoner profile.");
            return null;
        }
    }
    public async Task<List<TftLeagueEntryDto>?> GetTftRankByPuuidAsync(string puuid, string platform = "sg2")
    {
        var url = $"https://{platform}.api.riotgames.com/tft/league/v1/by-puuid/{puuid}";
        _logger.LogInformation("Querying TFT League Ranks: {Url}", url);
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<TftLeagueEntryDto>>(url);
            _logger.LogInformation("TFT League returned {Count} rank entries.", result?.Count ?? 0);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch TFT leagues. Status: {StatusCode}, Error: {Error}", ex.StatusCode, ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching TFT leagues.");
            return null;
        }
    }
    public async Task<List<string>?> GetTftMatchIdsAsync(string puuid, long startTime, int count = 100, string regionCluster = "sea")
    {
        var url = $"https://{regionCluster}.api.riotgames.com/tft/match/v1/matches/by-puuid/{puuid}/ids?startTime={startTime}&count={count}";
        _logger.LogInformation("Querying TFT Match IDs: {Url}", url);
        try
        {
            var result = await _httpClient.GetFromJsonAsync<List<string>>(url);
            _logger.LogInformation("TFT Match IDs found: {Count} matches. List: [{Matches}]", result?.Count ?? 0, result != null ? string.Join(", ", result) : "");
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch TFT match IDs. Status: {StatusCode}, Error: {Error}", ex.StatusCode, ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching TFT match IDs.");
            return null;
        }
    }
    public async Task<TftMatchDto?> GetTftMatchDetailsAsync(string matchId, string regionCluster = "sea")
    {
        var url = $"https://{regionCluster}.api.riotgames.com/tft/match/v1/matches/{matchId}";
        _logger.LogInformation("Querying TFT Match Details: {Url}", url);
        try
        {
            var result = await _httpClient.GetFromJsonAsync<TftMatchDto>(url);
            _logger.LogInformation("TFT Match Details returned successfully for: {MatchId}", matchId);
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch TFT match details. Status: {StatusCode}, Error: {Error}", ex.StatusCode, ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching TFT match details.");
            return null;
        }
    }
}
public class RiotAccountDto
{
    [JsonPropertyName("puuid")] public string Puuid { get; set; } = string.Empty;
    [JsonPropertyName("gameName")] public string GameName { get; set; } = string.Empty;
    [JsonPropertyName("tagLine")] public string TagLine { get; set; } = string.Empty;
}
public class SummonerDto
{
    [JsonPropertyName("profileIconId")] public int ProfileIconId { get; set; }
}
public class TftLeagueEntryDto
{
    [JsonPropertyName("tier")] public string Tier { get; set; } = string.Empty;
    [JsonPropertyName("rank")] public string Rank { get; set; } = string.Empty;
    [JsonPropertyName("leaguePoints")] public int LeaguePoints { get; set; }
    [JsonPropertyName("queueType")] public string QueueType { get; set; } = string.Empty;
}

public class TftMatchDto
{
    [JsonPropertyName("metadata")] public TftMatchMetadata Metadata { get; set; } = new();
    [JsonPropertyName("info")] public TftMatchInfo Info { get; set; } = new();
}
public class TftMatchMetadata
{
    [JsonPropertyName("match_id")] public string MatchId { get; set; } = string.Empty;
}
public class TftMatchInfo
{
    [JsonPropertyName("game_datetime")] public long GameDatetime { get; set; }
    [JsonPropertyName("participants")] public List<TftMatchParticipant> Participants { get; set; } = new();
}
public class TftMatchParticipant
{
    [JsonPropertyName("puuid")] public string Puuid { get; set; } = string.Empty;
    [JsonPropertyName("placement")] public int Placement { get; set; }
}