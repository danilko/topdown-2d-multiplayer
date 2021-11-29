using Godot;
using System;

public class ProjectileManager : Node2D
{
    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
    }

    private void _onProjectileShoot(PackedScene projectile, Vector2 _position, Vector2 _direction, Node2D source, Team sourceTeam, Node2D target, Vector2 defaultTarget)
    {
        Projectile newProjectile = (Projectile)projectile.Instance();
        AddChild(newProjectile);
        newProjectile.Initialize(_position, _direction, source, sourceTeam, target, defaultTarget);
    }

    private void _onDamageCalculation(int damage, Vector2 hitDir, Godot.Object source, Team sourceTeam, Godot.Object target)
    {
        if (target != null && IsInstanceValid(target))
        {

            if (target is Agent)
            {
                Agent targetAgent = (Agent)(target);
                Agent sourceAgent = (Agent)(source);
                targetAgent.TakeDamage(damage, hitDir, sourceAgent, sourceTeam);
            }
            else if (target is Obstacle)
            {
                ((Obstacle)(target)).TakeEnvironmentDamage(damage);
            }
            else if (target is ShieldPhysics)
            {
                ((ShieldPhysics)(target)).TakeShieldDamage(damage);
            }
        }
    }
}
