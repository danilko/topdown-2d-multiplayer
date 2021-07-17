using Godot;
using System;

public class MultiTunnelMissle : Missle
{

    private bool _noTarget = false;
    private Vector2 _noTargetApproxDestination;
    private Vector2 _initialPosition;

    private bool isInitialProjectileStart = false;

    private int initialSpeed = 100;
    private int targetSpeed;

    public override void _Ready()
    {
        base._Ready();
    }

    public override void Initialize(Vector2 position, Vector2 direction, Node2D inSource, Team sourceTeam, Node2D inTarget, Vector2 defaultTargetPosition)
    {
        base.Initialize(position, direction, inSource, sourceTeam, inTarget, defaultTargetPosition);

        if (inTarget == null || IsInstanceValid(inTarget))
        {
            _noTarget = true;

        }

        // Fire off to a position 500f from current position if no target set
        _noTargetApproxDestination = defaultTargetPosition;
        _initialPosition = position;

        targetSpeed = Speed;
        Speed = initialSpeed;

        isInitialProjectileStart = true;

        // Default to disable physics until pass initial launch stage
        Enabled = false;
        ((CollisionShape2D)GetNode("ProjectileArea2D/CollisionShape2D")).Disabled = true;
    }

    public override void _PhysicsProcess(float delta)
    {
        if (isInitialProjectileStart)
        {
            if (_initialPosition.DistanceTo(GlobalPosition) >= 50.0f)
            {
                Speed = targetSpeed;

                // Validate if target is available or is freed up (maybe no longer in scene)
                if (Target != null && !IsInstanceValid(Target))
                {
                    Target = null;
                }

                if (Target != null)
                {
                    this.LookAt(Target.GlobalPosition);
                    Acceleration.Rotated(Acceleration.AngleTo(Target.GlobalPosition));
                }
                else
                {
                    this.LookAt(_noTargetApproxDestination);
                    Acceleration.Rotated(Acceleration.AngleTo(_noTargetApproxDestination));
                }

                isInitialProjectileStart = false;
                // Enable Physics
                Enabled = true;
                ((CollisionShape2D)GetNode("ProjectileArea2D/CollisionShape2D")).Disabled = false;
            }
        }

        base._PhysicsProcess(delta);
    }

}
