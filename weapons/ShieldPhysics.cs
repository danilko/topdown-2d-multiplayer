using Godot;
using System;

public class ShieldPhysics : Area2D
{
    private Shield _shield;
    public override void _Ready()
    {
        _shield = (Shield)GetParent();
    }

    public void TakeShieldDamage(int damage)
    {
        _shield.TakeShieldDamage(damage);
    }

    public void OnShieldAreaEntered(Area2D body)
    {
        _shield.OnShieldAreaEntered(body);
    }
}
