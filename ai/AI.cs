using Godot;
using System;

public class AI : Node2D
{
    [Signal]
    private delegate void StateChangedSignal();


    public enum State
    {
        INVALID,
        PATROL,
        ENGAGE,
        ADVANCE
    }

    private State _currentState;

    private Area2D _playerDetectionZone;
    private Timer _patrolTimer;

    private Agent _targetAgent = null;

    private Agent _agent;

    private RandomNumberGenerator _rng;

    // Patrol State
    private Vector2 _patrolOrigin;
    private Vector2 _patrolLocation;
    private bool _patrolReached;

    private GameWorld _gameWorld;

    private Godot.Collections.Dictionary<String, Team.TeamCode> _targetAgents;

    private Vector2 _nextBasePosition = Vector2.Zero;

    private PathFinding _pathFinding;

    private Line2D _pathLine;

    public override void _Ready()
    {
        _playerDetectionZone = (Area2D)GetNode("DetectionZone");
        _patrolTimer = (Timer)GetNode("PatrolTimer");
        _pathLine = (Line2D)GetNode("PathLine");

        _rng = new RandomNumberGenerator();
        _rng.Randomize();

        _currentState = State.INVALID;

        _targetAgents = new Godot.Collections.Dictionary<String, Team.TeamCode>();
    }

    public void Initialize(GameWorld gameWorld, Agent agent, PathFinding pathFinding, float detectRaidus)
    {
        _gameWorld = gameWorld;
        _agent = agent;
        _patrolOrigin = _agent.GlobalPosition;
        _pathFinding = pathFinding;

        SetState(State.PATROL);

        CollisionShape2D detectRadius = (CollisionShape2D)(_playerDetectionZone.GetNode("CollisionShape2D"));

        CircleShape2D shape = new CircleShape2D();

        shape.Radius = detectRaidus;
        detectRadius.Shape = shape;
    }

    private void _setPathLine(Godot.Collections.Array points)
    {
        Vector2[] localPoints = new Vector2[points.Count];

        for (int index = 0; index < points.Count; index++)
        {
            if (index == 0)
            {
                localPoints[index] = Vector2.Zero;
            }
            else
            {
                localPoints[index] = ((Vector2)points[index]) - GlobalPosition;
            }


        }

        _pathLine.Points = localPoints;
    }

    public void SetState(State newState)
    {
        if (newState == _currentState)
        {
            return;
        }

        if (newState == State.PATROL)
        {
            _patrolTimer.Start();
            _patrolReached = true;
        }

        if (newState == State.ADVANCE)
        {
            _patrolOrigin = _nextBasePosition;
        }

        _currentState = newState;
        EmitSignal(nameof(StateChangedSignal), _currentState);
    }

    public void SetNextBase(CapturableBase capturableBase)
    {
        _nextBasePosition = capturableBase.GetRandomPositionWithinCaptureRadius();
    }

    public State getState()
    {
        return _currentState;
    }

    public void Control(float delta)
    {
        _pathLine.GlobalRotation = 0;

        // If not reloading, then set to default
        if (_agent.RightWeaponAction != (int)GameStates.PlayerInput.InputAction.RELOAD)
        {
            _agent.RightWeaponAction = (int)GameStates.PlayerInput.InputAction.NOT_TRIGGER;
        }
        if (_agent.LeftWeaponAction != (int)GameStates.PlayerInput.InputAction.RELOAD)
        {
            _agent.LeftWeaponAction = (int)GameStates.PlayerInput.InputAction.NOT_TRIGGER;
        }

        Godot.Collections.Array pathPoints = null;

        switch (_currentState)
        {
            case State.PATROL:
                if (!_patrolReached)
                {
                    pathPoints = _pathFinding.GetPath(GlobalPosition, _patrolLocation);

                    if (pathPoints.Count > 1)
                    {
                        Vector2 nextPoint = (Vector2)pathPoints[1];
                        _agent.RotateToward(nextPoint, delta);
                        _agent.MoveToward(_agent.GlobalPosition.DirectionTo(nextPoint), delta);
                        _setPathLine(pathPoints);
                    }
                    else
                    {
                        _patrolReached = true;
                        _patrolTimer.Start();
                        _pathLine.ClearPoints();
                    }
                }

                break;

            case State.ENGAGE:
                if (_targetAgent != null && IsInstanceValid(_targetAgent))
                {
                    _agent.RotateToward(_targetAgent.GlobalPosition, delta);

                    // Calculate rotation
                    float angelToTargetAgent = _agent.GlobalPosition.DirectionTo(_targetAgent.GlobalPosition).Angle();

                    // Only start fire when agent is closely faced to its target agent
                    if (Mathf.Abs(_agent.GlobalRotation - angelToTargetAgent) < 0.3)
                    {
                        _agent.RightWeaponAction = (int)GameStates.PlayerInput.InputAction.TRIGGER;
                        _agent.LeftWeaponAction = (int)GameStates.PlayerInput.InputAction.TRIGGER;
                    }

                    // Chanse engaged agent closer if possible
                    if (_agent.GlobalPosition.DistanceTo(_targetAgent.GlobalPosition) > 600.0f)
                    {
                        _agent.MoveToward(_agent.GlobalPosition.DirectionTo(_targetAgent.GlobalPosition), delta);
                    }
                }
                else
                {
                    _checkTarget();
                }
                break;

            case State.ADVANCE:

                pathPoints = _pathFinding.GetPath(GlobalPosition, _nextBasePosition);

                if (pathPoints.Count > 1)
                {
                    Vector2 nextPoint = (Vector2)pathPoints[1];
                    _agent.RotateToward(nextPoint, delta);
                    _agent.MoveToward(_agent.GlobalPosition.DirectionTo(nextPoint), delta);
                    _setPathLine(pathPoints);
                }
                else
                {
                    SetState(State.PATROL);
                    _pathLine.ClearPoints();
                }
                break;

            default:
                break;

        }
    }

