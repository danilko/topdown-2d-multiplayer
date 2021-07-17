using Godot;
using System;

// Concept from following
// from https://www.fiverr.com/nathanwfranke
// from https://www.fiverr.com/nonunknown
public class Projectile : RayCast2D
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
    protected  AudioStream MusicHitClip = (AudioStream)GD.Load("res://assets/sounds/Bullet Impact 22.wav");

    protected bool IsProjectileStart = false;

    protected GameWorld GameWorld;


    public virtual void Initialize(Vector2 position, Vector2 direction, Node2D inSource, Team sourceTeam, Node2D inTarget, Vector2 defaultTargetPosition)
    {
        GameWorld = (GameWorld)GetParent();
        Connect(nameof(ProjectileDamageSignal), GameWorld, "_onDamageCalculation");

        GlobalPosition = position;

        Rotation = direction.Angle();
        Velocity = direction * Speed;

        Acceleration = new Vector2();

        Target = inTarget;

        Source = inSource;
        SourceTeam = sourceTeam;

        Timer timer = (Timer)GetNode("Lifetime");
        timer.WaitTime = Lifetime;
        timer.Start();

        IsProjectileStart = true;
        Enabled = IsProjectileStart;

        AudioManager audioManager = (AudioManager)GetNode("/root/AUDIOMANAGER");
        audioManager.playSoundEffect(_musicClip);

    }

    public Team.TeamCode GetTeam()
    {
        return SourceTeam.CurrentTeamCode;
    }

    protected virtual Vector2 Seek()
    {
        Vector2 desired = (Target.Position - Position).Normalized() * Speed;
        Vector2 steer = (desired - Velocity).Normalized() * SteerForce;

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

            if (Target != null)
            {
                Acceleration += Seek();
                Velocity += Acceleration * delta;
                Rotation = Velocity.Angle();
            }

            if (IsColliding())
            {
                IsProjectileStart = false;
                Enabled = IsProjectileStart;

                ComputeDamage();

                Explode();

                AudioManager audioManager = (AudioManager)GetNode("/root/AUDIOMANAGER");
                audioManager.playSoundEffect(MusicHitClip);
            }

            GlobalPosition += Transform.x * Speed * delta;
            CastTo = Vector2.Right * 2.0f * Speed * delta;
        }
    }

    protected virtual void ComputeDamage()
    {
        // This is the code responsible for able to shoot down bullet with bullet
        EmitSignal(nameof(ProjectileDamageSignal), Damage, GetCollisionNormal(), Source, SourceTeam, GetCollider());
    }

    public virtual void Explode()
    {
        IsProjectileStart = false;
        Enabled = IsProjectileStart;

        Velocity = new Vector2();
        Sprite sprite = (Sprite)GetNode("Sprite");
        sprite.Hide();
        AnimatedSprite explosion = (AnimatedSprite)GetNode("Explosion");
        explosion.Show();
        explosion.Play("smoke");

    }

    private void _onLifetimeTimeout()
    {
        Explode();
    }

    private void _OnExplosionAnimationFinished()
    {
        QueueFree();
    }
}
