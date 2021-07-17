using Godot;
using System;

public class Missle : Projectile
{
    [Export]
    protected int DamageRayCount = 100;

    [Export]
    protected float DamageRayRadius = 100;

    public override void _Ready()
    {
        base._Ready();
    }


    protected override void ComputeDamage()
    {
        float currentRotation = 0.0f;
        for (int index = 0; index < DamageRayCount; index++)
        {
            currentRotation = index * 360.0f / DamageRayCount;
            currentRotation = Mathf.Deg2Rad(currentRotation);

            ExplosionRay explosionRay = (ExplosionRay)((PackedScene)GD.Load("res://projectiles/ExplosionRay.tscn")).Instance();
            explosionRay.CastTo = new Vector2(0.0f, DamageRayRadius);

            AddChild(explosionRay);
            explosionRay.Rotation = currentRotation;
            explosionRay.Initialize(Damage, Source, SourceTeam, GameWorld);

        }
    }

}
