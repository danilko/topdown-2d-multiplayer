using Godot;
using System;

public class Weapon : Node2D
{
    public enum WeaponAmmoType { machine_energy, external_energy, bullet }

    [Export]
    public WeaponAmmoType weaponAmmoType  {get; set;}

    public enum WeaponType {rifile, lasergun}

    [Export]
    public WeaponType weaponType {get; set;}

    [Signal]
    public delegate void AmmoChangedSignal();

    [Signal]
    public delegate void FireSignal();

    [Export]
    public int MaxAmmo = -1;

    [Export]
    protected int Ammo = -1;

    [Export]
    protected int GunShot = 1;

    [Export]
    protected float GunSpread = 1;

    [Export]
    protected Boolean CanShoot = true;

    [Export]
    protected float GunCooldown = -1;

    [Export]
    protected PackedScene Bullet;

    [Export]
    protected String WeaponName = "UNKNOWN";

    [Export]
    public float KnockbackForce { get; set; }

    public GameWorld gameWorld { get; set; }

    public override void _Ready()
    {
        if (GunCooldown > 0)
        {
            Timer timer = (Timer)GetNode("WeaponTimer");
            timer.WaitTime = GunCooldown;
        }

        Connect(nameof(FireSignal), gameWorld, "_onTankShoot");

        EmitSignal(nameof(AmmoChangedSignal), Ammo * 100 / MaxAmmo);
    }

    public virtual bool fire(Node2D source, Node2D target)
    {
        return false;
    }

    public void reload() { }


    public void ammoIncrease(int amount)
    {

        Ammo = +amount;

        if (Ammo > MaxAmmo)
        {
            Ammo = MaxAmmo;
        }

        EmitSignal(nameof(AmmoChangedSignal), Ammo * 100 / MaxAmmo);
    }

    public virtual void onWeaponTimerTimeout()
    {
        CanShoot = true;
        Timer timer = (Timer)GetNode("WeaponTimer");
    }

}
