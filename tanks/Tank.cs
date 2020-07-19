using Godot;
using System;

public class Tank : KinematicBody2D
{


    [Signal]
    public delegate void HealthChangedSignal();

    [Signal]
    public delegate void DeadSignal();

    [Signal]
    public delegate void PrimaryWeaponChangeSignal();

    [Signal]
    public delegate void SecondaryWeaponChangeSignal();

    [Export]
    protected int MaxSpeed;

    [Export]
    protected float RotationSpeed;

    [Export]
    protected int MaxHealth;

    [Export]
    protected int MaxEnergy;

    protected Vector2 Velocity;
    protected Boolean Alive = true;

    public float currentTime = 0;

    public int currentPrimaryWeaponIndex { get; set; }
    public int currentSecondaryWeaponIndex { get; set; }

    protected Godot.Collections.Array primaryWeapons = new Godot.Collections.Array();
    protected Godot.Collections.Array secondaryWeapons = new Godot.Collections.Array();

    [Export]
    private int MaxPrimaryWeaponCount = 2;

    [Export]
    private int MaxSecondaryWeaponCount = 1;


    [Export]
    private String unitName = "Default";

    private int health;

    private int energy;

    protected AudioStream musicClip = (AudioStream)GD.Load("res://assets/sounds/explosion_large_07.wav");
    protected AudioStream moveMusicClip = (AudioStream)GD.Load("res://assets/sounds/sci-fi_device_item_power_up_flash_01.wav");

    protected GameStates gameStates;
    protected Network network;

    protected Node2D target = null;

    private String teamIdentifier = "UNKOWN";

    protected GameWorld gameworld;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        gameworld = (GameWorld)GetParent();
        gameStates = (GameStates)GetNode("/root/GAMESTATES");
        network = (Network)GetNode("/root/NETWORK");

        health = MaxHealth;
        energy = MaxEnergy;

        Particles2D smoke = (Particles2D)GetNode("Smoke");
        smoke.Emitting = false;

        EmitSignal(nameof(HealthChangedSignal), health * 100 / MaxHealth);

        currentPrimaryWeaponIndex = -1;
        currentSecondaryWeaponIndex = -1;

        // Temporary script to automatic load weapon
        updatePrimaryWeapon((PackedScene)GD.Load("res://weapons/LaserGun.tscn"));
        updatePrimaryWeapon((PackedScene)GD.Load("res://weapons/Rifile.tscn"));

