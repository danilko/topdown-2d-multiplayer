using Godot;
using System;

public class ProjectileManager : Node
{
    private GameWorld _gameWorld;

    public override void _Ready()
    {
    }

   public void Initailize(GameWorld gameWorld)
    {
        _gameWorld = gameWorld;
    }


    private void _onProjectileShoot(PackedScene projectile, Vector2 _position, Vector2 _direction, Node2D source, Team sourceTeam, Node2D target, Vector2 defaultTarget)
    {
        Projectile newProjectile = (Projectile)projectile.Instance();
        _gameWorld.AddChild(newProjectile);

        newProjectile.Connect(nameof(Projectile.ProjectileDamageSignal), this, nameof(_onDamageCalculation));
        newProjectile.Connect(nameof(Projectile.ProjectileExplosionSignal), this, nameof(_onProjectileExplosion));

        newProjectile.Initialize( _position, _direction, source, sourceTeam, target, defaultTarget);
    }

    private void _onProjectileExplosion(Node2D source, Team sourceTeam, float raidus, float damage, Vector2 position)
    {
        ExplosionBlast explosionBlast = (ExplosionBlast)((PackedScene)GD.Load("res://projectiles/ExplosionBlast.tscn")).Instance();
        AddChild(explosionBlast);

        // Set the parent to gameworld
        explosionBlast.Connect(nameof(ExplosionBlast.ExplosionBlastDamageSignal), this, nameof(_onDamageCalculation));

        explosionBlast.Initialize(source, sourceTeam, raidus, damage, position);
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
