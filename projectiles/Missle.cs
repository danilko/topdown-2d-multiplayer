using Godot;
using System;

public class Missle : Projectile
{
    private int _rayCount = 200;
    public override void _Ready()
    {
        base._Ready();
    }

 
    protected override void ComputeDamage()
    {
        float currentRotation = 0.0f;
        for(int index = 0; index < _rayCount; index++)
        {
            currentRotation = index * 360.0f/_rayCount;
            currentRotation = Mathf.Deg2Rad(currentRotation);

            ExplosionRay explosionRay = (ExplosionRay)((PackedScene)GD.Load("res://projectiles/ExplosionRay.tscn")).Instance();
            
        AddChild(explosionRay);
        explosionRay.Rotation = currentRotation;
        explosionRay.Initialize(Damage, Source, SourceTeam, GameWorld);

        }
    }

}
