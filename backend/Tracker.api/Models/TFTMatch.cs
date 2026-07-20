using System;

namespace Tracker.Api.Models;

public class TftMatch
{
    public int Id { get; set; }
    public string MatchId { get; set; } = string.Empty;
    public string PlayerPuuid { get; set; } = string.Empty;
    public int Placement { get; set; }
    public DateTime MatchEndTime { get; set; }
}