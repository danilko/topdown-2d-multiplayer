using Godot;
using System;

public class Weapon : Node2D
{
    public enum WeaponAmmoType { machine_energy, external_energy, bullet }

    [Export]
    public WeaponAmmoType weaponAmmoType  {get; set;}

    public enum WeaponType {rifile, lasergun, misslelauncher}

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

    private GameWorld _gameWorld;

    protected Agent _agent;
    protected Team _team;

    public override void _Ready()
    {
        if (GunCooldown > 0)
        {
            Timer timer = (Timer)GetNode("WeaponTimer");
            timer.WaitTime = GunCooldown;
        }

        Connect(nameof(FireSignal), _gameWorld, "_onProjectileShoot");

        EmitSignal(nameof(AmmoChangedSignal), Ammo * 100 / MaxAmmo);
    }

    public virtual void Initialize(GameWorld gameWorld, Agent agent)
    {
        _agent = agent;
        _team = new Team();
        _team.CurrentTeamCode = agent.GetTeam();
        _gameWorld = gameWorld;
    }

    public virtual bool Fire(Agent targetAgent)
    {
        return false;
    }

    public void reload() { }


    public void AmmoIncrease(int amount)
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
