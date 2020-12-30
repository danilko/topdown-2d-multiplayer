using Godot;
using System;

public class Team : Node2D
{
    public enum TeamCode {
        TEAMALPHA,
        TEAMBETA,
        TEAMDELTA,
        TEAMUNKOWN,
    }

    [Export]
    public TeamCode CurrentTeamCode {get; set;}

    public override void _Ready()
    {
        base._Ready();
        CurrentTeamCode = TeamCode.TEAMALPHA;
    }
}
