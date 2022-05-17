using Godot;
using System;

public class Shield : Weapon
{
    [Export]
    protected int Damage = 50;

    [Export]
    protected bool Recoverable = false;

    private CollisionShape2D _collisionShape2D = null;
    private Sprite _effect;
    private Sprite _body;
    private ShieldPhysics _shieldPhysics;
    protected Timer DamageEffectTimer;
    private Particles2D _smoke;



    public override void _Ready()
    {
        base._Ready();
        _body = (Sprite)GetNode("Sprite");
        _effect = (Sprite)GetNode("Effect");
        DamageEffectTimer = (Timer)GetNode("DamageEffectTimer");
        _smoke = (Particles2D)GetNode("Smoke");
    }

    public override void Initialize(GameWorld gameWorld, Agent agent, WeaponOrder weaponOrder, int weaponIndex)
    {
        _shieldPhysics = (ShieldPhysics)((PackedScene)GD.Load("res://weapons/ShieldPhysics.tscn")).Instance();
        gameWorld.AddChild(_shieldPhysics);

        _shieldPhysics.Initialize(this, agent.GetTeam());
        _collisionShape2D = (CollisionShape2D)_shieldPhysics.GetNode("CollisionShape2D");

        base.Initialize(gameWorld, agent, weaponOrder, weaponIndex);
    }

    public override void Deinitialize()
    {
        GetGameWorld().RemoveChild(_shieldPhysics);

        _shieldPhysics.QueueFree();
        base.Deinitialize();
    }

    public override void EquipWeapon(bool equip)
    {
        base.EquipWeapon(equip);

        if(equip)
        {
            if (GetAmmo() > 0)
            {
                _toggleShield(true);
            }
        }
        else
        {
                _toggleShield(false);
        }
        
    }

    public override bool Fire(Agent targetAgent, Vector2 targetGlobalPosition)
    {
        if (Cooldown)
        {
            float moveYFloat = 25.0f;

            if (GetWeaponOrder() == WeaponOrder.Right)
            {
                moveYFloat = moveYFloat * -1.0f;
            }

            Position = new Vector2(0.0f, moveYFloat);
            Cooldown = false;
        }
        else
        {
            CooldownTimer.Stop();
        }

        CooldownTimer.Start();

        return true;
    }

    public override void  StartReload()
    {
            if (Recoverable)
            {

                base.StartReload();
            }
    }


    private void _toggleShield(Boolean toggle)
    {
        _collisionShape2D.SetDeferred("disabled", !toggle);
        _effect.Visible = toggle;
        _smoke.Visible = !toggle;
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
            if (Recoverable)
            {

                EmitSignal(nameof(AmmoOutSignal), GetWeaponOrder());

                // Auto reload
                StartReload();
            }
            else
            {

                AnimatedSprite animatedSprite = (AnimatedSprite)GetNode("Explosion");
                animatedSprite.Show();
                animatedSprite.Play("fire");


            }
        }
    }

    public void _OnExplosionAnimationFinished()
    {
        if (GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
        {
            Unequip();
        }

    }

    public override void onWeaponTimerTimeout()
    {
        Cooldown = true;

        Position = Vector2.Zero;

    }
}
