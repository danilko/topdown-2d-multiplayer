using Godot;
using System;

public class Shield : Weapon
{
    [Export]
    int Damage = 50;

    private CollisionShape2D _collisionShape2D;
    private Sprite _effect;

    public override void _Ready()
    {
        base._Ready();
        _collisionShape2D = (CollisionShape2D)GetNode("Area2D/CollisionShape2D");
        _effect = (Sprite)GetNode("Effect");
    }

    public override void Initialize(GameWorld gameWorld, Agent agent, WeaponOrder weaponOrder)
    {
        base.Initialize(gameWorld, agent, weaponOrder);



    }

    public override bool Fire(Agent targetAgent)
    {
        if (Cooldown && Ammo != 0)
        {
            Cooldown = false;
            Ammo -= 1;
            EmitSignal(nameof(AmmoChangeSignal), Ammo, MaxAmmo, GetWeaponOrder());

            CooldownTimer.Start();

            Vector2 dir = (new Vector2(1, 0)).Rotated(GlobalRotation);

            Position2D triggerPoint = (Position2D)GetNode("TriggerPoint");

            _collisionShape2D.Disabled = false;
            _effect.Visible = true;

            CooldownTimer.Start();

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

    public override void onWeaponTimerTimeout()
    {
        if (!_collisionShape2D.Disabled)
        {
            Cooldown = true;
            _collisionShape2D.Disabled = true;
            _effect.Visible = false;
        }
    }

    private void _onProjectileBodyEntered(Node2D body)
    {
    }

    public void TakeShieldDamage(int damage)
    {
        Ammo -= damage;
        if (Ammo < 0)
        {
            Ammo = 0;
        }
    }

    private void _onProjectileAreaEntered(Area2D body)
    {
        // Projectile will collide
        if (body.HasMethod("_onProjectileAreaEntered"))
        {
            Projectile projectile = (Projectile)body;
            Ammo -= projectile.Damage;
            EmitSignal(nameof(AmmoChangeSignal), Ammo, MaxAmmo, GetWeaponOrder());
            projectile.Explode();
            if (Ammo < 0)
            {
                Ammo = 0;
                EmitSignal(nameof(AmmoChangeSignal), Ammo, MaxAmmo, GetWeaponOrder());
                _collisionShape2D.Disabled = true;
                CooldownTimer.Start();
            }
        }
    }
}
