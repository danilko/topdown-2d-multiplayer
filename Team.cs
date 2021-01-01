using Godot;
using System;

public class Team : Node2D
{
    public enum TeamCode {
        TEAMALPHA,
        TEAMBETA,
        TEAMDELTA,
        TEAMUNKOWN,
        NEUTRAL
    }

    private Color [] _teamColor = {
        new Color("00ffff"), 
        new Color("ff2400"), 
        new Color("00ff40"),
        new Color("c15dff"),
        new Color("ffffff"),
        };

    [Export]
    public TeamCode CurrentTeamCode {get; set;}

    public override void _Ready()
    {
        base._Ready();
        CurrentTeamCode = TeamCode.NEUTRAL;
    }

    public Color getTeamColor(TeamCode teamCode)
    {
        return _teamColor[(int)teamCode];
    }
}
