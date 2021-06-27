using Godot;
using System;

public class RifileBullet : Projectile
{

    private Trail trail;
    public override void _Ready()
    {
        trail = (Trail)GetNode("Trail");
        base._Ready();
    }

    public override void _PhysicsProcess(float delta)
    {
        base._PhysicsProcess(delta);
    }

    public override void Explode()
    {
        trail.Stop();
        base.Explode();
    }
}
