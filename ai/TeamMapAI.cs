using Godot;
using System;

public class TeamMapAI : Node2D
{
    enum BaseCaptureStartOrder
    {
        FIRST,
        LAST
    }

    private Godot.Collections.Array<CapturableBase> _bases;
    private Team _team;

    [Export]
    private BaseCaptureStartOrder baseCaptureStartOrder;

    public override void _Ready()
    {
        _team = (Team)GetNode("Team");
    }

    public void Initialize(Godot.Collections.Array<CapturableBase> bases, Team.TeamCode team)
    {
        _bases = bases;
        _team.CurrentTeamCode = team;
        getNextCapturableBase();
    }

    public CapturableBase getNextCapturableBase()
    {
        if (baseCaptureStartOrder == BaseCaptureStartOrder.LAST)
        {
            for (int index = _bases.Count - 1; index > 0; index--)
            {
                if (_bases[index].GetCaptureBaseTeam() != _team.CurrentTeamCode)
                {
                    return _bases[index];
                }
            }
        }
        else
        {
            for (int index = 0; index < _bases.Count; index++)
            {
                if (_bases[index].GetCaptureBaseTeam() != _team.CurrentTeamCode)
                {
                    return _bases[index];
                }
            }
        }


        return null;
    }

    private void _assignNextCapturableBaseToUnits()
    {
        CapturableBase captureableBase = getNextCapturableBase();
        if (captureableBase != null)
        {
            foreach (Node2D node in GetChildren())
            {
                if (node.HasMethod(nameof(Agent.GetTeam)))
                {


                }
            }
        }
    }
}
