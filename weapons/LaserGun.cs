using Godot;
using System;

public class LaserGun : Weapon
{

    [Export]

    private LaserRay laserRay;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        base._Ready();
    }

    public override void Initialize(GameWorld gameWorld, Agent agent )
    {
        base.Initialize(gameWorld, agent);

        laserRay = ((LaserRay)GetNode("LaserRay"));
        laserRay.Initialize(gameWorld, _agent, _team);
    }

    public override void onWeaponTimerTimeout()
    {
        base.onWeaponTimerTimeout();
        if (laserRay.getIsCasting())
        {
            laserRay.setIsCasting(false);
        }
    }

    public override bool Fire(Agent targetAgent)
    {
        if (Cooldown && Ammo != 0)
        {
            Cooldown = false;
            Ammo -= 1;
            EmitSignal(nameof(AmmoChangedSignal), Ammo * 100 / MaxAmmo);
            laserRay.setIsCasting(true);

            CooldownTimer.Start();

            return true;
        }

        return false;
    }
}
