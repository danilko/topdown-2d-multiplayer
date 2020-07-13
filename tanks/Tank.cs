using Godot;
using System;

public class Tank : KinematicBody2D
{


    [Signal]
    public delegate void HealthChangedSignal();

    [Signal]
    public delegate void DeadSignal();

    [Signal]
    public delegate void ShootSingal();


    [Signal]
    public delegate void AmmoChangedSignal();


    [Export]
    protected PackedScene Bullet;

    [Export]
    protected int MaxSpeed;

    [Export]
    protected float RotationSpeed;

    [Export]
    protected float GunCooldown;

    [Export]
    protected int MaxHealth;

    [Export]
    protected int MaxAmmo = 20;

    [Export]
    protected int Ammo = -1;

    [Export]
    protected int GunShot = 1;

    [Export]
    protected float GunSpread;

    protected Vector2 Velocity;
    protected Boolean CanShoot = true;
    protected Boolean Alive = true;

    public float currentTime = 0;


    [Export]
    private String unitName = "Default";

    int max_dist = 2000;

    private int health;

    protected AudioStream musicClip = (AudioStream)GD.Load("res://assets/sounds/explosion_large_07.wav");
    protected AudioStream moveMusicClip = (AudioStream)GD.Load("res://assets/sounds/sci-fi_device_item_power_up_flash_01.wav");

    protected GameStates gameStates;
    protected Network network;

    private String teamIdentifier = "UNKOWN";

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        gameStates = (GameStates)GetNode("/root/GAMESTATES");
        network = (Network)GetNode("/root/NETWORK");

        Timer timer = (Timer)GetNode("GunTimer");
        timer.WaitTime = GunCooldown;
        health = MaxHealth;

        Particles2D smoke = (Particles2D)GetNode("Smoke");
        smoke.Emitting = false;

        EmitSignal(nameof(HealthChangedSignal), health * 100 / MaxHealth);
        EmitSignal(nameof(AmmoChangedSignal), Ammo * 100 / MaxAmmo);

        Label label = (Label)(GetNode("UnitDisplay/Name"));
        label.Text = this.unitName;
    }

    public void setTeamIdentifier(String inputTeamIdentifier)
    {
        teamIdentifier = inputTeamIdentifier;
    }

    public String getTeamIdentifier()
    {
        return teamIdentifier;
    }

    public void setUnitName(String unitName)
    {
        this.unitName = unitName;
        Label label = (Label)(GetNode("UnitDisplay/Name"));
        label.Text = this.unitName;
    }

    public String getUnitName()
    {
        return unitName;
    }

    public virtual void _Control(float delta) { }

    public virtual void _shootSecondary() { }


    public void handleBeam()
    {
        Node2D laser = (Node2D)GetNode("Turret/Laser");
        if (laser != null)
        {
            Physics2DDirectSpaceState ray = GetWorld2d().DirectSpaceState;
            Node2D muzzle = (Node2D)GetNode("Turret/Muzzle");
            Godot.Collections.Dictionary hit = ray.IntersectRay(muzzle.GlobalPosition, muzzle.GlobalPosition + Transform.x * max_dist, new Godot.Collections.Array() { this }, 1, true, true);

            if (hit.Count > 0)
            {
                Vector2 hit_position = (Vector2)hit["position"];

                float laserLength = laser.GlobalPosition.DistanceTo(hit_position);
                Vector2 laserScale = laser.Scale;
                laserScale.x = laserLength;
                laser.Scale = laserScale;

                // TODO: FIX TO FIND THE CORRECT OBJECT
            //    Node body = GetNode("/root/Map/" + hit["collider_id"]);

              //  if (body != null && body.HasMethod("TakeDamage"))
             //   {
             //       Tank tank = (Tank)(body);
             //       tank.TakeDamage(5, Position - hit_position);
             //   }

            }
            else
            {
                Vector2 laserScale = laser.Scale;
                laserScale.x = max_dist;
                laser.Scale = laserScale;
            }
        }
    }

    public void cleanBeam()
    {
        Node2D laser = (Node2D)GetNode("Turret/Laser");
        if (laser != null)
        {
            Vector2 laserScale = laser.Scale;
            laserScale.x = 0;
            laser.Scale = laserScale;
        }
    }


    public virtual void _shoot(int num, float spread, Node2D target = null)
    {
        if (CanShoot && Ammo != 0)
        {
            CanShoot = false;
            Ammo -= 1;
            EmitSignal(nameof(AmmoChangedSignal), Ammo * 100 / MaxAmmo);

            Timer timer = (Timer)GetNode("GunTimer");
            timer.Start();
            Sprite turret = (Sprite)GetNode("Turret");

            Vector2 dir = (new Vector2(1, 0)).Rotated(turret.GlobalRotation);

            Position2D muzzle = (Position2D)GetNode("Turret/Muzzle");
            if (num > 1)
            {
                for (int i = 0; i < num; i++)
                {
                    float a = -spread + i * (2 * spread) / (num - 1);
                    EmitSignal(nameof(ShootSingal), Bullet, muzzle.GlobalPosition, dir.Rotated(a), target);
                }
            }
            else
            {
                EmitSignal(nameof(ShootSingal), Bullet, muzzle.GlobalPosition, dir, target);
            }

            AnimationPlayer animationPlayer = (AnimationPlayer)GetNode("AnimationPlayer");
            animationPlayer.Play("muzzle_flash");

            // knock back effect
            if (MaxSpeed != 0)
            {
                MoveAndSlide(dir * -500);
            }

        }
    }


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
           // AudioManager audioManager = (AudioManager)GetNode("/root/AUDIOMANAGER");
           //audioManager.playSoundEffect(moveMusicClip);
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
        if (primaryWeapon)
        {
            _shoot(GunShot, GunSpread, null);
        }

        if (secondaryWeapon)
        {
            handleBeam();
        }
        else
        {
            cleanBeam();
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

    public void ammoIncrease(int amount)
    {

        Ammo = +amount;

        if (Ammo > MaxAmmo)
        {
            Ammo = MaxAmmo;
        }

        EmitSignal(nameof(AmmoChangedSignal), Ammo * 100 / MaxAmmo);
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

    public void _on_GunTimerTimeout()
    {
        CanShoot = true;
        Timer timer = (Timer)GetNode("GunTimer");
    }

    private void _OnExplosionAnimationFinished()
    {
        QueueFree();
        EmitSignal(nameof(DeadSignal));
    }

}
