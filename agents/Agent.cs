using Godot;
using System;

public class Agent : KinematicBody2D
{


    [Signal]
    public delegate void HealthChangedSignal();

    [Signal]
    public delegate void EnergyChangedSignal();

    [Signal]
    public delegate void DefeatedAgentChangedSignal();

    [Signal]
    public delegate void DeadSignal();

    [Signal]
    public delegate void PrimaryWeaponChangeSignal();

    [Signal]
    public delegate void SecondaryWeaponChangeSignal();

    [Export]
    public int MaxSpeed { get; set; }

    [Export]
    protected float RotationSpeed;

    [Export]
    protected int MaxHealth;

    [Export]
    protected int MaxEnergy;

    public Vector2 Velocity;
    protected Boolean Alive = true;

    public float currentTime = 0;

    public int currentPrimaryWeaponIndex { get; set; }
    public int currentSecondaryWeaponIndex { get; set; }
    public int PrimaryWeaponAction { set; get; }
    public int SecondaryWeaponAction { set; get; }
    public bool PrimaryWeaponReloading { set; get; }
    public bool SecondaryWeaponReloading { set; get; }

    protected Godot.Collections.Array primaryWeapons = new Godot.Collections.Array();
    protected Godot.Collections.Array secondaryWeapons = new Godot.Collections.Array();

    [Export]
    private int MaxPrimaryWeaponCount = 3;

    [Export]
    private int MaxSecondaryWeaponCount = 1;


    [Export]
    protected float PositionReachedRadius = 5.0f;

    [Export]
    private String _unitName = "Default";

    [Export]
    private String _displayName = "Default";

    private int _health;

    private int _energy;

    protected AudioStream explosionMusicClip = (AudioStream)GD.Load("res://assets/sounds/explosion_large_07.wav");
    protected AudioStream moveMusicClip = (AudioStream)GD.Load("res://assets/sounds/sci-fi_device_item_power_up_flash_01.wav");

    protected GameStates gameStates;
    protected Network network;

    protected Agent target = null;

    protected GameWorld _gameWorld;

    private int defeatedAgentCount = 0;

    private RemoteTransform2D _remoteTransform2D;

    private Team _team;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        gameStates = (GameStates)GetNode("/root/GAMESTATES");
        network = (Network)GetNode("/root/NETWORK");

        _remoteTransform2D = (RemoteTransform2D)GetNode("CameraRemoteTransform");

        Particles2D smoke = (Particles2D)GetNode("Smoke");
        smoke.Emitting = false;

        _health = MaxHealth;
        _energy = MaxEnergy;

        currentPrimaryWeaponIndex = -1;
        currentSecondaryWeaponIndex = -1;

