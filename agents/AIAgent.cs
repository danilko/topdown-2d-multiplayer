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


    private void calculatePath()
    {
        _currentSpawnPointIndex = _gameWorld.getNextSpawnIndex(_currentSpawnPointIndex);
        // targetPaths = gameworld.getPaths(GlobalPosition, gameworld.getSpawnPointPosition(currentSpawnPointIndex));

        Godot.Collections.Array excludes = new Godot.Collections.Array() { this };

        targetPaths = _gameWorld.getPaths(GlobalPosition, _gameWorld.getSpawnPointPosition(_currentSpawnPointIndex + 1), GetWorld2d(), excludes);

        if (targetPaths != null && targetPaths.Count < 1)
        {
            targetPaths = null;
        }

        if (targetPaths != null)
        {
            targetPaths.RemoveAt(0);
        }

        //  cleanDrawing();
        //  foreach(Vector2 point in targetPaths )
        // {
        //       debugDrawing(point);
        // }
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

    // public override void _Control(float delta)
    // {
    //     Godot.Collections.Array removeIndices = new Godot.Collections.Array();

    //     int index = 0;
    //     // Check vaild members
    //     foreach (Tank member in members)
    //     {
    //         if (!IsInstanceValid(member))
    //         {
    //             removeIndices.Add(index);
    //         }
    //         index++;
    //     }

    //     foreach (int removeIndex in removeIndices)
    //     {
    //         members.Remove(removeIndex);
    //     }

    //     // Set the accel
    //     _acceleration = new Vector2();

    //     // Validate if target is available or is freed up (maybe no longer in scene)
    //     if (target != null && !IsInstanceValid(target))
    //     {
    //         target = null;
    //     }

    //     if (targetPaths != null && targetPaths.Count == 0)
    //     {
    //         targetPaths = null;
    //     }


    //     // Execute next path when target is not empty
    //     if (targetPaths != null && target == null)
    //     {
    //         Vector2 targetPoint = (Vector2)targetPaths[0];

    //         Vector2 targetDir = (targetPoint - GlobalPosition).Normalized();
    //         Vector2 currentDir = (new Vector2(1, 0)).Rotated(GlobalRotation);

    //         GlobalRotation = currentDir.LinearInterpolate(targetDir, TurretSpeed * delta).Angle();

    //         if (GlobalPosition.DistanceTo(targetPoint) < _PathRadius)
    //         {
    //             targetPaths.RemoveAt(0);

    //             if (targetPaths.Count == 0)
    //             {
    //                 targetPaths = null;
    //             }



    //             slowDownBoostTrail();
    //         }
    //         else
    //         {
    //             Velocity = targetDir * MaxSpeed;

    //             Vector2 seekVelocity = seekAndArrive(targetPoint);
    //             //Vector2 separationVelocity = separation() * 0.5f;
    //             //Velocity = Velocity + seekVelocity + separationVelocity;

    //             MoveAndSlide(Velocity);

    //             speedUpBoostTrail();
    //         }
    //     }

    //     if (target != null)
    //     {
    //         Vector2 targetDir = (target.GlobalPosition - GlobalPosition).Normalized();
    //         Vector2 currentDir = (new Vector2(1, 0)).Rotated(GlobalRotation);

    //         GlobalRotation = currentDir.LinearInterpolate(targetDir, TurretSpeed * delta).Angle();
    //         if (targetDir.Dot(currentDir) > 0.9)
    //         {
    //             PrimaryWeaponFiring = true;
    //         }
    //         else
    //         {
    //             PrimaryWeaponFiring = false;
    //             SecondaryWeaponFiring = false;
    //         }
    //     }
    //     else
    //     {
    //         PrimaryWeaponFiring = false;
    //         SecondaryWeaponFiring = false;
    //     }

    //     if (target == null && targetPaths == null)
    //     {
    //         calculatePath();

    //         setPathLine(targetPaths);
    //     }

    // }

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