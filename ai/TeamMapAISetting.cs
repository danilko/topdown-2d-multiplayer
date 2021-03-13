using Godot;
using System;

public class TeamMapAISetting : Resource
{
    public Team.TeamCode TeamCode {get;set;}
    public int Budget {get; set;}
    public bool AutoSpawnMember {get; set;}
}