        EmitSignal(nameof(HealthChangedSignal), _health * 100 / MaxHealth);
        EmitSignal(nameof(EnergyChangedSignal), _health * 100 / MaxHealth);
    }

    public virtual void Initialize(GameWorld gameWorld, String unitName, String displayName, Team.TeamCode inputTeamCode, PathFinding pathFinding)
    {
        _team = (Team)GetNode("Team");

        _gameWorld = gameWorld;
        SetCurrentTeam(inputTeamCode);
        SetUnitName(unitName);
        SetDisplayName(displayName);

        _health = MaxHealth;
        _energy = MaxEnergy;

        // Temporary script to automatic load weapon
        UpdatePrimaryWeapon((PackedScene)GD.Load("res://weapons/Rifile.tscn"));
        UpdatePrimaryWeapon((PackedScene)GD.Load("res://weapons/MissleLauncher.tscn"));
        UpdatePrimaryWeapon((PackedScene)GD.Load("res://weapons/LaserGun.tscn"));
    }

    public virtual void changePrimaryWeapon(int weaponIndex)
    {
        if (currentPrimaryWeaponIndex != -1)
        {
            Weapon currentWeapon = ((Weapon)primaryWeapons[currentPrimaryWeaponIndex]);

            // Change of weapon, disable reloading
            if (currentPrimaryWeaponIndex != weaponIndex)
            {
                // Emit singal to disable any previous relad signal
                currentWeapon.EmitSignal(nameof(Weapon.ReloadStopSignal));
            }

            DisconnectWeapon(currentWeapon, Weapon.WeaponOrder.Primary);

            currentWeapon.Hide();
            currentPrimaryWeaponIndex = weaponIndex % primaryWeapons.Count;
            if (currentPrimaryWeaponIndex < 0) { currentPrimaryWeaponIndex = currentPrimaryWeaponIndex * -1; }

            currentWeapon = ((Weapon)primaryWeapons[currentPrimaryWeaponIndex]);

            currentWeapon.Show();
            EmitSignal(nameof(PrimaryWeaponChangeSignal), ((Weapon)primaryWeapons[currentPrimaryWeaponIndex]).CurrentWeaponType);

            ConnectWeapon((Weapon)primaryWeapons[currentPrimaryWeaponIndex], Weapon.WeaponOrder.Primary);

            // Emit bullet to update info
            currentWeapon.EmitSignal(nameof(Weapon.AmmoChangeSignal), currentWeapon.getAmmo(), currentWeapon.getMaxAmmo());

        }
    }

    public virtual void changeSecondaryWeapon(int weaponIndex)
    {
        if (currentSecondaryWeaponIndex != -1)
        {
            Weapon currentWeapon = ((Weapon)secondaryWeapons[currentSecondaryWeaponIndex]);

            // Change of weapon, disable reloading
            if (currentSecondaryWeaponIndex != weaponIndex)
            {
                // Emit singal to disable any previous relad signal
                currentWeapon.EmitSignal(nameof(Weapon.ReloadStopSignal));
            }

            DisconnectWeapon(currentWeapon, Weapon.WeaponOrder.Primary);

            DisconnectWeapon((Weapon)primaryWeapons[currentPrimaryWeaponIndex], Weapon.WeaponOrder.Secondary);

            currentWeapon.Hide();
            currentSecondaryWeaponIndex = weaponIndex % secondaryWeapons.Count;
            if (currentSecondaryWeaponIndex < 0) { currentSecondaryWeaponIndex = currentSecondaryWeaponIndex * -1; }

            currentWeapon = ((Weapon)secondaryWeapons[currentSecondaryWeaponIndex]);

            currentWeapon.Show();
            EmitSignal(nameof(SecondaryWeaponChangeSignal), currentWeapon.CurrentWeaponType);

            ConnectWeapon(currentWeapon, Weapon.WeaponOrder.Secondary);

            // Emit bullet to update info
            currentWeapon.EmitSignal(nameof(Weapon.AmmoChangeSignal), currentWeapon.getAmmo(), currentWeapon.getMaxAmmo());
        }
    }

    protected virtual void DisconnectWeapon(Weapon currentWeapon, Weapon.WeaponOrder weaponOrder)
    {
    }

    protected virtual void ConnectWeapon(Weapon currentWeapon, Weapon.WeaponOrder weaponOrder)
    {
        if (currentWeapon != null)
        {
            // If the current weapon ammo is 0, then notify about out of ammo
            if (currentWeapon.getAmmo() == 0)
            {
                currentWeapon.EmitSignal(nameof(Weapon.AmmoOutSignal));
            }
        }
    }

    public Boolean HasReachedPosition(Vector2 targetPosition)
    {
        return GlobalPosition.DistanceTo(targetPosition) < PositionReachedRadius;
    }

    public Weapon GetCurrentPrimaryWeapon()
    {
        return (Weapon)primaryWeapons[currentPrimaryWeaponIndex];
    }

    public Weapon GetCurrentSecondaryWeapon()
    {
        return (Weapon)primaryWeapons[currentPrimaryWeaponIndex];
    }

    public bool UpdatePrimaryWeapon(PackedScene weapon)
    {
        if (primaryWeapons.Count < MaxPrimaryWeaponCount)
        {
            // Hide existing weapon if exist
            if (currentPrimaryWeaponIndex != -1)
            {
                ((Weapon)primaryWeapons[currentPrimaryWeaponIndex]).Hide();
            }

            Position2D weaponHolder = ((Position2D)GetNode("Weapon"));
            Weapon currentWeapon = (Weapon)(weapon.Instance());
            currentWeapon.Initialize(_gameWorld, this);
            primaryWeapons.Add(currentWeapon);

            // Update to use this weapon as primary
            currentPrimaryWeaponIndex = primaryWeapons.Count - 1;
            weaponHolder.AddChild(currentWeapon);
            EmitSignal(nameof(PrimaryWeaponChangeSignal), ((Weapon)primaryWeapons[currentPrimaryWeaponIndex]).CurrentWeaponType);
            return true;
        }

        return false;
    }

    public bool UpdateSecondaryWeapon(PackedScene weapon)
    {
        if (secondaryWeapons.Count < MaxSecondaryWeaponCount)
        {
            // Hide existing weapon if exist
            if (currentSecondaryWeaponIndex != -1)
            {
                ((Weapon)secondaryWeapons[currentSecondaryWeaponIndex]).Hide();
            }

            Position2D weaponHolder = ((Position2D)GetNode("Weapon"));
            Weapon currentWeapon = (Weapon)(weapon.Instance());
            currentWeapon.Initialize(_gameWorld, this);
            secondaryWeapons.Add(currentWeapon);
            // Update to use this weapon as secondary
            currentSecondaryWeaponIndex = secondaryWeapons.Count - 1;
            weaponHolder.AddChild(currentWeapon);
            EmitSignal(nameof(SecondaryWeaponChangeSignal), ((Weapon)secondaryWeapons[currentSecondaryWeaponIndex]).CurrentWeaponType);

            return true;
        }

        return false;
    }

    public void SetCurrentTeam(Team.TeamCode inputTeamCode)
    {
        _team.CurrentTeamCode = inputTeamCode;
        _setUnitDisplay();
    }

    private void _setUnitDisplay()
    {
        ((Label)(GetNode("UnitDisplay/Name"))).Text = _displayName + "(" + _team.CurrentTeamCode + ")";
    }

    public Team.TeamCode GetCurrentTeam()
    {
        return _team.CurrentTeamCode;
    }

    public String GetDisplayName()
    {
        return _displayName;
    }

    public void SetUnitName(String unitName)
    {
        _unitName = unitName;
    }

    public void SetDisplayName(String displayName)
    {
        _displayName = displayName;
        _setUnitDisplay();
    }

    public String GetUnitName()
    {
        return _unitName;
    }

    public virtual void _Control(float delta) { }

    public virtual void MoveToward(Vector2 moveDir, float delta)
    {
        Velocity = moveDir.Normalized() * MaxSpeed * delta * 100;
        MoveAndSlide(Velocity);
    }

    protected void speedUpBoostTrail()
    {
        Particles2D boosterTrail = (Particles2D)GetNode("Partilcle2DBoosterTrail");

        boosterTrail.SpeedScale = 10;
        boosterTrail.Lifetime = 3;

    }

    protected void slowDownBoostTrail()
    {
        Particles2D boosterTrail = (Particles2D)GetNode("Partilcle2DBoosterTrail");

        boosterTrail.SpeedScale = 1;
        boosterTrail.Lifetime = 1;
    }


    public void Sync(Vector2 position, float rotation, int primaryWeapon, int secondaryWeapon)
    {

        Particles2D boosterTrail = (Particles2D)GetNode("Partilcle2DBoosterTrail");

        // Move effect
        if (position != Position)
        {
            //AudioManager audioManager = (AudioManager)GetNode("/root/AUDIOMANAGER");
            //audioManager.playSoundEffect(moveMusicClip);

            speedUpBoostTrail();
        }
        else
        {
            slowDownBoostTrail();
        }


        Fire(primaryWeapon, secondaryWeapon);

        GlobalPosition = position;
        GlobalRotation = rotation;
    }


    public void RotateToward(Vector2 location, float delta)
    {
        GlobalRotation = Mathf.LerpAngle(GlobalRotation, GlobalPosition.DirectionTo(location).Angle(), RotationSpeed * delta);
    }

    public void Fire(int primaryWeapon, int secondaryWeapon)
    {

        if (primaryWeapon == (int)GameStates.PlayerInput.InputAction.RELOAD)
        {
            ReloadPrimaryWeapon();
        }

        if (secondaryWeapon == (int)GameStates.PlayerInput.InputAction.RELOAD)
        {
            ReloadSecondaryWeapon();
        }

        if (primaryWeapon == (int)GameStates.PlayerInput.InputAction.TRIGGER && currentPrimaryWeaponIndex != -1)
        {
            // knock back effect
            if (((Weapon)primaryWeapons[currentPrimaryWeaponIndex]).Fire(target) && MaxSpeed != 0)
            {
                Vector2 dir = (new Vector2(1, 0)).Rotated(GlobalRotation);
                MoveAndSlide(dir * -10 * ((Weapon)primaryWeapons[currentPrimaryWeaponIndex]).KnockbackForce);
            }
        }

        // Secondary can only be invoked if primary is not invoked
        if (primaryWeapon != (int)GameStates.PlayerInput.InputAction.TRIGGER && secondaryWeapon == (int)GameStates.PlayerInput.InputAction.TRIGGER && currentSecondaryWeaponIndex != -1)
        {
            // knock back effect
            if (((Weapon)secondaryWeapons[currentSecondaryWeaponIndex]).Fire(target) && MaxSpeed != 0)
            {
                Vector2 dir = (new Vector2(1, 0)).Rotated(GlobalRotation);
                MoveAndSlide(dir * -10 * ((Weapon)secondaryWeapons[currentSecondaryWeaponIndex]).KnockbackForce);
            }
        }
    }

    public void setHealth(int health)
    {
        _health = health;
        EmitSignal(nameof(HealthChangedSignal), health * 100 / MaxHealth);
    }

    public int getHealth()
    {
        return _health;
    }

    public void IncrementDefeatedAgentCount()
    {
        defeatedAgentCount++;
        EmitSignal(nameof(DefeatedAgentChangedSignal), defeatedAgentCount);
    }

    public int getDefeatedAgentCount()
    {
        return defeatedAgentCount;
    }

    public void TakeDamage(int amount, Vector2 dir, Agent source, Team sourceTeam)
    {
        int originalHealth = _health;
        bool trackDamage = true;
        bool sourceAlive = true;

        if (source == null || !IsInstanceValid(source))
        {
            sourceAlive = false;
        }

        if (sourceTeam.CurrentTeamCode == GetCurrentTeam())
        {
            trackDamage = false;
        }

        if (trackDamage)
        {
            _health -= amount;

            EmitSignal(nameof(HealthChangedSignal), _health * 100 / MaxHealth);

            if (_health < MaxHealth / 2)
            {
                Particles2D smoke = (Particles2D)GetNode("Smoke");
                smoke.Emitting = true;
            }

            // Only the one that actually damage agent to 0 will be count as the one defeated
            if (originalHealth > 0 && _health <= 0)
            {
                if (sourceAlive)
                {
                    source.IncrementDefeatedAgentCount();
                }
            }
        }


        // knock back effect
        if (MaxSpeed != 0)
        {
            MoveAndSlide(dir * 10 * amount);
        }
    }

    public void SetCameraRemotePath(Camera2D camera)
    {
        _remoteTransform2D.RemotePath = camera.GetPath();
    }

    public void ReloadPrimaryWeapon()
    {
        ((Weapon)primaryWeapons[currentPrimaryWeaponIndex]).StartReload();
        PrimaryWeaponReloading = false;
    }

    public void ReloadSecondaryWeapon()
    {
        ((Weapon)secondaryWeapons[currentSecondaryWeaponIndex]).StartReload();
        SecondaryWeaponReloading = false;
    }

    public void Heal(int amount)
    {
        _health = +amount;

        if (_health > MaxHealth)
        {
            _health = MaxHealth;
        }

        if (_health >= MaxHealth / 2)
        {
            Particles2D smoke = (Particles2D)GetNode("Smoke");
            smoke.Emitting = false;
        }
    }

    public bool AmmoIncrease(Weapon.WeaponAmmoType weaponAmmoType, int amount)
    {
        bool consume = false;

        if (weaponAmmoType != Weapon.WeaponAmmoType.ENERGY)
        {
            foreach (Weapon currentWeapon in primaryWeapons)
            {
                if (weaponAmmoType == currentWeapon.CurrentWeaponAmmoType)
                {
                    consume = true;
                    ((Weapon)primaryWeapons[currentPrimaryWeaponIndex]).AmmoIncrease(amount);
                }
            }
        }
        else
        {
            _energy = +amount;

            if (_energy > MaxEnergy)
            {
                _energy = MaxEnergy;
            }
        }

        return consume;
    }

    public void Explode()
    {
        CollisionShape2D collisionShape2D = (CollisionShape2D)GetNode("CollisionShape2D");
        collisionShape2D.Disabled = true;
        Alive = false;
        Sprite body = (Sprite)GetNode("Body");
        body.Hide();

        AnimatedSprite animatedSprite = (AnimatedSprite)GetNode("Explosion");
        animatedSprite.Show();
        animatedSprite.Play("fire");

        AudioManager audioManager = (AudioManager)GetNode("/root/AUDIOMANAGER");
        audioManager.playSoundEffect(explosionMusicClip);
    }

    private void _OnExplosionAnimationFinished()
    {
        QueueFree();
        EmitSignal(nameof(DeadSignal));
    }

}
