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
        ENGAGE
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

    private float _patrolReachedRadius = 5.0f;

    public override void _Ready()
    {
        _playerDetectionZone = (Area2D)GetNode("DetectionZone");
        _patrolTimer = (Timer)GetNode("PatrolTimer");
        _rng = new RandomNumberGenerator();
        _rng.Randomize();
        _currentState = State.INVALID;
    }

    public void Initialize(Agent agent, float detectRaidus)
    {
        _agent = agent;
        _patrolOrigin = _agent.GlobalPosition;

        setState(State.PATROL);

        CollisionShape2D detectRadius = (CollisionShape2D)(_playerDetectionZone.GetNode("CollisionShape2D"));

        CircleShape2D shape = new CircleShape2D();

        shape.Radius = detectRaidus;
        detectRadius.Shape = shape;

    }

    public void setState(State newState)
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

        _currentState = newState;
        EmitSignal(nameof(StateChangedSignal), _currentState);
    }

    public State gsetState()
    {
        return _currentState;
    }

    public void Control(float delta)
    {
        // If not reloading, then set to default
        if (_agent.PrimaryWeaponAction != (int)GameStates.PlayerInput.InputAction.RELOAD)
        {
            _agent.PrimaryWeaponAction = (int)GameStates.PlayerInput.InputAction.NOT_TRIGGER;
        }
        if (_agent.SecondaryWeaponAction != (int)GameStates.PlayerInput.InputAction.RELOAD)
        {
            _agent.SecondaryWeaponAction = (int)GameStates.PlayerInput.InputAction.NOT_TRIGGER;
        }

        switch (_currentState)
        {
            case State.PATROL:
                if (!_patrolReached)
                {
                    _agent.MoveToward(_agent.GlobalPosition.DirectionTo(_patrolLocation), delta);
                    _agent.RotateToward(_patrolLocation, delta);

                    // Start the next patrol timer if reach target
                    if (_agent.GlobalPosition.DistanceTo(_patrolLocation) < _patrolReachedRadius)
                    {
                        _patrolReached = true;
                        _patrolTimer.Start();
                    }
                }

                break;
            case State.ENGAGE:
                if (IsInstanceValid(_targetAgent))
                {
                    _agent.RotateToward(_targetAgent.GlobalPosition, delta);

                    // Calculate rotation
                    float angelToTargetAgent = _agent.GlobalPosition.DirectionTo(_targetAgent.GlobalPosition).Angle();

                    // Only start fire when agent is closely faced to its target agent
                    if (Mathf.Abs(_agent.GlobalRotation - angelToTargetAgent) < 0.1)
                    {
                        // Only can fire if not in reload
                        if (_agent.PrimaryWeaponAction != (int)GameStates.PlayerInput.InputAction.RELOAD)
                        {
                            _agent.PrimaryWeaponAction = (int)GameStates.PlayerInput.InputAction.TRIGGER;
                        }
                        if (_agent.SecondaryWeaponAction != (int)GameStates.PlayerInput.InputAction.RELOAD)
                        {
                            _agent.SecondaryWeaponAction = (int)GameStates.PlayerInput.InputAction.TRIGGER;
                        }
                    }
                }
                break;
            default:
                break;

        }
    }

    private void _onPrimaryWeaponNeedReload()
    {
        _agent.PrimaryWeaponAction = (int)GameStates.PlayerInput.InputAction.RELOAD;
    }

    private void _onPrimaryWeaponReloadStop()
    {
        _agent.PrimaryWeaponAction = (int)GameStates.PlayerInput.InputAction.NOT_TRIGGER;
                GD.Print("AI reload complete");
    }


    private void _onSecondaryWeaponNeedReload()
    {
        _agent.SecondaryWeaponAction = (int)GameStates.PlayerInput.InputAction.RELOAD;
    }

    private void _onSecondaryWeaponReloadStop()
    {
        _agent.SecondaryWeaponAction = (int)GameStates.PlayerInput.InputAction.NOT_TRIGGER;
    }

    private void _onDetectionZoneBodyEntered(Node body)
    {
        if (body.HasMethod(nameof(Agent.GetTeam)) && body != _agent)
        {
            // If not same team identifier, identify as target
            if (((Agent)body).GetTeam() != _agent.GetTeam())
            {
                _targetAgent = (Agent)body;
                setState(State.ENGAGE);
            }
        }
    }

    private void _onDetectionZoneBodyExited(Node body)
    {
        if (body == _targetAgent)
        {
            setState(State.PATROL);
            _targetAgent = null;
        }
    }

    private void _onPatrolTimerTimeout()
    {
        _patrolReached = false;
        float patrolRange = 1000f;

        float randomX = _rng.RandfRange(-patrolRange, patrolRange);
        float randomY = _rng.RandfRange(-patrolRange, patrolRange);

        _patrolLocation.x = randomX;
        _patrolLocation.y = randomY;

        _patrolLocation = _patrolLocation + _patrolOrigin;
    }
}
