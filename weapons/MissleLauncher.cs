using Godot;
using System;

public class MissleLauncher : Weapon
{

    public override void _Ready()
    {
        base._Ready();
    }

    public override bool Fire(Agent targetAgent) {
        if (CanShoot && Ammo != 0)
        {
            CanShoot = false;
            Ammo -= 1;
            EmitSignal(nameof(AmmoChangedSignal), Ammo * 100 / MaxAmmo);

            Timer timer = (Timer)GetNode("WeaponTimer");
            timer.Start();

            Vector2 dir = (new Vector2(1, 0)).Rotated(GlobalRotation);

            Position2D triggerPoint = (Position2D)GetNode("TriggerPoint");
            if (GunShot > 1)
            {
                for (int i = 0; i < GunShot; i++)
                {
                    float a = -GunSpread + i * (2 * GunSpread) / (GunShot - 1);
                    EmitSignal(nameof(FireSignal), Bullet, triggerPoint.GlobalPosition, dir.Rotated(a), _agent, _team, targetAgent);
                }
            }
            else
            {
                EmitSignal(nameof(FireSignal), Bullet, triggerPoint.GlobalPosition, dir, _agent, _team,  targetAgent);
                
            }
            
            return true;
        }

        return false;
    }
}
