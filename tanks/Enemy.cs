using Godot;
using System;

public class Enemy : Tank
{

    [Export]
    protected int TurretSpeed;

    [Export]
    protected float DetectRadius;

    private int speed;

    public bool isPrimaryWeapon;
    public bool isSecondaryWeapon;

    private Godot.Collections.Array targetPaths = null;

    private int currentSpawnPointIndex = 0;

    private float PATHRADIUS = 2.0f;

    Godot.Collections.Array detourPaths = null;
    Godot.Collections.Array members = null;
    // 10 px / s
    float maxForces = 500;

    float cohesionDistance = 64;
    float alignmentDistance = 20;
    float separationDistance = 10.0f;

    int slowingDistance = 10;

    private Vector2 acceleration;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        base._Ready();

        CollisionShape2D detectRadius = (CollisionShape2D)(GetNode("DetectRadius").GetNode("CollisionShape2D"));

        CircleShape2D shape = new CircleShape2D();
        shape.Radius = DetectRadius;
        detectRadius.Shape = shape;

        isPrimaryWeapon = false;
        isSecondaryWeapon = false;

        speed = MaxSpeed;

        members = new Godot.Collections.Array();
    }

    public void setCurrentSpawnIndex(int currentSpawnPointIndex)
    {
        this.currentSpawnPointIndex = currentSpawnPointIndex;
    }

    public int getCurrentSpawnIndex()
    {
        return currentSpawnPointIndex;
    }

    private void calculatePath()
    {
        currentSpawnPointIndex = gameworld.getNextSpawnIndex(currentSpawnPointIndex);
        // targetPaths = gameworld.getPaths(GlobalPosition, gameworld.getSpawnPointPosition(currentSpawnPointIndex));

        Godot.Collections.Array excludes = new Godot.Collections.Array() { this };

        targetPaths = gameworld.getPaths(GlobalPosition, gameworld.getSpawnPointPosition(currentSpawnPointIndex + 1), GetWorld2d(), excludes);

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

    public Vector2 seek(Vector2 targetPosition)
    {
        // Return the force that needs to be added to the current velocity to seek the target position
        // Use the max force to attribute to clamp the force magnitude

        return ((targetPosition - Position).Normalized() * MaxSpeed - Velocity).Clamped(maxForces);
    }

    public Vector2 seekAndArrive(Vector2 targetPosition)
    {
        // Return the force that needs to be added to the current velocity to seek and slowly approch the target position
        Vector2 desiredVelocity = (targetPosition - Position);
        float targetDistance = desiredVelocity.Length();

        return (approachTarget(slowingDistance, targetDistance, desiredVelocity.Normalized() * MaxSpeed) - Velocity).Clamped(maxForces);
    }

    public Vector2 approachTarget(int slowingDistance, float distanceToTarget, Vector2 desiredVelocity)
    {
        if (distanceToTarget < slowingDistance)
        {
            desiredVelocity *= distanceToTarget / slowingDistance;
        }
        return desiredVelocity;
    }

    public Vector2 cohesion()
    {
        int membersCount = 0;
        Vector2 sumOfPosition = new Vector2();

        foreach (Tank member in members)
        {
            // It is possible that member got destroy before this API request
            if (!IsInstanceValid(member))
            {
                continue;
            }

            float distance = Position.DistanceTo(member.Position);

            if (distance > 0 && distance <= cohesionDistance)
            {
                sumOfPosition += member.Position;
                membersCount++;
            }
        }
        if (membersCount != 0)
        {
            return seek(sumOfPosition / (float)membersCount);
        }

        return sumOfPosition;
    }

    public Vector2 alignment()
    {
        int membersCount = 0;
        Vector2 sumOfVelocity = new Vector2();

        foreach (Tank member in members)
        {
            // It is possible that member got destroy before this API request
            if (!IsInstanceValid(member))
            {
                continue;
            }

            float distance = Position.DistanceTo(member.Position);

            if (distance > 0 && distance <= alignmentDistance)
            {
                sumOfVelocity += member.Velocity;
                membersCount++;
            }
        }
        if (membersCount != 0)
        {
            return ((sumOfVelocity / (float)membersCount).Normalized() * MaxSpeed - Velocity).Clamped(maxForces);
        }

        return sumOfVelocity;
    }

    public Vector2 separation()
    {
        int membersCount = 0;
        Vector2 separationForce = new Vector2();

        foreach (Tank member in members)
        {
            // It is possible that member got destroy before this API request
            if (!IsInstanceValid(member))
            {
                continue;
            }

            float distance = Position.DistanceTo(member.Position);

            if (distance > 0 && distance <= separationDistance)
            {
                separationForce += (Position - member.Position).Normalized() / distance;
                membersCount++;
            }
        }
        if (membersCount != 0)
        {
            return ((separationForce / (float)membersCount).Normalized() * MaxSpeed - Velocity).Clamped(maxForces);
        }

        return separationForce;
    }

    public void flock()
    {
        ///Velocity += cohesion() * 1.0f;
        //Velocity += alignment() * 0.8f;
        //Velocity += separation() * 1.0f;
    }

    public void applyForce(Vector2 force)
    {
        acceleration = acceleration + force;
    }

    public override void _Control(float delta)
    {
        Godot.Collections.Array removeIndices = new Godot.Collections.Array();

        int index = 0;
        // Check vaild members
        foreach (Tank member in members)
        {
            if (!IsInstanceValid(member))
            {
                removeIndices.Add(index);
            }
            index++;
        }

        foreach (int removeIndex in removeIndices)
        {
            members.Remove(removeIndex);
        }

        // Set the accel
        acceleration = new Vector2();

        // Validate if target is available or is freed up (maybe no longer in scene)
        if (target != null && !IsInstanceValid(target))
        {
            target = null;
        }

        if (targetPaths != null && targetPaths.Count == 0)
        {
            targetPaths = null;
        }


        // Execute next path when target is not empty
        if (targetPaths != null && target == null)
        {
            Vector2 targetPoint = (Vector2)targetPaths[0];

            Vector2 targetDir = (targetPoint - GlobalPosition).Normalized();
            Vector2 currentDir = (new Vector2(1, 0)).Rotated(GlobalRotation);

            GlobalRotation = currentDir.LinearInterpolate(targetDir, TurretSpeed * delta).Angle();


            if (GlobalPosition.DistanceTo(targetPoint) < PATHRADIUS)
            {
                targetPaths.RemoveAt(0);

                if (targetPaths.Count == 0)
                {
                    targetPaths = null;
                }

                slowDownBoostTrail();
            }
            else
            {
                Velocity = targetDir * MaxSpeed;

                Vector2 seekVelocity = seekAndArrive(targetPoint);
                //Vector2 separationVelocity = separation() * 0.5f;
                //Velocity = Velocity + seekVelocity + separationVelocity;
                
                MoveAndSlide(Velocity);

                speedUpBoostTrail();
            }
        }

        if (target != null)
        {
            Vector2 targetDir = (target.GlobalPosition - GlobalPosition).Normalized();
            Vector2 currentDir = (new Vector2(1, 0)).Rotated(GlobalRotation);

            GlobalRotation = currentDir.LinearInterpolate(targetDir, TurretSpeed * delta).Angle();
            if (targetDir.Dot(currentDir) > 0.9)
            {
                isPrimaryWeapon = true;
            }
            else
            {
                isPrimaryWeapon = false;
                isSecondaryWeapon = false;
            }
        }
        else
        {
            isPrimaryWeapon = false;
            isSecondaryWeapon = false;
        }

        if (target == null && targetPaths == null)
        {
            calculatePath();
        }

    }

    public void debugDrawing(Vector2 point)
    {
        Node2D path = (Node2D)gameworld.GetNode("path_" + Name);

        if (path != null && IsInstanceValid(path))
        {
            path = (Node2D)gameworld.GetNode("pathchart").Duplicate();
            path.Name = "path_" + Name;
            gameworld.AddChild(path);
        }

        Node2D pointNode = (Node2D)gameworld.GetNode("dot").Duplicate();
        pointNode.Name = "pathdot_" + Name + "_" + point.x + "_" + point.y;
        pointNode.Position = point;
        path.AddChild(pointNode);
    }

    public void cleanDrawing()
    {
        Node2D path = (Node2D)gameworld.GetNode("path_" + Name);

        if (path != null && IsInstanceValid(path))
        {
            foreach (Node2D node in path.GetChildren())
            {
                node.QueueFree();
            }
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
    }

    private void _on_DetectRadius_body_entered(Node2D body)
    {
        if (body.HasMethod("getTeamIdentifier"))
        {
            // If not same team identifier, identify as target
            if (((Tank)body).getTeamIdentifier() != getTeamIdentifier())
            {
                target = body;
            }
            else
            {
                // Join as member
                members.Add((Tank)body);
            }
        }
    }

    private void _on_DetectRadius_body_exited(Node2D body)
    {
        if (body == target)
        {
            target = null;
        }
        else
        {
            // Remove from members
            int removeIndex = 0;
            foreach (Tank member in members)
            {
                if (IsInstanceValid(member) && member == body)
                {
                    break;
                }

                removeIndex++;
            }

            if (removeIndex != -1 && removeIndex < members.Count)
            {
                members.Remove(removeIndex);
            }
        }
     }
}