using Godot;
using System;

public class TeamMapAI : Node2D
{
    enum BaseCaptureStartOrder
    {
        FIRST,
        LAST
    }

    [Export]
    private BaseCaptureStartOrder baseCaptureStartOrder = BaseCaptureStartOrder.FIRST;

    private Godot.Collections.Array _bases;
    private Team _team;

    private Node _unitsContainer;

    private GameWorld _gameWorld;

    [Export]
    private int _maxUnitUsageAmount = 1000;

    [Export]
    private int _maxConcurrentUnit = 10;

    [Export]
    private int _advancedWaitInterval = 20;

    private int _currentUnitUsageAmount;

    private int _unitCost = 100;

    
    private Timer _advancedTimer;

    public override void _Ready()
    {
        _team = (Team)GetNode("Team");
        _unitsContainer = GetNode("UnitsContainer");

        _currentUnitUsageAmount = _maxUnitUsageAmount;

        _advancedTimer = (Timer)GetNode("AdvancedTimer");
        _advancedTimer.WaitTime = _advancedWaitInterval;
    }

    public Team.TeamCode GetCurrentTeam()
    {
        return _team.CurrentTeamCode;
    }

    public int GetMaxUnitUsageAmount()
    {
        return _maxUnitUsageAmount;
    }

    public void SetMaxUnitUsageAmount(int maxUnitUsageAmount)
    {
        _maxUnitUsageAmount = maxUnitUsageAmount;
        _currentUnitUsageAmount = _maxUnitUsageAmount;
    }

    public bool isUnitUsageAmountAllowed()
    {
        if (_currentUnitUsageAmount < _unitCost || _currentUnitUsageAmount < 0)
        {
            return false;
        }

        return true;
    }

    public int GetMaxConcurrentUnit()
    {
        return _maxConcurrentUnit;
    }

    public void SetMaxConcurrentUnit(int maxConcurrentUnit)
    {
        _maxConcurrentUnit = maxConcurrentUnit;
    }

    public bool isConcurrentUnitAllowed()
    {
        if (_unitsContainer.GetChildren().Count >= _maxConcurrentUnit)
        {
            return false;
        }

        return true;
    }

    public bool isNewUnitAllow()
    {
        if (!isUnitUsageAmountAllowed())
        {
            return false;
        }

        if (!isConcurrentUnitAllowed())
        {
            return false;
        }

        if (GetSpawnPointFromCaptureBase() == null)
        {
            return false;
        }

        return true;
    }

    public void Initialize(GameWorld gameWorld, Godot.Collections.Array bases, Team.TeamCode team)
    {
        _bases = bases;
        _team.CurrentTeamCode = team;
        _gameWorld = gameWorld;
        _advancedTimer.Start();

        CheckForCapturableBase();
    }

    public void CheckForCapturableBase()
    {
        CapturableBase capturableBase = _getNextCapturableBase();

        if (capturableBase != null)
        {
            _assignNextCapturableBaseToUnits(capturableBase);
        }
    }

    public void HandleCapturableBaseCaptured(CapturableBase capturable)
    {
        CheckForCapturableBase();
    }

    public Node GetUnitsContainer()
    {
        return _unitsContainer;
    }

    private CapturableBase _getNextCapturableBase()
    {
        if (baseCaptureStartOrder == BaseCaptureStartOrder.LAST)
        {
            for (int index = _bases.Count - 1; index > 0; index--)
            {
                if (((CapturableBase)_bases[index]).GetCaptureBaseTeam() != _team.CurrentTeamCode)
                {
                    return (CapturableBase)(_bases[index]);
                }
            }
        }
        else
        {
            for (int index = 0; index < _bases.Count; index++)
            {
                if (((CapturableBase)_bases[index]).GetCaptureBaseTeam() != _team.CurrentTeamCode)
                {
                    return (CapturableBase)(_bases[index]);
                }
            }
        }

        return null;
    }

    private void _assignNextCapturableBaseToUnits(CapturableBase capturableBase)
    {
        foreach (Node node in _unitsContainer.GetChildren())
        {
            if (node.HasMethod(nameof(AIAgent.GetAI)))
            {
                AI agentAI = ((AIAgent)node).GetAI();
                agentAI.SetNextBase(capturableBase);
                agentAI.SetState(AI.State.ADVANCE);
            }
        }
    }

    public Agent CreateUnit(String unitName, String displayName, Boolean enableAI)
    {
        Agent unit = null;

        if (enableAI)
        {
            unit = (AIAgent)((PackedScene)GD.Load("res://agents/AIAgent.tscn")).Instance();
        }
        else
        {
            unit = (Agent)((PackedScene)GD.Load("res://agents/Player.tscn")).Instance();
        }

        unit.Name = unitName;

        unit.SetNetworkMaster(1);

        unit.GlobalPosition = GetSpawnPointFromCaptureBase().GlobalPosition;

        _unitsContainer.AddChild(unit);

        // Set the info afterward as some of these depend on child node to be available
        unit.Initialize(_gameWorld, unitName, displayName, _team.CurrentTeamCode);

        unit.changePrimaryWeapon(0);

        _currentUnitUsageAmount -= _unitCost;

        return unit;
    }

    public void RemoveUnit(String unitID)
    {
        Agent agent = GetUnit(unitID);


        if (agent == null || !IsInstanceValid(agent))
        {
            GD.Print("Cannoot remove invalid node from tree");
            return;
        }
        // Mark the node for deletion
        agent.explode();
    }

    public CapturableBase GetSpawnPointFromCaptureBase()
    {
        CapturableBase targetCaptureBase = null;
        CapturableBase neutralCaptureBase = null;

        foreach (CapturableBase captureBase in _bases)
        {
            if (captureBase.GetCaptureBaseTeam() == _team.CurrentTeamCode)
            {
                targetCaptureBase = captureBase;
                break;
            }

            if (captureBase.GetCaptureBaseTeam() == Team.TeamCode.NEUTRAL)
            {
                neutralCaptureBase = captureBase;
            }
        }

        // If not on same team base, try get a neutral base
        if (targetCaptureBase == null)
        {
            targetCaptureBase = neutralCaptureBase;
        }

        return targetCaptureBase;
    }

    public Agent GetUnit(String unitName)
    {
        if (_unitsContainer.HasNode(unitName))
        {
            return (Agent)_unitsContainer.GetNode(unitName);
        }
        else
        {
            foreach(Agent agent in _unitsContainer.GetChildren())
            {
                if(agent.GetUnitName().Equals(unitName))
                {
                    return agent;
                }
            }
        }

        return null;
    }
}
