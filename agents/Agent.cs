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
    public delegate void RightWeaponChangeSignal();

    [Signal]
    public delegate void LeftWeaponChangeSignal();

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

    public int currentRightWeaponIndex { get; set; }
    public int currentLeftWeaponIndex { get; set; }
    public int RightWeaponAction { set; get; }
    public int LeftWeaponAction { set; get; }
    public bool RightWeaponReloading { set; get; }
    public bool LeftWeaponReloading { set; get; }

    protected Godot.Collections.Array RightWeapons = new Godot.Collections.Array();
    protected Godot.Collections.Array LeftWeapons = new Godot.Collections.Array();

    [Export]
    private int MaxRightWeaponCount = 3;

    [Export]
    private int MaxLeftWeaponCount = 3;


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

        currentRightWeaponIndex = -1;
        currentLeftWeaponIndex = -1;

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
        UpdateRightWeapon((PackedScene)GD.Load("res://weapons/Rifile.tscn"));
        UpdateRightWeapon((PackedScene)GD.Load("res://weapons/MissleLauncher.tscn"));
        UpdateRightWeapon((PackedScene)GD.Load("res://weapons/LaserGun.tscn"));

        UpdateLeftWeapon((PackedScene)GD.Load("res://weapons/Shield.tscn"));
        UpdateLeftWeapon((PackedScene)GD.Load("res://weapons/LightSaber.tscn"));
        UpdateLeftWeapon((PackedScene)GD.Load("res://weapons/LaserGun.tscn"));
    }

    public virtual void changeRightWeapon(int weaponIndex)
    {
        weaponIndex = Mathf.Abs(weaponIndex);

        if (RightWeapons.Count != 0)
        {
            // No need to change weapon if index is same
            if (weaponIndex % RightWeapons.Count == currentRightWeaponIndex)
            {
                return;
            }

            Weapon currentWeapon = ((Weapon)RightWeapons[currentRightWeaponIndex]);

            // Change of weapon, disable reloading
            if (currentRightWeaponIndex != weaponIndex)
            {
                // Emit singal to disable any previous relad signal
                currentWeapon.EmitSignal(nameof(Weapon.ReloadStopSignal));
            }

            DisconnectWeapon(currentWeapon, Weapon.WeaponOrder.Right);

            currentWeapon.Hide();
            currentRightWeaponIndex = weaponIndex % RightWeapons.Count;

            currentWeapon = ((Weapon)RightWeapons[currentRightWeaponIndex]);

            currentWeapon.Show();
            EmitSignal(nameof(RightWeaponChangeSignal), ((Weapon)RightWeapons[currentRightWeaponIndex]).CurrentWeaponType);

            ConnectWeapon((Weapon)RightWeapons[currentRightWeaponIndex], Weapon.WeaponOrder.Right);

            // Emit signal to update info
            currentWeapon.EmitSignal(nameof(Weapon.AmmoChangeSignal), currentWeapon.getAmmo(), currentWeapon.getMaxAmmo());

        }
    }

    public virtual void changeLeftWeapon(int weaponIndex)
    {
        weaponIndex = Mathf.Abs(weaponIndex);

        if (LeftWeapons.Count != 0)
        {
            // No need to change weapon if index is same
            if (weaponIndex % LeftWeapons.Count == currentLeftWeaponIndex)
            {
                return;
            }

            Weapon currentWeapon = ((Weapon)LeftWeapons[currentLeftWeaponIndex]);

            // Change of weapon, disable reloading
            if (currentLeftWeaponIndex != weaponIndex)
            {
                // Emit singal to disable any previous relad signal
                currentWeapon.EmitSignal(nameof(Weapon.ReloadStopSignal));
            }

            DisconnectWeapon(currentWeapon, Weapon.WeaponOrder.Left);
            DisconnectWeapon((Weapon)RightWeapons[currentRightWeaponIndex], Weapon.WeaponOrder.Left);

            currentWeapon.Hide();
            currentLeftWeaponIndex = weaponIndex % LeftWeapons.Count;

            currentWeapon = ((Weapon)LeftWeapons[currentLeftWeaponIndex]);

            currentWeapon.Show();

            EmitSignal(nameof(LeftWeaponChangeSignal), ((Weapon)LeftWeapons[currentRightWeaponIndex]).CurrentWeaponType);

            ConnectWeapon(currentWeapon, Weapon.WeaponOrder.Left);

            // Emit signal to update info
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

    public Weapon GetCurrentRightWeapon()
    {
        return (Weapon)RightWeapons[currentRightWeaponIndex];
    }

    public Weapon GetCurrentLeftWeapon()
    {
        return (Weapon)RightWeapons[currentRightWeaponIndex];
    }

    public bool UpdateRightWeapon(PackedScene weapon)
    {
        if (RightWeapons.Count < MaxRightWeaponCount)
        {
            // Hide existing weapon if exist
            if (currentRightWeaponIndex != -1)
            {
                ((Weapon)RightWeapons[currentRightWeaponIndex]).Hide();
            }

            Position2D weaponHolder = ((Position2D)GetNode("RightWeaponHolder"));
            Weapon currentWeapon = (Weapon)(weapon.Instance());
            currentWeapon.Initialize(_gameWorld, this);
            RightWeapons.Add(currentWeapon);

            // Update to use this weapon as primary
            currentRightWeaponIndex = RightWeapons.Count - 1;
            weaponHolder.AddChild(currentWeapon);
            EmitSignal(nameof(RightWeaponChangeSignal), ((Weapon)RightWeapons[currentRightWeaponIndex]).CurrentWeaponType);
            return true;
        }

        return false;
    }

    public bool UpdateLeftWeapon(PackedScene weapon)
    {
        if (LeftWeapons.Count < MaxLeftWeaponCount)
        {
            // Hide existing weapon if exist
            if (currentLeftWeaponIndex != -1)
            {
                ((Weapon)LeftWeapons[currentLeftWeaponIndex]).Hide();
            }

            Position2D weaponHolder = ((Position2D)GetNode("LeftWeaponHolder"));
            Weapon currentWeapon = (Weapon)(weapon.Instance());
            currentWeapon.Initialize(_gameWorld, this);
            LeftWeapons.Add(currentWeapon);

            // Update to use this weapon as secondary
            currentLeftWeaponIndex = LeftWeapons.Count - 1;
            weaponHolder.AddChild(currentWeapon);

            EmitSignal(nameof(LeftWeaponChangeSignal), ((Weapon)LeftWeapons[currentLeftWeaponIndex]).CurrentWeaponType);

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

    public void Fire(int rightWeapon, int leftWeapon)
    {

        if (rightWeapon == (int)GameStates.PlayerInput.InputAction.RELOAD)
        {
            ReloadRightWeapon();
        }

        if (leftWeapon == (int)GameStates.PlayerInput.InputAction.RELOAD)
        {
            ReloadLeftWeapon();
        }

        if (rightWeapon == (int)GameStates.PlayerInput.InputAction.TRIGGER && currentRightWeaponIndex != -1)
        {
            // knock back effect
            if (((Weapon)RightWeapons[currentRightWeaponIndex]).Fire(target) && MaxSpeed != 0)
            {
                Vector2 dir = (new Vector2(1, 0)).Rotated(GlobalRotation);
                MoveAndSlide(dir * -10 * ((Weapon)RightWeapons[currentRightWeaponIndex]).KnockbackForce);
            }
        }

        if (leftWeapon == (int)GameStates.PlayerInput.InputAction.TRIGGER && currentLeftWeaponIndex != -1)
        {
            // knock back effect
            if (((Weapon)LeftWeapons[currentLeftWeaponIndex]).Fire(target) && MaxSpeed != 0)
            {
                Vector2 dir = (new Vector2(1, 0)).Rotated(GlobalRotation);
                MoveAndSlide(dir * -10 * ((Weapon)LeftWeapons[currentLeftWeaponIndex]).KnockbackForce);
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

    public void ReloadRightWeapon()
    {
        ((Weapon)RightWeapons[currentRightWeaponIndex]).StartReload();
        RightWeaponReloading = false;
    }

    public void ReloadLeftWeapon()
    {
        ((Weapon)LeftWeapons[currentLeftWeaponIndex]).StartReload();
        LeftWeaponReloading = false;
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
            foreach (Weapon currentWeapon in RightWeapons)
            {
                if (weaponAmmoType == currentWeapon.CurrentWeaponAmmoType)
                {
                    consume = true;
                    ((Weapon)RightWeapons[currentRightWeaponIndex]).AmmoIncrease(amount);
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

        RemainParticles remainParticles = (RemainParticles)((PackedScene)GD.Load("res://effects/RemainParticles.tscn")).Instance();
        remainParticles.GlobalPosition = this.GlobalPosition;
        _gameWorld.GetNode("RemainEffectManager").AddChild(remainParticles);

        AgentExplosionParticle agentExplosionParticle = (AgentExplosionParticle)GetNode("AgentExplosionParticle");
        agentExplosionParticle.Connect(nameof(AgentExplosionParticle.EffectCompleteSignal), this, nameof(_OnExplosionAnimationFinished));
        agentExplosionParticle.SetTrigger(true);

        AudioManager audioManager = (AudioManager)GetNode("/root/AUDIOMANAGER");
        audioManager.playSoundEffect(explosionMusicClip);
    }

    private void _OnExplosionAnimationFinished()
    {
        QueueFree();
        EmitSignal(nameof(DeadSignal));
    }

    public override void _Process(float delta)
    {
    }

}
