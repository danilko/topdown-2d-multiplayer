using Godot;
using System;

public class Enemy : Tank
{

    [Export]
    protected int TurretSpeed;

    [Export]
    protected float DetectRadius;

    protected Node2D target;

    private int speed;

    public bool isPrimaryWeapon;
    public bool isSecondaryWeapon;

    Godot.Collections.Array targetPaths = null;

    GameWorld gameworld;

    private int currentSpawnPointIndex = 0;

    private float PATHRADIUS = 5.0f;

    private RaycastAStar aStarSolver = null;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        CollisionShape2D detectRadius = (CollisionShape2D)(GetNode("DetectRadius").GetNode("CollisionShape2D"));

        CircleShape2D shape = new CircleShape2D();
        shape.Radius = DetectRadius;
        detectRadius.Shape = shape;

        base._Ready();

        isPrimaryWeapon = false;
        isSecondaryWeapon = false;

        gameworld = (GameWorld)GetParent();

        if (GetTree().IsNetworkServer())
        {
            aStarSolver = new RaycastAStar();
        }
    }

    public void setCurrentSpawnPoint(int currentSpawnPointIndex)
    {
        this.currentSpawnPointIndex = currentSpawnPointIndex;
    }

    public override void _Control(float delta)
    {
        if (targetPaths == null && target == null)
        {
            currentSpawnPointIndex = gameworld.getNextSpawnIndex(currentSpawnPointIndex);
            // targetPaths = gameworld.getPaths(GlobalPosition, gameworld.getSpawnPointPosition(currentSpawnPointIndex));

            Godot.Collections.Array excludes = new Godot.Collections.Array() { this };

            targetPaths = aStarSolver.path(GlobalPosition, gameworld.getSpawnPointPosition(currentSpawnPointIndex + 1), GetWorld2d(), excludes, this);
            if (targetPaths != null && targetPaths.Count < 2)
            {
                targetPaths = null;
            }


            if (targetPaths != null)
            {
                targetPaths.RemoveAt(0);
            }

        }

        if (targetPaths != null)
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
            }
            else
            {
                MoveAndSlide(targetDir * speed);
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

        RayCast2D lookAhead1 = (RayCast2D)GetNode("LookAhead1");
        RayCast2D lookAhead2 = (RayCast2D)GetNode("LookAhead2");
        if (lookAhead1.IsColliding() || lookAhead2.IsColliding())
        {
            //speed = (int)Mathf.Lerp(speed, 0.0f, 0.1f);
        }
        else
        {
            speed = (int)Mathf.Lerp(speed, MaxSpeed, 0.05f);
        }
    }

    public void debugDrawing(Vector2 point)
    {
        Node2D path = (Node2D)gameworld.GetNode("path_" + Name);

        if (path == null)
        {
            path = (Node2D)gameworld.GetNode("pathchart").Duplicate();
            path.Name = "path_" + Name;
            gameworld.AddChild(path);
        }

        foreach (Node2D node in path.GetChildren())
        {
            node.QueueFree();
        }

        Node2D pointNode = (Node2D)gameworld.GetNode("dot").Duplicate();
        pointNode.Name = "pathdot_" + Name + "_" + point.x + "_" + point.y;
        pointNode.Position = point;
        path.AddChild(pointNode);
    }

    public void cleanDrawing()
    {
        Node2D path = (Node2D)gameworld.GetNode("path_" + Name);

        if (path != null)
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
                targetPaths = null;
            }
        }
    }


    private void _on_DetectRadius_body_exited(Node2D body)
    {
        if (body == target)
        {
            target = null;
        }
    }
}