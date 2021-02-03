using Godot;
using System;

public class LightSaber : Weapon
{
    [Export]
    int Damage = 50;

    public override void _Ready()
    {
        if (MaxAmmo == 0)
        {
            MaxAmmo = -1;
        }

        Ammo = MaxAmmo;

        CooldownTimer = (Timer)GetNode("CooldownTimer");
        CooldownTimer.WaitTime = 1;

        ReloadTimer = (Timer)GetNode("ReloadTimer");
        ReloadTimer.WaitTime = 1;

        EmitSignal(nameof(AmmoChangeSignal), Ammo, MaxAmmo);
    }

    public override bool Fire(Agent targetAgent)
    {
        if (Cooldown)
        {
            Cooldown = false;
            AnimationPlayer animationPlayer = (AnimationPlayer)GetNode("AnimationPlayer");
            animationPlayer.Play("Attack");
        }

        return false;
    }

    private void _onAnimationFinished(String animationName)
    {
        Cooldown = true;
    }

    private void _onCollisionEntered(Node2D body)
    {
     
        // Projectile will collide
        if (body.HasMethod("_onProjectileAreaEntered"))
        {
            Projectile projectile = (Projectile)body;
            // Only bullets from different team will cloide
            if (projectile.GetTeam() != _team.CurrentTeamCode)
            {
                projectile.Explode();
            }
        }

        if (body.HasMethod("TakeEnvironmentDamage"))
        {
            Obstacle obstacle = (Obstacle)body;
            obstacle.TakeEnvironmentDamage(Damage);

        }

        if (body.HasMethod("TakeDamage"))
        {
            Agent agent = (Agent)body;
            // Only bullets from different team will cloide
            if (agent.GetCurrentTeam() != _team.CurrentTeamCode)
            {
                agent.TakeDamage(Damage, (agent.GlobalPosition - _agent.GlobalPosition).Normalized(), _agent, _team);
            }
        }
    }

}