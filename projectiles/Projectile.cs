using Godot;
using System;

// Concept from following
// from https://www.fiverr.com/nathanwfranke
// from https://www.fiverr.com/nonunknown
public class Projectile : Area2D
{
    [Signal]
    public delegate void ProjectileDamageSignal();

    [Export]
    protected int Speed;

    [Export]
    protected int Damage;

    [Export]
    float Lifetime;

    [Export]
    protected float SteerForce = 0;

    protected Node2D Target = null;

    protected Node2D Source = null;

    protected Team SourceTeam;

    protected Vector2 Velocity;
    protected Vector2 Acceleration;

    // https://gamesounds.xyz/?dir=FXHome
    private AudioStream _musicClip = (AudioStream)GD.Load("res://assets/sounds/Future Weapons 2 - Energy Gun - shot_single_2.wav");
    protected AudioStream MusicHitClip = (AudioStream)GD.Load("res://assets/sounds/Bullet Impact 22.wav");

    protected bool IsProjectileStart = false;

    protected GameWorld GameWorld;


    public virtual void Initialize(Vector2 position, Vector2 direction, Node2D inSource, Team sourceTeam, Node2D inTarget, Vector2 defaultTargetPosition)
    {
        GameWorld = (GameWorld)GetParent();
        Connect(nameof(ProjectileDamageSignal), GameWorld, "_onDamageCalculation");

        GlobalPosition = position;

        Rotation = direction.Angle();

        Target = inTarget;

        Source = inSource;
        SourceTeam = sourceTeam;

        Timer timer = (Timer)GetNode("Lifetime");
        timer.WaitTime = Lifetime;
        timer.Start();

        IsProjectileStart = true;

        AudioManager audioManager = (AudioManager)GetNode("/root/AUDIOMANAGER");
        audioManager.playSoundEffect(_musicClip);

        Velocity = Transform.x * Speed;
        Acceleration = Vector2.Zero;
    }

    public Team.TeamCode GetTeam()
    {
        return SourceTeam.CurrentTeamCode;
    }

    protected virtual Vector2 Seek()
    {
        Vector2 steer = Vector2.Zero;

        if (Target != null && IsInstanceValid(Target))
        {
            Vector2 desired = (Target.Position - Position).Normalized() *  Speed;
            steer = (desired - Velocity).Normalized() * SteerForce;
        }
        else
        {
            Target = null;
        }

        return steer;
    }

    public override void _PhysicsProcess(float delta)
    {
        if (IsProjectileStart)
        {
            // Validate if target is available or is freed up (maybe no longer in scene)
            if (Target != null && !IsInstanceValid(Target))
            {
                Target = null;
            }
            
            if(Target!=null)
            {
                LookAt(Target.Position);
                Velocity = Transform.x * Speed;
            }

            //Acceleration += Seek();
            //Velocity += Acceleration * delta;

            

            Velocity = Velocity.Clamped(Speed);
            Rotation = Velocity.Angle();
            GlobalPosition += Velocity * delta;
        }
    }

    public virtual void OnNodeEntered(Node body)
    {
        if(IsProjectileStart)
        {
        if(body.HasMethod("GetTeam"))
        {
           if(body is ExplosionBlast)
           {
               // Ignore explosion blast for now
               return;
           }

           if(body is Projectile && ((Projectile)body).GetTeam() == GetTeam())
           {
               // This is from same team, ignore it
               return;
           }

           if(body is ShieldPhysics && ((ShieldPhysics)body).GetTeam() == GetTeam())
           {
               // This is from same team, ignore it
               return;
           }

           if(body is LightSaberAttack)
           {
               // Ignore light saber for now, this will be taken care of by Attack
               return;
           }


            if(body is Agent && ((Agent)body).GetTeam() == GetTeam())
           {
               // This is from same team, ignore it
               return;
           }
        }

        IsProjectileStart = false;

        ComputeDamage(body);

        Explode();
        }
    }


    protected virtual void ComputeDamage(Node body)
    {
        EmitSignal(nameof(ProjectileDamageSignal), Damage, Vector2.Right.Rotated(Mathf.Deg2Rad(GlobalRotation)), Source, SourceTeam, body);
    }


    public virtual void Explode()
    {
        IsProjectileStart = false;

        Velocity = Vector2.Zero;

        Sprite sprite = (Sprite)GetNode("Sprite");
        sprite.Hide();
    }

    private void _onLifetimeTimeout()
    {
        Explode();
    }

    protected void _OnExplosionAnimationFinished()
    {
        QueueFree();
    }
}
