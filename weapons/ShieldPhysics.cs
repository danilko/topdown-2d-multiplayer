using Godot;
using System;

public class ShieldPhysics : StaticBody2D
{
    private Shield _shield;
    public override void _Ready()
    {
    }
    public void Initialize(Shield shield)
    {
        _shield = shield;
    }

    public void TakeShieldDamage(int damage)
    {
        if (IsInstanceValid(_shield))
        {
            _shield.TakeShieldDamage(damage);
            _updateTransform();
        }
    }

    public void OnShieldAreaEntered(Area2D body)
    {
        if (IsInstanceValid(_shield))
        {
            _shield.OnShieldAreaEntered(body);
        }
    }

    public void _updateTransform()
    {
        if (IsInstanceValid(_shield))
        {
            GlobalPosition = _shield.GlobalPosition;
            GlobalRotation = _shield.GlobalRotation;
        }

    }
    public override void _PhysicsProcess(float delta)
    {
        _updateTransform();
    }
}

