using Godot;
using System;

public class TeamMapAISetting : Resource
{
    public enum AILevel {
        WEAK,
        MEDIUM,
        STRONG
    }

    public Team.TeamCode TeamCode {get;set;}
    public int Budget {get; set;}
    public bool AIControl {get; set;}

    public AILevel TeamAILevel {get; set;}

    public int TotalUnitCount {get; set;}
}
