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
    int Speed;

    [Export]
    public int Damage;

    [Export]
    float Lifetime;

    [Export]
    float steer_force = 0;

    Node2D target = null;

    protected Node2D Source = null;

    protected Team SourceTeam;

    protected Vector2 Velocity;
    private Vector2 acceleration;

    // https://gamesounds.xyz/?dir=FXHome
    private AudioStream _musicClip = (AudioStream)GD.Load("res://assets/sounds/Future Weapons 2 - Energy Gun - shot_single_2.wav");
    private AudioStream _musicHitClip = (AudioStream)GD.Load("res://assets/sounds/Bullet Impact 22.wav");

    private bool isProjectileStart = false;

    protected GameWorld GameWorld;

    public void Initialize(Vector2 position, Vector2 direction, Node2D inSource, Team sourceTeam, Node2D inTarget)
    {
        GameWorld = (GameWorld)GetParent();
        Connect(nameof(ProjectileDamageSignal), GameWorld, "_onDamageCalculation");

        GlobalPosition = position;

        Rotation = direction.Angle();
        Velocity = direction * Speed;

        acceleration = new Vector2();

        target = inTarget;
        Source = inSource;
        SourceTeam = sourceTeam;

        Timer timer = (Timer)GetNode("Lifetime");
        timer.WaitTime = Lifetime;
        timer.Start();

        isProjectileStart = true;
        Enabled = isProjectileStart;

        AudioManager audioManager = (AudioManager)GetNode("/root/AUDIOMANAGER");
        audioManager.playSoundEffect(_musicClip);

    }

    public Team.TeamCode GetTeam()
    {
        return SourceTeam.CurrentTeamCode;
    }

    private Vector2 seek()
    {
        Vector2 desired = (target.Position - Position).Normalized() * Speed;
        Vector2 steer = (desired - Velocity).Normalized() * steer_force;
        return steer;
    }

    public override void _PhysicsProcess(float delta)
    {
        if (isProjectileStart)
        {
            // Validate if target is available or is freed up (maybe no longer in scene)
            if (target != null && IsInstanceValid(target))
            {
                target = null;
            }

            if (target != null)
            {
                acceleration += seek();
                Velocity += acceleration * delta;
                Rotation = Velocity.Angle();
            }

            if (IsColliding())
            {
                isProjectileStart = false;
                Enabled = isProjectileStart;

                ComputeDamage();

                Explode();

                AudioManager audioManager = (AudioManager)GetNode("/root/AUDIOMANAGER");
                audioManager.playSoundEffect(_musicHitClip);
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

    public void Explode()
    {
        isProjectileStart = false;
        Enabled = isProjectileStart;

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
