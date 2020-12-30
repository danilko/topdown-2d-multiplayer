using Godot;
using System;

public class Projectile : Area2D
{
    [Signal]
    public delegate void ProjectileDamageSignal();

    [Export]
    int Speed;

    [Export]
    int Damage;

    [Export]
    float Lifetime;

    [Export]
    float steer_force = 0;

    Node2D target = null;

    Node2D source = null;

    private Team _sourceTeam;

    protected Vector2 _velocity;
    Vector2 acceleration;

    // https://gamesounds.xyz/?dir=FXHome
    AudioStream musicClip = (AudioStream)GD.Load("res://assets/sounds/Future Weapons 2 - Energy Gun - shot_single_2.wav");
    AudioStream musicHitClip = (AudioStream)GD.Load("res://assets/sounds/Bullet Impact 22.wav");

    public void Initialize(Vector2 position, Vector2 direction, Node2D inSource, Team sourceTeam, Node2D inTarget)
    {
        Connect(nameof(ProjectileDamageSignal), GetParent(), "_onDamageCalculation");
        
        GlobalPosition = position;

        Rotation = direction.Angle();
        _velocity = direction * Speed;

        acceleration = new Vector2();

        target = inTarget;
        source = inSource;
        _sourceTeam = sourceTeam;

        Timer timer = (Timer)GetNode("Lifetime");
        timer.WaitTime = Lifetime;
        timer.Start();


        AudioManager audioManager = (AudioManager)GetNode("/root/AUDIOMANAGER");
        audioManager.playSoundEffect(musicClip);

    }

    private Vector2 seek()
    {
        Vector2 desired = (target.Position - Position).Normalized() * Speed;
        Vector2 steer = (desired - _velocity).Normalized() * steer_force;
        return steer;
    }

    public override void _Process(float delta)
    {
        // Validate if target is available or is freed up (maybe no longer in scene)
        if (target != null && IsInstanceValid(target))
        {
            target = null;
        }

        if (target != null)
        {
            acceleration += seek();
            _velocity += acceleration * delta;
            Rotation = _velocity.Angle();
        }
        Position = Position + _velocity * delta;
    }


    private void explode()
    {

        _velocity = new Vector2();
        Sprite sprite = (Sprite)GetNode("Sprite");
        sprite.Hide();
        AnimatedSprite explosion = (AnimatedSprite)GetNode("Explosion");
        explosion.Show();
        explosion.Play("smoke");


    }

    private void _onProjectileBodyEntered(Node2D body)
    {
        // This is the code responsible for able to shoot down bullet with bullet
        Vector2 hitDir = _velocity.Normalized();
        explode();

        AudioManager audioManager = (AudioManager)GetNode("/root/AUDIOMANAGER");
        audioManager.playSoundEffect(musicHitClip);
        EmitSignal(nameof(ProjectileDamageSignal), Damage, hitDir, source, _sourceTeam, body);
    }

    private void _onProjectileAreaEntered(Area2D body)
    {
        // Projectile will collide
         if (body.HasMethod("_onProjectileAreaEntered")){
            explode();
         }
    }

    private void _onLifetimeTimeout()
    {
        explode();
    }

    private void _OnExplosionAnimationFinished()
    {
        QueueFree();
    }
}
