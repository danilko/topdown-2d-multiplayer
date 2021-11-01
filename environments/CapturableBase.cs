using Godot;
using System;
using System.Collections.Generic;

public class CapturableBase : Area2D
{
    private List<int> agentsEntered;

    private Team _team;

    [Signal]
    public delegate void BaseTeamChangeSignal();

    [Export]
    private int _timeToCaptureBase = 10;

    [Export]
    private Team.TeamCode _defaultTeamCode = Team.TeamCode.NEUTRAL;

    private Team.TeamCode _captureTeamCode;
    private Sprite _base;
    private Sprite _boundry;
    private Timer _timer;
    private GameWorld _gameWorld;
    private UnitDisplay _unitDisplay;
    private Label _unitDisplayLabel;

    private CollisionShape2D _collisionShape;
    private RandomNumberGenerator _rng;

    private int _counter = 0;

    public override void _Ready()
    {
        _team = (Team)GetNode("Team");
        _team.CurrentTeamCode = _defaultTeamCode;

        _captureTeamCode = _team.CurrentTeamCode;
        _unitDisplayLabel = ((Label)(GetNode("UnitDisplay/Name")));
        _unitDisplay = (UnitDisplay)GetNode("UnitDisplay");

        _collisionShape = (CollisionShape2D)GetNode("CollisionShape2D");

        _rng = new RandomNumberGenerator();
        _rng.Randomize();
        
        _base = (Sprite)GetNode("Base");
        _boundry = (Sprite)GetNode("Boundry");

        _timer = (Timer)GetNode("CaptureTimer");

        agentsEntered = new List<int>();

        for (int index = 0; index < Enum.GetNames(typeof(Team.TeamCode)).Length; index++)
        {
            agentsEntered.Add(0);
        }

        // Update unit display
        SetCaptureBaseTeam(_team.CurrentTeamCode);
    }

    public void Initialize(GameWorld gameWorld)
    {
        _gameWorld = gameWorld;
    }

    public Team.TeamCode GetCaptureBaseTeam()
    {
        return _team.CurrentTeamCode;
    }

    public void SetCaptureBaseTeam(Team.TeamCode team)
    {
        _team.CurrentTeamCode = team;
        _base.Modulate = _team.getTeamColor(_team.CurrentTeamCode);
        _boundry.Modulate = _team.getTeamColor(_team.CurrentTeamCode);
        _unitDisplayLabel.Text = Name + " (" + _team.CurrentTeamCode + ")";
        _counter = _timeToCaptureBase;

        EmitSignal(nameof(BaseTeamChangeSignal), this);
    }

    public Vector2 GetRandomPositionWithinCaptureRadius()
    {
        Vector2 extents = (Vector2)_collisionShape.Shape.Get("extents");
        Vector2 topLeftCorner =  _collisionShape.GlobalPosition - extents;
        float randX = _rng.RandfRange(topLeftCorner.x, topLeftCorner.x + extents.x);
        float randY = _rng.RandfRange(topLeftCorner.y, topLeftCorner.y + extents.y);

        return new Vector2(randX, randY);
    }

    private void _onCapturableBaseBodyEntered(Node2D body)
    {
        if (body.HasMethod(nameof(Agent.GetTeam)))
        {
            Agent agent = (Agent)body;
            agentsEntered[(int)agent.GetTeam()] = (int)agentsEntered[(int)agent.GetTeam()] + 1;

            checkIsBaseCaptured();
        }
    }

    private void _onCapturableBaseBodyExited(Node2D body)
    {
        if (body.HasMethod(nameof(Agent.GetTeam)))
        {
            Agent agent = (Agent)body;
            agentsEntered[(int)agent.GetTeam()] = (int)agentsEntered[(int)agent.GetTeam()] - 1;
            if ((int)agentsEntered[(int)agent.GetTeam()] < 0)
            {
                agentsEntered[(int)agent.GetTeam()] = 0;
            }

            checkIsBaseCaptured();
        }
    }

    private void checkIsBaseCaptured()
    {
        int currentMajorityTeam = _calculateBaseMajority();

        // Only start counting if:
        // New majority team is different from current base team
        // New majority team is not neutral
        if (currentMajorityTeam != (int)_team.CurrentTeamCode && currentMajorityTeam != (int)Team.TeamCode.NEUTRAL)
        {
            // If is different from current working in progress capture team
            if ((int)_captureTeamCode != currentMajorityTeam)
            {
                // Restart counter as previous timer is for different capture team
                _counter = 0;
                updateUnitDisplay();
                // Set to the new capture team
                _captureTeamCode = (Team.TeamCode)currentMajorityTeam;
                _timer.Start();
            }
        }
        else
        {
            // Reset back to current captured team
            _captureTeamCode = _team.CurrentTeamCode;
            _counter = _timeToCaptureBase;
            updateUnitDisplay();
            _timer.Stop();
        }

    }

    private void updateUnitDisplay()
    {
        _unitDisplay.UpdateUnitBar(_counter * 100 / _timeToCaptureBase);
    }

    private int _calculateBaseMajority()
    {
        int captureIndex = (int)Team.TeamCode.NEUTRAL;
        int largestTeamCount = 0;

        // Count the largest of every team, except NEUTRAL, which is the last one
        // As NEUTRAL is not a team
        for (int index = 0; index < (int)Team.TeamCode.NEUTRAL; index++)
        {
            if ((int)agentsEntered[index] > largestTeamCount)
            {
                captureIndex = index;
                largestTeamCount = (int)agentsEntered[index];
            }
        }

        return captureIndex;
    }

    private void _updateBaseTeam()
    {
        if (_counter != _timeToCaptureBase)
        {
            _counter++;
            updateUnitDisplay();
            _timer.Start();

            if (_counter == _timeToCaptureBase)
            {
                SetCaptureBaseTeam(_captureTeamCode);
            }
        }
        else
        {
            _timer.Stop();
        }
    }
}
