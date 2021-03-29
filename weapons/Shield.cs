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
        _collisionShape2D = (CollisionShape2D)GetNode("StaticBody2D/CollisionShape2D");
        _effect = (Sprite)GetNode("Effect");
    }

    public override void Initialize(GameWorld gameWorld, Agent agent, WeaponOrder weaponOrder)
    {
        base.Initialize(gameWorld, agent, weaponOrder);

        PinJoint2D pinJoint2D = (PinJoint2D)(GetNode("StaticBody2D/PinJoint2D"));
        pinJoint2D.NodeA = agent.GetPath();

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
}
