using Godot;
using System;

public class ShieldPhysics : StaticBody2D
{
    private Shield _shield;
    private Team.TeamCode _teamCode;
    public override void _Ready()
    {
    }
    public void Initialize(Shield shield, Team.TeamCode teamCode)
    {
        _shield = shield;
        _teamCode = teamCode;
    }

    public Team.TeamCode GetTeam()
    {
        return _teamCode;
    }

    public void TakeShieldDamage(int damage)
    {
        if (IsInstanceValid(_shield))
        {
            _shield.TakeShieldDamage(damage);
            _updateTransform();
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

