using System;

namespace Tracker.Api.Models;

public class Player
{
    public int Id {get; set;}

    public string DiscordId {get; set;} = string.Empty;
    public string DiscordUsername {get; set;} = string.Empty;

    public string RiotName {get; set;} = string.Empty;
    public string RiotTag {get; set;} = string.Empty;

    public string RiotPuuid {get; set;} = string.Empty;

    public string CurrentTier {get; set;} = "UNRANKED";
    public string CurrentRank {get; set;} = "IV";
    public int CurrentLP {get; set;} = 0;

    public bool HasHitMasterThisSet {get; set;} = false;
    public DateTime? TimestampHitMaster {get; set;}

}