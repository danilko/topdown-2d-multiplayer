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

    public override bool Fire(Agent targetAgent) { return true; }

    private void _onShieldBodyEntered(Node2D body)
    {
    }

    private void _toggleShield(Boolean toggle)
    {
        _collisionShape2D.SetDeferred("disabled", !toggle);
        _effect.Visible = toggle;
    }

    private void _shieldStatusChange(int ammo, int maxAmmo, Weapon.WeaponOrder weaponOrder)
    {
        if (ammo == 0)
        {
            if (!_collisionShape2D.Disabled)
            {
                _toggleShield(false);
            }
        }
        else
        {
            if (_collisionShape2D.Disabled)
            {
                _toggleShield(true);
            }
        }
    }

    public void TakeShieldDamage(int damage)
    {
        Ammo -= damage;
        Ammo = Mathf.Clamp(Ammo, 0, MaxAmmo);
        SetAmmo(Ammo);
        if (Ammo == 0)
        {
            EmitSignal(nameof(AmmoOutSignal), GetWeaponOrder());

            // Auto reload
            StartReload();
        }
    }

    public void OnShieldAreaEntered(Area2D body)
    {
        // Projectile will collide
        if (body.HasMethod("_onProjectileAreaEntered"))
        {
            Projectile projectile = (Projectile)body;
            Ammo -= projectile.Damage;
            TakeShieldDamage(projectile.Damage);
            projectile.Explode();
        }
    }
}
