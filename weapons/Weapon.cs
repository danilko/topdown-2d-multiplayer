using Godot;
using System;

public class Weapon : Node2D
{
	public enum WeaponAmmoType { ENERGY, AMMO }

	[Export]
	public WeaponAmmoType CurrentWeaponAmmoType { get; set; }

	public enum WeaponOrder { Right, Left }

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
	public String ItemResourceID { get; set; }

	private GameWorld _gameWorld;

	protected Agent Agent;
	protected Team _team;

	protected Timer CooldownTimer;
	protected Timer ReloadTimer;

	private WeaponOrder _weaponOrder = WeaponOrder.Right;

	private int _weaponIndex = -1;

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

	public float GetReloadTime()
	{
		return ReloadTime;
	}

	public void SetReloadTime(float reloadTime)
	{
		ReloadTime = reloadTime;
		ReloadTimer.WaitTime = ReloadTime;
		if(!ReloadTimer.IsStopped())
		{
			// Reload the timer
			ReloadTimer.Stop();
			ReloadTimer.Start();
		}
	}

	public virtual void Initialize(GameWorld gameWorld, Agent agent, WeaponOrder weaponOrder, int weaponIndex)
	{
		Agent = agent;
		_team = new Team();
		_team.CurrentTeamCode = agent.GetTeam();
		_gameWorld = gameWorld;
		_weaponOrder = weaponOrder;
		_weaponIndex = weaponIndex;

		Connect(nameof(FireSignal), _gameWorld.GetProjectileManager(), "_onProjectileShoot");

		EmitSignal(nameof(AmmoChangeSignal), Ammo, MaxAmmo, GetWeaponOrder());
	}

	protected GameWorld GetGameWorld()
	{
		return _gameWorld;
	}

	public virtual void Deinitialize()
	{
		QueueFree();
	}

	public WeaponOrder GetWeaponOrder()
	{
		return _weaponOrder;
	}

	public bool isReloading()
	{
		// If timer is not stop, then it is reloading
		return !ReloadTimer.IsStopped();
	}

	public virtual void EquipWeapon(bool equip)
	{}

	public virtual bool Fire(Agent targetAgent, Vector2 targetGlobalPosition)
	{
		if (Cooldown && Ammo != 0)
		{
			Cooldown = false;
			Ammo -= 1;
			EmitSignal(nameof(AmmoChangeSignal), Ammo, MaxAmmo, GetWeaponOrder());

			CooldownTimer.Start();

	

			Position2D triggerPoint = (Position2D)GetNode("TriggerPoint");

			Vector2 dir = (targetGlobalPosition - triggerPoint.GlobalPosition).Normalized();

			if (Shot > 1)
			{
				for (int i = 0; i < Shot; i++)
				{
					float a = -Spread + i * (2 * Spread) / (Shot - 1);
					EmitSignal(nameof(FireSignal), Bullet, triggerPoint.GlobalPosition, dir.Rotated(a), Agent, _team, targetAgent, targetGlobalPosition);
				}
			}
			else
			{
				EmitSignal(nameof(FireSignal), Bullet, triggerPoint.GlobalPosition, dir, Agent, _team, targetAgent, targetGlobalPosition);
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

	public int GetMaxAmmo()
	{
		return MaxAmmo;
	}

	public int GetAmmo()
	{
		return Ammo;
	}

	public virtual void SetAmmo(int ammo)
	{
		Ammo = ammo;
		EmitSignal(nameof(AmmoChangeSignal), Ammo, MaxAmmo, GetWeaponOrder());
	}

	public virtual void Unequip()
	{
		// Remove item and unequip it
		_gameWorld.getInventoryManager().UnequipItem(Agent.GetInventory(), _weaponOrder, _weaponIndex, 1);
	}

	protected virtual void FireEffect() { }

	public virtual void StartReload()
	{
		// Only allow reload if there is no reload in process
		if (!isReloading())
		{
			Ammo = 0;
			ReloadTimer.Start();
			EmitSignal(nameof(ReloadSignal), _weaponOrder, true);
		}
	}

	private void _stopReload()
	{
		SetAmmo(MaxAmmo);
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
