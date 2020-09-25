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
        laserRay = ((LaserRay)GetNode("LaserRay"));
    }

    public override void onWeaponTimerTimeout()
    {
        base.onWeaponTimerTimeout();
        if (laserRay.getIsCasting())
        {
            laserRay.setIsCasting(false);
        }
    }

    public override bool fire(Node2D source, Node2D target)
    {
        Timer timer = (Timer)GetNode("WeaponTimer");

        if (CanShoot && Ammo != 0)
        {
            CanShoot = false;
            Ammo -= 1;
            EmitSignal(nameof(AmmoChangedSignal), Ammo * 100 / MaxAmmo);
            laserRay.source = (Tank)source;
            laserRay.setIsCasting(true);

            timer.Start();

            return true;
        }

        return false;
    }
}
