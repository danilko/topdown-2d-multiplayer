using Godot;
using System;

public class MultiTunnelMissleLauncher : Weapon
{

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        base._Ready();
    }

    public override void Initialize(GameWorld gameWorld, Agent agent, WeaponOrder weaponOrder, int weaponIndex)
    {
        base.Initialize(gameWorld, agent, weaponOrder, weaponIndex);

        // Re-align weapon fire
        if (GetWeaponOrder() == WeaponOrder.Left)
        {
            foreach (Position2D triggerPoint in GetNode("TriggerPoint").GetChildren())
            {
                triggerPoint.Position = new Vector2(triggerPoint.Position.x, triggerPoint.Position.y * -1);
            }
        }
    }

    public override bool Fire(Agent targetAgent)
    {
        if (Cooldown && Ammo != 0)
        {
            Cooldown = false;
            Ammo -= GetNode("TriggerPoint").GetChildCount();
            EmitSignal(nameof(AmmoChangeSignal), Ammo, MaxAmmo, GetWeaponOrder());

            CooldownTimer.Start();

            // Rotated 45 degree

            Vector2 dir = (new Vector2(1, 0)).Rotated(GlobalRotation + (Mathf.Pi / 4.0f));
        // Re-align weapon fire
        if (GetWeaponOrder() == WeaponOrder.Left)
        {
            dir = (new Vector2(1, 0)).Rotated(GlobalRotation - (Mathf.Pi / 4.0f));
        }

            // If no target set, utilze the default
            Vector2 defaultTarget = ((new Vector2(1, 0)).Rotated(GlobalRotation) * 2000f) + GlobalPosition;
            foreach (Position2D triggerPoint in GetNode("TriggerPoint").GetChildren())
            {
                EmitSignal(nameof(FireSignal), Bullet, triggerPoint.GlobalPosition, dir, Agent, _team, targetAgent, defaultTarget);

                FireEffect();
            }


            return true;

        }

        if (Ammo == 0)
        {
            EmitSignal(nameof(AmmoOutSignal), GetWeaponOrder());

            // Auto reload
            StartReload();
        }

        return false;
    }
}
