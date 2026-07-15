using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Tracker.Api.Services;

public class RiotApiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    public RiotApiService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Riot:ApiKey"]!;

        _httpClient.DefaultRequestHeaders.Add("X-Riot-Token", _apiKey);
    }

    public async Task<RiotAccountDto?> GetAccountByRiotIdAsync(string gameName, string tagLine, string regionCluster = "asia")
    {
        var url = $"https://{regionCluster}.api.riotgames.com/riot/account/v1/accounts/by-riot-id/{gameName}/{tagLine}";

        try
        {
            return await _httpClient.GetFromJsonAsync<RiotAccountDto>(url);
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<TftLeagueEntryDto>?> GetTftRankByPuuidAsync(string puuid, string platform = "sg2")
    {
        var url = $"https://{platform}.api.riotgames.com/tft/league/v1/by-puuid/{puuid}";

        try
        {
            return await _httpClient.GetFromJsonAsync<List<TftLeagueEntryDto>>(url);
        }
        catch
        {
            return null;
        }
    }
}

public class RiotAccountDto
{
    [JsonPropertyName("puuid")] public string Puuid {get; set;} = string.Empty;
    [JsonPropertyName("gameName")] public string GameName {get; set;} = string.Empty;
    [JsonPropertyName("tagLine")] public string TagLine {get; set;} = string.Empty;
}

public class TftLeagueEntryDto
{
    [JsonPropertyName("tier")] public string Tier {get; set;} = string.Empty;
    [JsonPropertyName("rank")] public string Rank {get; set;} = string.Empty;
    [JsonPropertyName("leaguePoints")] public int LeaguePoints {get; set;}
    [JsonPropertyName("queueType")] public string QueueType {get; set;} = string.Empty;
}