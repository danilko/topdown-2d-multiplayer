using Godot;
using System;

public class Shield : Weapon
{
    [Export]
    int Damage = 50;

    //private CollisionShape2D _collisionShape2D;

    public override void _Ready()
    {
        //_collisionShape2D = (CollisionShape2D)(GetNode("Area2D").GetNode("CollisionShape2D"));

        if (MaxAmmo == 0)
        {
            MaxAmmo = -1;
        }

        Ammo = MaxAmmo;

        CooldownTimer = (Timer)GetNode("CooldownTimer");
        CooldownTimer.WaitTime = 1;

        ReloadTimer = (Timer)GetNode("ReloadTimer");
        ReloadTimer.WaitTime = 1;

        EmitSignal(nameof(AmmoChangeSignal), Ammo, MaxAmmo);
    }

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