        EmitSignal(nameof(PrimaryWeaponChangeSignal), ((Weapon)primaryWeapons[currentPrimaryWeaponIndex]).weaponType);
    }

    public void changePrimaryWeapon()
    {
        if (currentPrimaryWeaponIndex != -1)
        {
            ((Weapon)primaryWeapons[currentPrimaryWeaponIndex]).Hide();
            currentPrimaryWeaponIndex = (currentPrimaryWeaponIndex + 1) % primaryWeapons.Count;
            ((Weapon)primaryWeapons[currentPrimaryWeaponIndex]).Show();
            EmitSignal(nameof(PrimaryWeaponChangeSignal), ((Weapon)primaryWeapons[currentPrimaryWeaponIndex]).weaponType);
        }
    }

    public void changeSecondaryWeapon()
    {
        if (currentSecondaryWeaponIndex != -1)
        {
            ((Weapon)secondaryWeapons[currentSecondaryWeaponIndex]).Hide();
            currentSecondaryWeaponIndex = (currentSecondaryWeaponIndex + 1) % secondaryWeapons.Count;
            ((Weapon)secondaryWeapons[currentSecondaryWeaponIndex]).Show();
            EmitSignal(nameof(SecondaryWeaponChangeSignal), ((Weapon)secondaryWeapons[currentSecondaryWeaponIndex]).weaponType);
        }

    }
    public bool updatePrimaryWeapon(PackedScene weapon)
    {
        if (primaryWeapons.Count < MaxPrimaryWeaponCount)
        {
            // Hide existing weapon if exist
            if (currentPrimaryWeaponIndex != -1)
            {
                ((Weapon)primaryWeapons[currentPrimaryWeaponIndex]).Hide();
            }

            Position2D weaponHolder = ((Position2D)GetNode("Weapon"));
            Weapon curretnWeapon = (Weapon)(weapon.Instance());
            curretnWeapon.gameWorld = (GameWorld)GetParent();
            primaryWeapons.Add(curretnWeapon);
            // Update to use this weapon as primary
            currentPrimaryWeaponIndex = primaryWeapons.Count - 1;
            weaponHolder.AddChild(curretnWeapon);
            EmitSignal(nameof(PrimaryWeaponChangeSignal), ((Weapon)primary            EmitSignal(nameof(PrimaryWeaponChangeSignal), ((Weapon)primaryWeapons[currentPrimaryWeaponIndex]).weaponType);Weapons[currentPrimaryWeaponIndex]).weaponType);

            return true;
        }

        return false;
    }

    public bool updateSecondaryWeapon(PackedScene weapon)
    {
        if (secondaryWeapons.Count < MaxSecondaryWeaponCount)
        {
            // Hide existing weapon if exist
            if (currentSecondaryWeaponIndex != -1)
            {
                ((Weapon)secondaryWeapons[currentSecondaryWeaponIndex]).Hide();
            }

            Position2D weaponHolder = ((Position2D)GetNode("Weapon"));
            Weapon curretnWeapon = (Weapon)(weapon.Instance());
            secondaryWeapons.Add(curretnWeapon);
            // Update to use this weapon as primary
            currentSecondaryWeaponIndex = secondaryWeapons.Count - 1;
            weaponHolder.AddChild(curretnWeapon);
            EmitSignal(nameof(SecondaryWeaponChangeSignal), ((Weapon)secondaryWeapons[currentSecondaryWeaponIndex]).weaponType);

            return true;
        }

        return false;
    }

    public void setTeamIdentifier(String inputTeamIdentifier)
    {
        teamIdentifier = inputTeamIdentifier;
        ((Label)(GetNode("UnitDisplay/Name"))).Text = unitName + "(" + teamIdentifier + ")";
    }

    public String getTeamIdentifier()
    {
        return teamIdentifier;
    }

    public void setUnitName(String unitName)
    {
        this.unitName = unitName;
        ((Label)(GetNode("UnitDisplay/Name"))).Text = unitName + "(" + teamIdentifier + ")";
    }

    public String getUnitName()
    {
        return unitName;
    }

    public virtual void _Control(float delta) { }

    public virtual void _shootSecondary() { }


    public void move(Vector2 moveDir, Vector2 pointPosition, float delta)
    {
        // Need to times 100 to catch up with AI movement as there is delay in updating
        Velocity = moveDir.Normalized() * MaxSpeed * delta * 100;
        LookAt(pointPosition);
        MoveAndSlide(Velocity);
    }

    public void set(Vector2 position, float rotation, bool primaryWeapon, bool secondaryWeapon, bool playerMove)
    {

        Particles2D boosterTrail = (Particles2D)GetNode("BoosterTrail");

        // Move effect
        if (playerMove)
        {
            AudioManager audioManager = (AudioManager)GetNode("/root/AUDIOMANAGER");
            audioManager.playSoundEffect(moveMusicClip);
        }
        if (Velocity.x != 0 || Velocity.y != 0)
        {
            boosterTrail.SpeedScale = 2;
        }
        else
        {
            boosterTrail.SpeedScale = 1;
        }

        _shoot(primaryWeapon, secondaryWeapon);

        Position = position;
        Rotation = rotation;
    }

    public void _shoot(bool primaryWeapon, bool secondaryWeapon)
    {
        if (primaryWeapon && currentPrimaryWeaponIndex != -1)
        {
            // knock back effect
            if (((Weapon)primaryWeapons[currentPrimaryWeaponIndex]).fire(target) && MaxSpeed != 0)
            {
                Vector2 dir = (new Vector2(1, 0)).Rotated(GlobalRotation);
                MoveAndSlide(dir * -1 * ((Weapon)primaryWeapons[currentPrimaryWeaponIndex]).KnockbackForce);
            }
        }

        if (secondaryWeapon && currentSecondaryWeaponIndex != -1)
        {
            // knock back effect
            if (((Weapon)secondaryWeapons[currentSecondaryWeaponIndex]).fire(target) && MaxSpeed != 0)
            {
                Vector2 dir = (new Vector2(1, 0)).Rotated(GlobalRotation);
                MoveAndSlide(dir * -1 * ((Weapon)secondaryWeapons[currentSecondaryWeaponIndex]).KnockbackForce);
            }
        }
    }

    public void setHealth(int health)
    {
        this.health = health;
        EmitSignal(nameof(HealthChangedSignal), health * 100 / MaxHealth);

        if (health <= 0)
        {
            //   explode();
        }
    }

    public int getHealth()
    {
        return health;
    }

    public void TakeDamage(int amount, Vector2 dir)
    {
        health -= amount;

        EmitSignal(nameof(HealthChangedSignal), health * 100 / MaxHealth);

        if (health < MaxHealth / 2)
        {
            Particles2D smoke = (Particles2D)GetNode("Smoke");
            smoke.Emitting = true;
        }

        // knock back effect
        if (MaxSpeed != 0)
        {
            MoveAndSlide(dir * 50 * amount);
        }
    }


    public void heal(int amount)
    {

        health = +amount;

        if (health > MaxHealth)
        {
            health = MaxHealth;
        }

        if (health >= MaxHealth / 2)
        {
            Particles2D smoke = (Particles2D)GetNode("Smoke");
            smoke.Emitting = false;
        }
    }

    public bool ammoIncrease(Weapon.WeaponAmmoType weaponAmmoType, int amount)
    {
        bool consume = false;

        if (weaponAmmoType != Weapon.WeaponAmmoType.machine_energy)
        {
            foreach (Weapon currentWeapon in primaryWeapons)
            {
                if (weaponAmmoType == currentWeapon.weaponAmmoType)
                {
                    consume = true;
                    ((Weapon)primaryWeapons[currentPrimaryWeaponIndex]).ammoIncrease(amount);
                }
            }
        }
        else
        {
            energy = +amount;

            if (energy > MaxEnergy)
            {
                energy = MaxEnergy;
            }
        }

        return consume;
    }

    private void explode()
    {
        CollisionShape2D collisionShape2D = (CollisionShape2D)GetNode("CollisionShape2D");
        collisionShape2D.Disabled = true;
        Alive = false;
        Sprite body = (Sprite)GetNode("Body");
        body.Hide();
        Sprite turret = (Sprite)GetNode("Turret");
        turret.Hide();

        AnimatedSprite animatedSprite = (AnimatedSprite)GetNode("Explosion");
        animatedSprite.Show();
        animatedSprite.Play("fire");

        AudioManager audioManager = (AudioManager)GetNode("/root/AUDIOMANAGER");
        audioManager.playSoundEffect(musicClip);
    }

    private void _OnExplosionAnimationFinished()
    {
        QueueFree();
        EmitSignal(nameof(DeadSignal));
    }

}
