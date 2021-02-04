using Godot;
using System;

public class LaserGun : Weapon
{

    private LaserRay _laserRay;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        base._Ready();
    }

    public override void Initialize(GameWorld gameWorld, Agent agent )
    {
        base.Initialize(gameWorld, agent);

        _laserRay = ((LaserRay)GetNode("LaserRay"));
        _laserRay.Initialize(gameWorld, _agent, _team);
    }

    public override void onWeaponTimerTimeout()
    {
        base.onWeaponTimerTimeout();
        if (_laserRay.getIsCasting())
        {
            _laserRay.setIsCasting(false);
        }
    }

    public override bool Fire(Agent targetAgent)
    {
        if (Cooldown && Ammo != 0)
        {
            Cooldown = false;
            Ammo -= 1;
            EmitSignal(nameof(AmmoChangeSignal), Ammo, MaxAmmo);
            _laserRay.setIsCasting(true);

            CooldownTimer.Start();

            return true;
        }

        if(Ammo == 0)
        {
            StartReload();
        }

        return false;
    }
}
