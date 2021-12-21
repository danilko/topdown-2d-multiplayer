using Godot;
using System;
using System.Collections.Generic;

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

    public enum PathRequestState
    {
        NONE,
        REQUEST,
        READY
    }

    private PathRequestState _pathRequestState;
    private State _currentState;

    private Timer _patrolTimer;

    private Agent _agent;

    private RandomNumberGenerator _rng;

    // Patrol State
    private Vector2 _patrolOrigin;
    private Vector2 _patrolLocation;
    private bool _patrolReached;

    private GameWorld _gameWorld;

    private Vector2 _nextBasePosition = Vector2.Zero;

    private PathFinding _pathFinding;

    private Line2D _pathLine;

    private DetectionZone _detectionZone;

    private Agent _targetAgent;

    List<Vector2> _pathPoints;


    private Vector2 _engagePositionOrign;
    private Boolean _engagePositionSet;

    private RandomNumberGenerator _random;

    public override void _Ready()
    {

        _patrolTimer = (Timer)GetNode("PatrolTimer");
        _pathLine = (Line2D)GetNode("PathLine");

        _rng = new RandomNumberGenerator();
        _rng.Randomize();

        _currentState = State.INVALID;
        _pathRequestState = PathRequestState.NONE;
        _pathPoints = null;

        _random = new RandomNumberGenerator();
    }

    public void Initialize(GameWorld gameWorld, Agent agent, PathFinding pathFinding, DetectionZone detectionZone)
    {
        _gameWorld = gameWorld;
        _agent = agent;
        _patrolOrigin = _agent.GlobalPosition;
        _pathFinding = pathFinding;
        _detectionZone = detectionZone;

        SetState(State.PATROL);

        _engagePositionOrign = Vector2.Zero;
        _engagePositionSet = false;
    }

    private void _setPathLine(List<Vector2> points)
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

    private void _resetPath()
    {
        // Reset the path to let system know about next path
        if (_pathPoints != null)
        {
            _pathPoints.Clear();
            _pathPoints = null;
        }

        _pathRequestState = PathRequestState.NONE;
    }

    public void SetState(State newState)
    {
        if (newState == _currentState)
        {
            return;
        }

        // Reset the path to let system to request next path
        _resetPath();

        _pathRequestState = PathRequestState.NONE;

        if (newState == State.PATROL)
        {
            _patrolTimer.Start();
            _patrolReached = true;
        }

        if (newState == State.ADVANCE)
        {
            _patrolOrigin = _nextBasePosition;
        }

        // Default all state will change to weapon 0
        if (_agent.GetCurrentWeaponIndex(Weapon.WeaponOrder.Left) != 0)
        {
            _agent.ChangeWeapon(0, Weapon.WeaponOrder.Left);
        }

        _currentState = newState;
        EmitSignal(nameof(StateChangedSignal), _currentState);
    }

    public void SetNextBase(CapturableBase capturableBase)
    {
        _nextBasePosition = capturableBase.GetRandomPositionWithinCaptureRadius();
    }

    public void SetPathResult(List<Vector2> pathPoints)
    {
        _pathPoints = pathPoints;
        _pathRequestState = PathRequestState.READY;
    }

    public State getState()
    {
        return _currentState;
    }

    public void Control(float delta)
    {
        _pathLine.GlobalRotation = 0;

        // If not reloading, then set to default
        if (_agent.RightWeaponAction != (int)NetworkSnapshotManager.PlayerInput.InputAction.RELOAD)
        {
            _agent.RightWeaponAction = (int)NetworkSnapshotManager.PlayerInput.InputAction.NOT_TRIGGER;
        }
        if (_agent.LeftWeaponAction != (int)NetworkSnapshotManager.PlayerInput.InputAction.RELOAD)
        {
            _agent.LeftWeaponAction = (int)NetworkSnapshotManager.PlayerInput.InputAction.NOT_TRIGGER;
        }


        switch (_currentState)
        {
            case State.PATROL:
                if (!_patrolReached)
                {
                    // If wait for Path
                    if (_pathRequestState == PathRequestState.NONE)
                    {
                        // Add a new request
                        _pathFinding.AddPathRequest(_agent, GlobalPosition, _patrolLocation);
                        // Change state to request
                        _pathRequestState = PathRequestState.REQUEST;
                        break;
                    }

                    // Only should continue to process if request state is ready
                    // all other state will just contine to wait
                    if (_pathRequestState != PathRequestState.READY)
                    {
                        break;
                    }

                    if (_pathPoints.Count > 1)
                    {
                        Vector2 nextPoint = (Vector2)_pathPoints[1];
                        _agent.RotateToward(nextPoint, delta);
                        _agent.MoveToward(_agent.GlobalPosition.DirectionTo(nextPoint), delta);
                        _setPathLine(_pathPoints);

                        if (_agent.HasReachedPosition(nextPoint))
                        {
                            _pathPoints.RemoveAt(1);
                        }
                    }
                    else
                    {
                        _patrolReached = true;
                        _patrolTimer.Start();
                        _pathLine.ClearPoints();

                        // Search for next path
                        _resetPath();

                    }
                }

                break;

            case State.ENGAGE:

                if (_targetAgent != null && IsInstanceValid(_targetAgent))
                {
                    // Default all state will change to weapon 0 for long range
                    if (_agent.GetCurrentWeaponIndex(Weapon.WeaponOrder.Left) != 0)
                    {
                        _agent.ChangeWeapon(0, Weapon.WeaponOrder.Left);
                    }

                    _agent.RotateToward(_targetAgent.GlobalPosition, delta);

                    // Calculate rotation
                    float angelToTargetAgent = _agent.GlobalPosition.DirectionTo(_targetAgent.GlobalPosition).Angle();

                    // Only start fire when agent is closely faced to its target agent (around Pi / 4)
                    if (Mathf.Abs(_agent.GlobalRotation - angelToTargetAgent) < 0.8f)
                    {
                        _agent.RightWeaponAction = (int)NetworkSnapshotManager.PlayerInput.InputAction.TRIGGER;
                        _agent.LeftWeaponAction = (int)NetworkSnapshotManager.PlayerInput.InputAction.TRIGGER;
                    }

                    // Chanse engaged agent closer if possible
                    if (_agent.GlobalPosition.DistanceTo(_targetAgent.GlobalPosition) > 250.0f)
                    {
                        if (_agent.GetCurrentWeaponIndex(Weapon.WeaponOrder.Left) != 0)
                        {
                            _agent.ChangeWeapon(0, Weapon.WeaponOrder.Left);
                        }

                        // If wait for Path
                        if (_pathRequestState == PathRequestState.NONE)
                        {
                            // Add a new request
                            _pathFinding.AddPathRequest(_agent, GlobalPosition, _targetAgent.GlobalPosition);
                            // Change state to request
                            _pathRequestState = PathRequestState.REQUEST;
                            break;
                        }

                        // Only should continue to process if request state is ready
                        // all other state will just contine to wait
                        if (_pathRequestState != PathRequestState.READY)
                        {
                            break;
                        }

                        if (_pathPoints.Count > 1)
                        {
                            Vector2 nextPoint = (Vector2)_pathPoints[1];
                            _agent.MoveToward(_agent.GlobalPosition.DirectionTo(nextPoint), delta);
                            _setPathLine(_pathPoints);

                            if (_agent.HasReachedPosition(nextPoint))
                            {
                                // Search for next path
                                _resetPath();
                            }
                        }
                        else
                        {
                            SetState(State.PATROL);
                            _pathLine.ClearPoints();
                        }

                    }
                    else
                    {
                        // Default all state will change to weapon 0
                        if (_agent.GetCurrentWeaponIndex(Weapon.WeaponOrder.Left) != 1)
                        {
                            _agent.ChangeWeapon(1, Weapon.WeaponOrder.Left);
                        }

                        // Random position in current position to avoid being hit
                        if (!_engagePositionSet)
                        {
                            _engagePositionOrign = _agent.GlobalPosition;
                            _engagePositionSet = true;
                        }

                        Vector2 randomPosition = new Vector2(_engagePositionOrign.x + _random.RandfRange(20.0f, -20.0f), _engagePositionOrign.y + _random.RandfRange(20.0f, -20.0f));
                        _agent.MoveToward(_agent.GlobalPosition.DirectionTo(randomPosition), delta);

                    }
                }

                break;

            case State.ADVANCE:

                if (_agent.GetCurrentWeaponIndex(Weapon.WeaponOrder.Left) != 0)
                {
                    _agent.ChangeWeapon(0, Weapon.WeaponOrder.Left);
                }

                // If wait for Path
                if (_pathRequestState == PathRequestState.NONE)
                {
                    // Add a new request
                    _pathFinding.AddPathRequest(_agent, GlobalPosition, _nextBasePosition);
                    // Change state to request
                    _pathRequestState = PathRequestState.REQUEST;
                    break;
                }

                // Only should continue to process if request state is ready
                // all other state will just contine to wait
                if (_pathRequestState != PathRequestState.READY)
                {
                    break;
                }

                if (_pathPoints.Count > 1)
                {
                    Vector2 nextPoint = (Vector2)_pathPoints[1];
                    _agent.RotateToward(nextPoint, delta);
                    _agent.MoveToward(_agent.GlobalPosition.DirectionTo(nextPoint), delta);
                    _setPathLine(_pathPoints);

                    if (_agent.HasReachedPosition(nextPoint))
                    {
                        // Search for next path
                        _resetPath();
                    }
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
            _agent.LeftWeaponAction = (int)NetworkSnapshotManager.PlayerInput.InputAction.RELOAD;
        }
        else
        {
            _agent.RightWeaponAction = (int)NetworkSnapshotManager.PlayerInput.InputAction.RELOAD;
        }
    }

    private void _onWeaponReload(Weapon.WeaponOrder weaponOrder, bool isReloading)
    {
        if (weaponOrder == Weapon.WeaponOrder.Left)
        {
            _agent.LeftWeaponAction = (int)NetworkSnapshotManager.PlayerInput.InputAction.NOT_TRIGGER;
        }
        else
        {
            _agent.RightWeaponAction = (int)NetworkSnapshotManager.PlayerInput.InputAction.NOT_TRIGGER;
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

        // Signify for new path
        _pathRequestState = PathRequestState.NONE;
    }

    public void OnTargetAgentChange(Agent agent)
    {
        _targetAgent = agent;

        if (_targetAgent != null && IsInstanceValid(_targetAgent))
        {
            SetState(State.ENGAGE);
        }
        else
        {
            SetState(State.ADVANCE);
        }

        _pathFinding.ClearRequest(_agent.GetUnitID());
        _pathRequestState = PathRequestState.NONE;

        _pathPoints = null;
    }
}
