using Godot;
using System;

// Reference base on LegionGames https://www.youtube.com/watch?v=xPk9cxHrPrU

public class ExplosionRay : RayCast2D
{
    [Signal]
    public delegate void RayDamageSignal();

    private int _damage;
    private Node2D _source;
    private Team _sourceTeam;

    public void Initialize(int damage, Node2D source, Team sourceTeam, GameWorld gameWorld)
    {
        _damage = damage;
        _source = source;
        _sourceTeam = sourceTeam;
        Enabled = true;

         // Set the parent to player
        if (!IsConnected(nameof(RayDamageSignal), gameWorld, "_onDamageCalculation"))
        {
            Connect(nameof(RayDamageSignal), gameWorld, "_onDamageCalculation");
        }
    }

    public override void _PhysicsProcess(float delta)
    {
        if (Enabled && IsColliding())
        {
            // Projectile will collide
            if (GetCollider() is Projectile)
            {
                Projectile collider = (Projectile)(GetCollider());

                // Only bullets from different team will cloide
                if (collider.GetTeam() != _sourceTeam.CurrentTeamCode)
                {
                    collider.Explode();
                }
            }
            // shield will collide
            else if (GetCollider().HasMethod("_onShieldAreaEntered"))
            {
                ShieldPhysics shieldPhysics = (ShieldPhysics)GetCollider();
                shieldPhysics.TakeShieldDamage(_damage);
            }
            else
            {
                EmitSignal(nameof(RayDamageSignal), _damage, GetCollisionNormal() * -1, _source, _sourceTeam, GetCollider());
            }
        }
    }
}
