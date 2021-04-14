using Godot;
using System;

public class ProjectileArea2D : Area2D
{
    Projectile projectile;

    public override void _Ready()
    {
        projectile = (Projectile)GetParent();
    }

    public Team.TeamCode GetTeam()
    {
        return projectile.GetTeam();
    }

    public void Explode()
    {
        projectile.Explode();
    }

    private void _onProjectileAreaEntered(Area2D body)
    {
        // Projectile will collide
        if (body.HasMethod("_onProjectileAreaEntered"))
        {
            ProjectileArea2D collider = (ProjectileArea2D)(body);

            // Only bullets from different team will cloide
            if (collider.GetTeam() != projectile.GetTeam())
            {
                collider.Explode();
            }
        }
    }

}
