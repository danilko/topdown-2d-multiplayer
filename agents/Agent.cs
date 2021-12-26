using Godot;
using System;
using System.Collections.Generic;

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
    public delegate void WeaponChangeSignal();

    [Export]
    public int MaxSpeed { get; set; }

    [Export]
    protected float RotationSpeed;

    [Export]
    public int MaxHealth { get; set; }

    [Export]
    protected int MaxEnergy;

    [Export]
    protected float DetectionRadius = 1500.0f;

    public Vector2 Velocity;
    protected Boolean Alive = true;

    public float currentTime = 0;

    public int RightWeaponAction { set; get; }
    public int LeftWeaponAction { set; get; }
    protected List<Weapon> RightWeapons;
    protected List<Weapon> LeftWeapons;

    protected Dictionary<Weapon.WeaponOrder, int> CurrentWeaponIndex;

    [Export]
    private int MaxWeaponCount = 3;


    [Export]
    protected float PositionReachedRadius = 5.0f;

    [Export]
    private String _unitID = "Default";

    [Export]
    private String _displayName = "Default";

    private int _health;

    private int _energy;

    protected AudioStream explosionMusicClip = (AudioStream)GD.Load("res://assets/sounds/explosion_large_07.wav");
    protected AudioStream moveMusicClip = (AudioStream)GD.Load("res://assets/sounds/sci-fi_device_item_power_up_flash_01.wav");

    protected GameStates gameStates;
    protected Network network;

    protected GameWorld _gameWorld;

    private int defeatedAgentCount = 0;

    private RemoteTransform2D _remoteTransform2D;

    private Team _team;

    public TeamMapAI _teamMapAI;

    protected Inventory CurrentInventory;

    protected Timer DamageEffectTimer;

    protected Sprite Body;

    protected DetectionZone DetectionZone;

    protected bool CurrentPlayer = false;

    protected Agent CurrentTargetAgent;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {

        RightWeapons = new List<Weapon>();
        LeftWeapons = new List<Weapon>();
        CurrentWeaponIndex = new Dictionary<Weapon.WeaponOrder, int>();


        gameStates = (GameStates)GetNode("/root/GAMESTATES");
        network = (Network)GetNode("/root/NETWORK");

        _remoteTransform2D = (RemoteTransform2D)GetNode("CameraRemoteTransform");

        Particles2D smoke = (Particles2D)GetNode("Smoke");
        smoke.Emitting = false;

        CurrentInventory = (Inventory)GetNode("Inventory");
        DamageEffectTimer = (Timer)GetNode("DamageEffectTimer");
        Body = (Sprite)GetNode("Body");

        _health = MaxHealth;
        _energy = MaxEnergy;

        CurrentWeaponIndex.Add(Weapon.WeaponOrder.Left, 0);
        CurrentWeaponIndex.Add(Weapon.WeaponOrder.Right, 0);

        EmitSignal(nameof(HealthChangedSignal), _health * 100 / MaxHealth);
        EmitSignal(nameof(EnergyChangedSignal), _health * 100 / MaxHealth);

        DetectionZone = (DetectionZone)(GetNode("DetectionZone"));

        CurrentTargetAgent = null;
    }

    public DetectionZone GetDetectionZone()
    {
        return DetectionZone;
    }

    public Boolean IsCurrentPlayer()
    {
        return CurrentPlayer;
    }

    public Inventory GetInventory()
    {
        return CurrentInventory;
    }

    public virtual void Initialize(GameWorld gameWorld, String unitID, String displayName, TeamMapAI teamMapAI, PathFinding pathFinding)
    {
        _team = (Team)GetNode("Team");
        _teamMapAI = teamMapAI;

        _gameWorld = gameWorld;
        SetCurrentTeam(_teamMapAI.GetTeam());
        SetUnitID(unitID);
        SetDisplayName(displayName);

        _health = MaxHealth;
        _energy = MaxEnergy;

        CurrentInventory.Initialize(this);

        _initializeWeapon(LeftWeapons);
        _initializeWeapon(RightWeapons);

        DetectionZone.Initialize(gameWorld, this, DetectionRadius);
    }

    public GameWorld GetGameWorld()
    {
        return _gameWorld;
    }

    private void _initializeWeapon(List<Weapon> weapons)
    {
        for (int index = 0; index < MaxWeaponCount; index++)
        {
            weapons.Add(null);
        }
    }

    public int GetCurrentWeaponIndex(Weapon.WeaponOrder weaponOrder)
    {
        return CurrentWeaponIndex[weaponOrder];
    }

    public virtual void ChangeWeapon(int weaponIndex, Weapon.WeaponOrder weaponOrder)
    {
        weaponIndex = Mathf.Abs(weaponIndex);
        List<Weapon> weapons = GetWeapons(weaponOrder);

        // Caculate actual index base on availble weapon
        weaponIndex = weaponIndex % weapons.Count;

        int originalWeaponIndex = CurrentWeaponIndex[weaponOrder];

        Weapon currentWeapon = ((Weapon)weapons[originalWeaponIndex]);

        if (currentWeapon != null && IsInstanceValid(currentWeapon))
        {
            DisconnectWeapon(currentWeapon, weaponOrder);
        }

        CurrentWeaponIndex[weaponOrder] = weaponIndex % weapons.Count;
        currentWeapon = ((Weapon)weapons[CurrentWeaponIndex[weaponOrder]]);


        if (currentWeapon != null && IsInstanceValid(currentWeapon))
        {
            ConnectWeapon(currentWeapon, weaponOrder);

            // This is re-click, no need to submit signal
            if (originalWeaponIndex != weaponIndex)
            {
                EmitSignal(nameof(WeaponChangeSignal), CurrentInventory.GetItems()[CurrentInventory.GetEquipItemIndex(weaponOrder, weaponIndex)], weaponOrder, weaponIndex);
            }

            // Emit signal to update info
            currentWeapon.EmitSignal(nameof(Weapon.AmmoChangeSignal), currentWeapon.GetAmmo(), currentWeapon.GetMaxAmmo(), weaponOrder);
        }
        else
        {

            // This is re-click, no need to submit signal
            if (originalWeaponIndex != weaponIndex)
            {
                EmitSignal(nameof(WeaponChangeSignal), null, weaponOrder, weaponIndex);
            }
        }
    }

    protected virtual void DisconnectWeapon(Weapon currentWeapon, Weapon.WeaponOrder weaponOrder)
    {
        if (currentWeapon != null && IsInstanceValid(currentWeapon))
        {
            currentWeapon.EquipWeapon(false);
            currentWeapon.Hide();
        }
    }

    protected virtual void ConnectWeapon(Weapon currentWeapon, Weapon.WeaponOrder weaponOrder)
    {
        if (currentWeapon != null && IsInstanceValid(currentWeapon))
        {
            currentWeapon.EquipWeapon(true);

            // If the current weapon ammo is 0, then notify about out of ammo
            if (currentWeapon.GetAmmo() == 0)
            {
                currentWeapon.EmitSignal(nameof(Weapon.AmmoOutSignal), weaponOrder);
            }


            currentWeapon.Show();
        }
    }

    public Boolean HasReachedPosition(Vector2 targetPosition)
    {
        return GlobalPosition.DistanceTo(targetPosition) < PositionReachedRadius;
    }

    public Weapon GetCurrentWeapon(Weapon.WeaponOrder weaponOrder)
    {
        if (weaponOrder == Weapon.WeaponOrder.Right)
        {
            return (Weapon)RightWeapons[CurrentWeaponIndex[weaponOrder]];
        }
        else
        {
            return (Weapon)LeftWeapons[CurrentWeaponIndex[weaponOrder]];
        }
    }

    /**
    AddWeapon
    Assign weapon to target weaponOrder and index
    **/
    public bool EquipWeapon(PackedScene weaponScene, Weapon.WeaponOrder weaponOrder, int index)
    {
        List<Weapon> weapons = GetWeapons(weaponOrder);

        if (weapons[index] != null)
        {
            return false;
        }

        Node2D weaponHolder = GetWeaponsHolder(weaponOrder);
        Weapon weapon = (Weapon)(weaponScene.Instance());
        weaponHolder.AddChild(weapon);
        weapon.Initialize(_gameWorld, this, weaponOrder, index);
        weapons[index] = weapon;
        weapon.Hide();

        // If it is current weapon, then perform weapon change
        if (index == CurrentWeaponIndex[weaponOrder])
        {
            ChangeWeapon(index, weaponOrder);
        }

        return true;
    }

    public List<Weapon> GetWeapons(Weapon.WeaponOrder weaponOrder)
    {
        if (weaponOrder == Weapon.WeaponOrder.Right)
        {
            return RightWeapons;
        }
        else
        {
            return LeftWeapons;
        }
    }

    public Node2D GetWeaponsHolder(Weapon.WeaponOrder weaponOrder)
    {
        return ((Node2D)GetNode(weaponOrder + "WeaponHolder"));
    }

    /**
    Unequip weapon at given weapon order's given index
    **/
    public void UnequipWeapon(Weapon.WeaponOrder weaponOrder, int weaponIndex)
    {
        List<Weapon> weapons = GetWeapons(weaponOrder);

        Weapon weapon = (Weapon)weapons[weaponIndex];

        if (weapon != null)
        {
            Node2D weaponHolder = GetWeaponsHolder(weaponOrder);
            weaponHolder.RemoveChild(weapon);
            // Null the weapon
            weapons[weaponIndex] = null;
            DisconnectWeapon(weapon, weaponOrder);
            // Empty out weapon
            weapon.Deinitialize();
        }

        EmitSignal(nameof(WeaponChangeSignal), null, weaponOrder, weaponIndex);
    }

    public void SetCurrentTeam(Team.TeamCode inputTeamCode)
    {
        _team.CurrentTeamCode = inputTeamCode;
        _setUnitDisplay();
    }

    private void _setUnitDisplay()
    {
        ((Label)(GetNode("UnitDisplay/Name"))).Text = _displayName + "(" + _team.CurrentTeamCode + ")";
        //Set team indicator
        ((Sprite)(GetNode("TeamIndicator"))).Modulate = new Color(_team.getTeamColor(_team.CurrentTeamCode), 0.5f);
    }

    public Team.TeamCode GetTeam()
    {
        return _team.CurrentTeamCode;
    }


    public TeamMapAI GetTeamMapAI()
    {
        return _teamMapAI;
    }



    public String GetDisplayName()
    {
        return _displayName;
    }

    public void SetUnitID(String unitID)
    {
        _unitID = unitID;
    }

    public void SetDisplayName(String displayName)
    {
        _displayName = displayName;
        _setUnitDisplay();
    }

    public String GetUnitID()
    {
        return _unitID;
    }

    public virtual void _Control(float delta) { }

    public virtual void MoveToward(Vector2 moveDir, float delta)
    {
        Velocity = moveDir.Normalized() * MaxSpeed;

        // Set the velocity and also set up to be 0 to simulate everything to be wall as top down
        MoveAndSlide(Velocity, Vector2.Zero);
    }

    protected void speedUpBoostTrail()
    {
        for (int index = 0; index < (int)Weapon.WeaponOrder.Left; index++)
        {
            Particles2D boosterTrail = (Particles2D)GetNode((Weapon.WeaponOrder)index + "Booster/BoosterTrail");

            boosterTrail.SpeedScale = 10;
            boosterTrail.Lifetime = 3;
        }


    }

    protected void slowDownBoostTrail()
    {
        for (int index = 0; index < (int)Weapon.WeaponOrder.Left; index++)
        {
            Particles2D boosterTrail = (Particles2D)GetNode((Weapon.WeaponOrder)index + "Booster/BoosterTrail");

            boosterTrail.SpeedScale = 1;
            boosterTrail.Lifetime = 1;
        }
    }


    public void Sync(Vector2 position, float rotation, int rightWeapon, int leftWeapon)
    {
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

        GlobalPosition = position;
        GlobalRotation = rotation;

        Fire(Weapon.WeaponOrder.Right, rightWeapon);
        Fire(Weapon.WeaponOrder.Left, leftWeapon);

    }


    public void RotateToward(Vector2 location, float delta)
    {
        GlobalRotation = Mathf.LerpAngle(GlobalRotation, GlobalPosition.DirectionTo(location).Angle(), RotationSpeed * delta);
    }

    public void Fire(Weapon.WeaponOrder weaponOrder, int weaponAction)
    {
        Weapon weapon = GetWeapons(weaponOrder)[CurrentWeaponIndex[weaponOrder]];

        if (weapon != null)
        {
            if (weaponAction == (int)NetworkSnapshotManager.PlayerInput.InputAction.RELOAD)
            {
                ReloadWeapon(weaponOrder);
            }
            else if (weaponAction == (int)NetworkSnapshotManager.PlayerInput.InputAction.TRIGGER)
            {

                Vector2 fireTarget = this.GlobalPosition + new Vector2(2000, 0).Rotated(GlobalRotation);

                // knock back effect
                if (weapon.Fire(CurrentTargetAgent, fireTarget) && MaxSpeed != 0)
                {
                    Vector2 dir = (new Vector2(1, 0)).Rotated(GlobalRotation).Normalized();

                    if (weapon.KnockbackForce > 0)
                    {
                        ApplyKnockBackForce(-1.0f * dir * weapon.KnockbackForce);
                    }


                }
            }
        }

    }

    public void ApplyKnockBackForce(Vector2 force)
    {
        // 100 is the tweak effect to have a nice knocback
        MoveAndSlide(force * 100);
    }

    public void setHealth(int health)
    {
        _health = health;
        EmitSignal(nameof(HealthChangedSignal), health * 100 / MaxHealth);
    }

    public void setEnergy(int energy)
    {
        _energy = energy;
        EmitSignal(nameof(HealthChangedSignal), energy * 100 / MaxEnergy);
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

        if (sourceTeam.CurrentTeamCode == GetTeam())
        {
            trackDamage = false;
        }

        if (trackDamage)
        {
            if (DamageEffectTimer.IsStopped())
            {
                Body.SelfModulate = new Color(5.0f, 5.0f, 5.0f, 1.0f);
                DamageEffectTimer.Start();
            }

            _health -= amount;

            EmitSignal(nameof(HealthChangedSignal), _health * 100 / MaxHealth);

            ApplyKnockBackForce(-1.0f * dir * amount * 0.5f);

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

    public void DamageEffectTimerTimeout()
    {
        Body.SelfModulate = new Color(1.0f, 1.0f, 1.0f, 1.0f);
    }

    public void ReloadWeapon(Weapon.WeaponOrder weaponOrder)
    {
        ((Weapon)GetWeapons(weaponOrder)[CurrentWeaponIndex[weaponOrder]]).StartReload();
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

        EmitSignal(nameof(HealthChangedSignal), _health);
    }

    public virtual void Explode()
    {
        for (int index = 0; index <= (int)Weapon.WeaponOrder.Left; index++)
        {
            List<Weapon> weapons = GetWeapons((Weapon.WeaponOrder)index);

            foreach (Weapon weapon in weapons)
            {
                if (weapon != null)
                {
                    weapon.Deinitialize();
                }
            }
        }

        CollisionShape2D collisionShape2D = (CollisionShape2D)GetNode("CollisionShape2D");
        collisionShape2D.Disabled = true;
        Alive = false;
        Sprite body = (Sprite)GetNode("Body");
        body.Hide();

        // Disable remain for performance reason
        //RemainParticles remainParticles = (RemainParticles)((PackedScene)GD.Load("res://effects/RemainParticles.tscn")).Instance();
        //remainParticles.GlobalPosition = this.GlobalPosition;
        //_gameWorld.GetNode("RemainEffectManager").AddChild(remainParticles);

        AgentExplosionParticle agentExplosionParticle = (AgentExplosionParticle)GetNode("AgentExplosionParticle");
        agentExplosionParticle.SetTrigger(true);

        AudioManager audioManager = (AudioManager)GetNode("/root/AUDIOMANAGER");
        audioManager.playSoundEffect(explosionMusicClip);



    }

    public virtual void OnTargetAgentChange()
    {
        CurrentTargetAgent = DetectionZone.getTargetAgent();
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
