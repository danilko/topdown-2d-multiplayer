using Godot;
using System;

public class Weapon : Node2D
{
    public enum WeaponAmmoType { ENERGY, AMMO }

    [Export]
    public WeaponAmmoType CurrentWeaponAmmoType { get; set; }

    public enum WeaponType { RIFILE, LASER, MISSLELAUNCHER, SHIELD, LIGHTSABER, EMPTY }

    public enum WeaponOrder { Right, Left }

    [Export]
    public WeaponType CurrentWeaponType { get; set; }

    [Signal]
    public delegate void AmmoChangeSignal();

    [Signal]
    public delegate void AmmoOutSignal();

    [Signal]
    public delegate void ReloadSignal();

    [Signal]
    public delegate void FireSignal();

    [Export]
    public int MaxAmmo = -1;

    protected int Ammo = -1;

    [Export]
    protected int Shot = 1;

    [Export]
    protected float Spread = 1;

    protected Boolean Cooldown = true;

    [Export]
    protected float CooldownTime = -1;

    [Export]
    protected float ReloadTime = -1;

    [Export]
    protected PackedScene Bullet;

    [Export]
    public float KnockbackForce { get; set; }

    [Export]
    public String ItemResourceID {get;set;}

    private GameWorld _gameWorld;

    protected Agent _agent;
    protected Team _team;

    protected Timer CooldownTimer;
    protected Timer ReloadTimer;

    private WeaponOrder _weaponOrder = WeaponOrder.Right;

    public override void _Ready()
    {
        if (MaxAmmo == 0)
        {
            MaxAmmo = -1;
        }

        Ammo = MaxAmmo;

        CooldownTimer = (Timer)GetNode("CooldownTimer");
        CooldownTimer.WaitTime = CooldownTime;

        ReloadTimer = (Timer)GetNode("ReloadTimer");
        ReloadTimer.WaitTime = ReloadTime;
    }

    public virtual void Initialize(GameWorld gameWorld, Agent agent, WeaponOrder weaponOrder)
    {
        _agent = agent;
        _team = new Team();
        _team.CurrentTeamCode = agent.GetCurrentTeam();
        _gameWorld = gameWorld;
        _weaponOrder = weaponOrder;

        Connect(nameof(FireSignal), _gameWorld, "_onProjectileShoot");

        EmitSignal(nameof(AmmoChangeSignal), Ammo, MaxAmmo, GetWeaponOrder());
    }

    public WeaponOrder GetWeaponOrder()
    {
        return _weaponOrder;
    }

    public bool isReloading()
    {
        // If timer is not stop, then it is reloading
        return ! ReloadTimer.IsStopped();
    }

    public virtual bool Fire(Agent targetAgent)
    {
        if (Cooldown && Ammo != 0)
        {
            Cooldown = false;
            Ammo -= 1;
            EmitSignal(nameof(AmmoChangeSignal), Ammo, MaxAmmo, _weaponOrder);

            CooldownTimer.Start();

            Vector2 dir = (new Vector2(1, 0)).Rotated(GlobalRotation);

            Position2D triggerPoint = (Position2D)GetNode("TriggerPoint");

            if (Shot > 1)
            {
                for (int i = 0; i < Shot; i++)
                {
                    float a = -Spread + i * (2 * Spread) / (Shot - 1);
                    EmitSignal(nameof(FireSignal), Bullet, triggerPoint.GlobalPosition, dir.Rotated(a), _agent, _team, targetAgent);
                }
            }
            else
            {
                EmitSignal(nameof(FireSignal), Bullet, triggerPoint.GlobalPosition, dir, _agent, _team, targetAgent);
            }

            FireEffect();

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

    public int getMaxAmmo()
    {
        return MaxAmmo;
    }

    public int getAmmo()
    {
        return Ammo;
    }

    protected virtual void FireEffect() { }

    public void StartReload()
    {
        // Only allow reload if there is no reload in process
        if (! isReloading())
        {
            Ammo = 0;
            ReloadTimer.Start();
            EmitSignal(nameof(ReloadSignal), _weaponOrder, true);
        }
    }

    private void _stopReload()
    {
        AmmoIncrease(MaxAmmo);
        EmitSignal(nameof(ReloadSignal), _weaponOrder, false);
    }

    public void AmmoIncrease(int amount)
    {
        // -1 Indicate no ammo
        if (Ammo == -1)
        {
            return;
        }

        Ammo = +amount;

        if (Ammo > MaxAmmo)
        {
            Ammo = MaxAmmo;
        }

        EmitSignal(nameof(AmmoChangeSignal), Ammo, MaxAmmo, GetWeaponOrder());
    }

    public virtual void onWeaponTimerTimeout()
    {
        Cooldown = true;
    }

}
