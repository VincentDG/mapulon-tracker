using System;

namespace Tracker.Api.Models;

public class Player
{
    public int Id {get; set;}

    public string DiscordId {get; set;} = string.Empty;
    public string DiscordUsername {get; set;} = string.Empty;
    public string DiscordAvatarHash {get; set;} = string.Empty;
    public string DiscordGlobalName {get; set;} = string.Empty;

    public string RiotName {get; set;} = string.Empty;
    public string RiotTag {get; set;} = string.Empty;
    public string RiotPuuid {get; set;} = string.Empty;
    public int ProfileIconId {get; set;} = 0;

    public string CurrentTier {get; set;} = "UNRANKED";
    public string CurrentRank {get; set;} = "IV";
    public int CurrentLP {get; set;} = 0;

    public bool HasHitMasterThisSet {get; set;} = false;
    public DateTime? TimestampHitMaster {get; set;}

    public bool IsOptedIn {get; set;} = false;
    public int GamesPlayed {get; set;} = 0;
    public double AVP {get; set;} = 0.0;

}