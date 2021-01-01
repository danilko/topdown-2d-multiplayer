using Godot;
using System;

public class AIAgent : Agent
{

    [Export]
    public int TurretSpeed { get; set; }

    [Export]
    public float DetectRadius { get; set; }

    private int _speed;

    private Godot.Collections.Array targetPaths = null;

    private int _currentSpawnPointIndex = 0;

    private float _PathRadius = 5.0f;

    private Godot.Collections.Array members = null;

    // 10 px / s
    private float maxForces = 500;

    private int _slowingDistance = 10;

    private Vector2 _acceleration;

    private Line2D _pathLine;
    private Vector2 _originalPathLineLocation;

    private AI _agentAI;


    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        base._Ready();

        PrimaryWeaponAction = (int)GameStates.PlayerInput.InputAction.NOT_TRIGGER;
        SecondaryWeaponAction = (int)GameStates.PlayerInput.InputAction.NOT_TRIGGER;

        _speed = MaxSpeed;

        members = new Godot.Collections.Array();

        _pathLine = (Line2D)GetNode("PathLine");
        _originalPathLineLocation = GlobalPosition;

        _agentAI = (AI)GetNode("AI");
        _agentAI.Initialize(this, DetectRadius);
    }

    public void setCurrentSpawnIndex(int currentSpawnPointIndex)
    {
        _currentSpawnPointIndex = currentSpawnPointIndex;
    }

    public int getCurrentSpawnIndex()
    {
        return _currentSpawnPointIndex;
    }

    protected  override void DisconnectWeapon(Weapon currentWeapon, Weapon.WeaponOrder weaponOrder)
    {
        if (currentWeapon != null && _agentAI != null && IsInstanceValid(_agentAI))
        {
            // Deregister weapon with ai
            if (currentWeapon.IsConnected(nameof(Weapon.AmmoOutSignal), _agentAI, "_on" + weaponOrder + "WeaponNeedReload"))
            {
                currentWeapon.Disconnect(nameof(Weapon.AmmoOutSignal), _agentAI, "_on" + weaponOrder + "WeaponNeedReload");
                currentWeapon.Disconnect(nameof(Weapon.ReloadStopSignal), _agentAI, "_onPrimaryWeaponReloadStop");
            }
        }
    }

    protected override void ConnectWeapon(Weapon currentWeapon, Weapon.WeaponOrder weaponOrder)
    {
        if (currentWeapon != null && _agentAI != null && IsInstanceValid(_agentAI))
        {
            // Register weapon with AI
            if (! currentWeapon.IsConnected(nameof(Weapon.AmmoOutSignal), _agentAI, "_on" + weaponOrder + "WeaponNeedReload"))
            {
                currentWeapon.Connect(nameof(Weapon.AmmoOutSignal), _agentAI, "_on" + weaponOrder + "WeaponNeedReload");
                currentWeapon.Connect(nameof(Weapon.ReloadStopSignal), _agentAI, "_onPrimaryWeaponReloadStop");
            }

            base.ConnectWeapon(currentWeapon, weaponOrder);
        }
    }


    public Vector2 seekAndArrive(Vector2 targetPosition)
    {
        // Return the force that needs to be added to the current velocity to seek and slowly approch the target position
        Vector2 desiredVelocity = (targetPosition - Position);
        float targetDistance = desiredVelocity.Length();

        return (approachTarget(_slowingDistance, targetDistance, desiredVelocity.Normalized() * MaxSpeed) - Velocity).Clamped(maxForces);
    }

    public Vector2 approachTarget(int slowingDistance, float distanceToTarget, Vector2 desiredVelocity)
    {
        if (distanceToTarget < slowingDistance)
        {
            desiredVelocity *= distanceToTarget / slowingDistance;
        }
        return desiredVelocity;
    }

    public override void _Control(float delta)
    {
        _agentAI.Control(delta);
    }

    public void setPathLine(Godot.Collections.Array points)
    {
        _pathLine.ClearPoints();

        _originalPathLineLocation = GlobalPosition;
        _pathLine.GlobalPosition = _originalPathLineLocation;

        if (points != null)
        {

            Vector2[] localPoints = new Vector2[points.Count];

            for (int index = 0; index < points.Count; index++)
            {
                localPoints[index] = (Vector2)points[index] - Position;
            }

            _pathLine.Points = localPoints;
        }

    }


    public override void _Process(float delta)
    {
        if (!Alive)
        {
            return;
        }

        if (GetTree().IsNetworkServer())
        {
            _Control(delta);
        }


        _pathLine.GlobalRotation = 0;
        _pathLine.GlobalPosition = _originalPathLineLocation;
    }

}