    private void _onWeaponNeedReload(Weapon.WeaponOrder weaponOrder)
    {
        if (weaponOrder == Weapon.WeaponOrder.Left)
        {
            _agent.LeftWeaponAction = (int)GameStates.PlayerInput.InputAction.RELOAD;
        }
        else
        {
            _agent.RightWeaponAction = (int)GameStates.PlayerInput.InputAction.RELOAD;
        }
    }

    private void _onWeaponReload(Weapon.WeaponOrder weaponOrder, bool isReloading)
    {
        if (weaponOrder == Weapon.WeaponOrder.Left)
        {
            _agent.LeftWeaponAction = (int)GameStates.PlayerInput.InputAction.NOT_TRIGGER;
        }
        else
        {
            _agent.RightWeaponAction = (int)GameStates.PlayerInput.InputAction.NOT_TRIGGER;
        }

    }

    private void _onDetectionZoneBodyEntered(Node body)
    {

        if (body.HasMethod(nameof(Agent.GetCurrentTeam)) && body != _agent)
        {
            // If not same team identifier, identify as target
            if (((Agent)body).GetCurrentTeam() != _agent.GetCurrentTeam())
            {
                Agent agent = (Agent)body;
                if (_targetAgent == null)
                {
                    _targetAgent = agent;
                    SetState(State.ENGAGE);
                }
                else
                {
                    // Save as list of future target
                    if (!_targetAgents.ContainsKey(agent.GetUnitName()))
                    {
                        _targetAgents.Add(agent.GetUnitName(), agent.GetCurrentTeam());
                    }
                }
            }
        }
    }

    private void _onDetectionZoneBodyExited(Node body)
    {
        if (body.HasMethod(nameof(Agent.GetCurrentTeam)) && body != _agent)
        {
            Agent agent = (Agent)body;

            // Clean up current target/possible target
            if (body == _targetAgent)
            {
                _targetAgent = null;
            }
            else
            {
                if (_targetAgents.ContainsKey(agent.GetUnitName()))
                {
                    _targetAgents.Remove(agent.GetUnitName());
                }
            }

            // If target is not vaild, then will try to check for next target
            if (_targetAgent == null || IsInstanceValid(_targetAgent))
            {
                // Compute target
                _checkTarget();
            }
        }
    }

    private void _checkTarget()
    {
        _targetAgent = null;

        Godot.Collections.Array<String> removeTargetList = new Godot.Collections.Array<String>();

        foreach (String targetAgentUnitName in _targetAgents.Keys)
        {
            Agent targetAgent = _gameWorld.GetTeamMapAIs()[(int)_targetAgents[targetAgentUnitName]].GetUnit(targetAgentUnitName);

            if (targetAgent != null && IsInstanceValid(targetAgent))
            {
                SetState(State.ENGAGE);
                _targetAgent = targetAgent;
            }
            else
            {
                // Remove this target from list as it is no longer valid
                removeTargetList.Add(targetAgentUnitName);
            }
        }

        foreach (String targetAgentUnitName in removeTargetList)
        {
            _targetAgents.Remove(targetAgentUnitName);
        }

        // If no possible target, then set to ADVANCE
        if (_targetAgent == null)
        {
            // Set advance if next target is available
            if (_nextBasePosition != Vector2.Zero)
            {
                SetState(State.ADVANCE);
            }
            else
            {
                SetState(State.PATROL);
            }
        }
    }

    private void _onPatrolTimerTimeout()
    {
        _patrolReached = false;
        float patrolRange = 100f;

        float randomX = _rng.RandfRange(-patrolRange, patrolRange);
        float randomY = _rng.RandfRange(-patrolRange, patrolRange);

        _patrolLocation.x = randomX;
        _patrolLocation.y = randomY;

        _patrolLocation = _patrolLocation + _patrolOrigin;
    }
}
