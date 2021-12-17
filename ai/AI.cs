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

	public override void _Ready()
	{

		_patrolTimer = (Timer)GetNode("PatrolTimer");
		_pathLine = (Line2D)GetNode("PathLine");

		_rng = new RandomNumberGenerator();
		_rng.Randomize();

		_currentState = State.INVALID;

	}

	public void Initialize(GameWorld gameWorld, Agent agent, PathFinding pathFinding, DetectionZone detectionZone)
	{
		_gameWorld = gameWorld;
		_agent = agent;
		_patrolOrigin = _agent.GlobalPosition;
		_pathFinding = pathFinding;
		_detectionZone = detectionZone;

		SetState(State.PATROL);
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
		if (_agent.RightWeaponAction != (int)NetworkSnapshotManager.PlayerInput.InputAction.RELOAD)
		{
			_agent.RightWeaponAction = (int)NetworkSnapshotManager.PlayerInput.InputAction.NOT_TRIGGER;
		}
		if (_agent.LeftWeaponAction != (int)NetworkSnapshotManager.PlayerInput.InputAction.RELOAD)
		{
			_agent.LeftWeaponAction = (int)NetworkSnapshotManager.PlayerInput.InputAction.NOT_TRIGGER;
		}

		List<Vector2> pathPoints = null;

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

					// Only start fire when agent is closely faced to its target agent (around Pi / 4)
					if (Mathf.Abs(_agent.GlobalRotation - angelToTargetAgent) < 0.8f)
					{
						_agent.RightWeaponAction = (int)NetworkSnapshotManager.PlayerInput.InputAction.TRIGGER;
						_agent.LeftWeaponAction = (int)NetworkSnapshotManager.PlayerInput.InputAction.TRIGGER;
					}

					// Chanse engaged agent closer if possible
					// if (_agent.GlobalPosition.DistanceTo(_targetAgent.GlobalPosition) > 600.0f)
					// {
					//    _agent.MoveToward(_agent.GlobalPosition.DirectionTo(_targetAgent.GlobalPosition), delta);
					// }
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
	}
}
