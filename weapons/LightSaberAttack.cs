using Godot;
using System;

public class LightSaberAttack : Area2D
{
    protected int Damage;
    protected Agent Agent;
    protected Team SourceTeam;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        SourceTeam = new Team();
    }

    public void Initialize(int damage, Agent agent)
    {
        Damage = damage;
        Agent = agent;
        SourceTeam.CurrentTeamCode = agent.GetTeam();
    }

    public Team.TeamCode GetTeam()
    {
        return SourceTeam.CurrentTeamCode;
    }

   private void _onCollisionEntered(Node2D body)
    {
        // No need to calculate as the instance is already in middle of clean up
        if(body == null || ! IsInstanceValid(body))
        {
            return;
        }

        if (body is Projectile && ((Projectile)body).GetTeam() != GetTeam())
        {
            ((Projectile)body).Explode();
        }

        if (body is Obstacle)
        {
            Obstacle obstacle = (Obstacle)body;
            obstacle.TakeEnvironmentDamage(Damage);
        }

        if (body is RemoteWeapon)
        {
            RemoteWeapon remoteWeapon = (RemoteWeapon)body;

            // Only bullets from different team will cloide
            if (remoteWeapon.GetTeam() != SourceTeam.CurrentTeamCode)
            {
                remoteWeapon.TakeDamage(Damage, (remoteWeapon.GlobalPosition - Agent.GlobalPosition).Normalized(), Agent, SourceTeam);
            }
        }

        if (body is Agent)
        {
            Agent agent = (Agent)body;

            // Only bullets from different team will cloide
            if (agent.GetTeam() != SourceTeam.CurrentTeamCode)
            {
                agent.TakeDamage(Damage, (agent.GlobalPosition - Agent.GlobalPosition).Normalized(), Agent, SourceTeam);
            }
        }
    }
}
