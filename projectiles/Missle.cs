using Godot;
using System;

public class Missle : Projectile
{

    [Export]
    protected float DamageRayRadius = 100;

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

        // Fire off to a position 500f from current position if no target set
        _noTargetApproxDestination = defaultTargetPosition;
        _initialPosition = position;

        targetSpeed = Speed;
        Speed = initialSpeed;

        isInitialProjectileStart = true;
    }

    public override void _PhysicsProcess(float delta)
    {
        if (isInitialProjectileStart)
        {
            if (_initialPosition.DistanceTo(GlobalPosition) >= 20.0f)
            {
                this.LookAt(_noTargetApproxDestination);

                Speed = targetSpeed;
                Velocity = Transform.x * Speed;

                isInitialProjectileStart = false;
            }
        }

        base._PhysicsProcess(delta);
    }
    public override void Explode()
    {
        base.Explode();

        CallDeferred("createExplosion");

        QueueFree();
    }

    private void createExplosion()
    {
        ExplosionBlast explosionBlast = (ExplosionBlast)((PackedScene)GD.Load("res://projectiles/ExplosionBlast.tscn")).Instance();
        GameWorld.AddChild(explosionBlast);
        explosionBlast.Initialize(Source, SourceTeam,  GameWorld, DamageRayRadius, Damage, GlobalPosition);
    }

    protected override void ComputeDamage(Node body)
    {
        // Instead of direct compute, will be handle by explosion blast in this case


    }

}
