using Godot;
using System;

public class Shield : Weapon
{
    [Export]
    int Damage = 50;

    private CollisionShape2D _collisionShape2D = null;
    private Sprite _effect;
        private Sprite _body;
    private ShieldPhysics _shieldPhysics;
    protected Timer DamageEffectTimer;

    public override void _Ready()
    {
        base._Ready();
_body = (Sprite)GetNode("Sprite");
        _effect = (Sprite)GetNode("Effect");
        DamageEffectTimer = (Timer)GetNode("DamageEffectTimer");
    }

    public override void Initialize(GameWorld gameWorld, Agent agent, WeaponOrder weaponOrder)
    {
        _shieldPhysics = (ShieldPhysics)((PackedScene)GD.Load("res://weapons/ShieldPhysics.tscn")).Instance();
        gameWorld.AddChild(_shieldPhysics);

        _shieldPhysics.Initialize(this);
        _collisionShape2D = (CollisionShape2D)_shieldPhysics.GetNode("CollisionShape2D");

        base.Initialize(gameWorld, agent, weaponOrder);
    }

    public override void Deinitialize()
    {
        GetGameWorld().RemoveChild(_shieldPhysics);
        
        _shieldPhysics.QueueFree();
        base.Deinitialize();
    }
    
    public override bool Fire(Agent targetAgent) { return true; }

    private void _toggleShield(Boolean toggle)
    {
        _collisionShape2D.SetDeferred("disabled", !toggle);
        _effect.Visible = toggle;
    }

    public void DamageEffectTimerTimeout()
    {
        _body.SelfModulate = new Color(1.0f, 1.0f, 1.0f, 1.0f);
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

                    if (DamageEffectTimer.IsStopped())
            {
                _body.SelfModulate = new Color(5.0f, 5.0f, 5.0f, 1.0f);
                DamageEffectTimer.Start();
            }

        if (Ammo == 0)
        {
            EmitSignal(nameof(AmmoOutSignal), GetWeaponOrder());

            // Auto reload
            StartReload();
        }
    }
}
