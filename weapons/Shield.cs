using Godot;
using System;

public class Shield : Weapon
{
    [Export]
    int Damage = 50;


    public override bool Fire(Agent targetAgent)
    {
        return true;
    }

    private void _onCollisionEntered(Node2D body)
    {

        // Projectile will collide
        if (body.HasMethod("_onProjectileAreaEntered"))
        {
            Projectile projectile = (Projectile)body;
            projectile.Explode();
        }
    }

}
