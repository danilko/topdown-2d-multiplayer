using Godot;
using System;

public class ExplosionBlast : Area2D
{

    [Signal]
    public delegate void ExplosionBlastDamageSignal();

    protected Team SourceTeam;
    protected float Damage;
    protected Node2D Source;

    protected bool IsInitialize;

    public override void _Ready()
    {
        base._Ready();
        IsInitialize = false;
    }

    public Team.TeamCode GetTeam()
    {
        return SourceTeam.CurrentTeamCode;
    }

    public void OnExplosionAnimationFinished()
    {
        // Clean up parent, which will also clean up this
        QueueFree();
    }

    public void Initialize(Node2D source, Team sourceTeam, float raidus, float damage, Vector2 position)
    {
        CollisionShape2D detectRadius = (CollisionShape2D)GetNode("CollisionShape2D");

        // Use SetDeferred to change the pyhsic world when it is ready
        // reference from here https://github.com/godotengine/godot/issues/33766
        ((CircleShape2D)(detectRadius.Shape)).SetDeferred("radius", raidus);

        Source = source;
        SourceTeam = sourceTeam;
        Damage = damage;

        GlobalPosition = position;

        AgentExplosionParticle agentExplosionParticle = (AgentExplosionParticle)GetNode("AgentExplosionParticle");
        agentExplosionParticle.SetTrigger(true);

        IsInitialize = true;
    }

    public virtual void OnNodeEntered(Node body)
    {
        if (!IsInitialize)
        {
            return;
        }

        if (body.HasMethod("GetTeam"))
        {
            // Ignore projectile for now
            if (body is Projectile)
            {
                // Blast ignore projectile for now
                return;
            }


            if (body is ExplosionBlast && ((ExplosionBlast)body).GetTeam() == GetTeam())
            {
                // This is from same team, ignore it
                return;
            }


            if (body is ShieldPhysics && ((ShieldPhysics)body).GetTeam() == GetTeam())
            {
                // This is from same team, ignore it
                return;
            }

            if (body is Agent && ((Agent)body).GetTeam() == GetTeam())
            {
                // This is from same team, ignore it
                return;
            }
        }

        EmitSignal(nameof(ExplosionBlastDamageSignal), Damage, (((Node2D)body).GlobalPosition - GlobalPosition).Normalized(), Source, SourceTeam, body);
    }

}
