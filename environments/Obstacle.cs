using Godot;
using System;

public class Obstacle : StaticBody2D
{
    [Signal]
    public delegate void ObstacleDestroySignal();

    public enum Items
    {
        crate_wood,
        crate_steel,
        cross_block_wood,
        roadblook_yellow,
        roadblock_red,
        remain
    }

    private Rect2[] itemRegions = {
    new Rect2(750.402f, 110.314f, 56.327f, 55.5f),
    new Rect2(989.709f, 112.098f, 56.327f, 56.077f),
    new Rect2(1045.898f, 112.348f, 55.97f, 55.827f),
    new Rect2(1042.576f, 810.563f, 31.155f, 103.693f),
    new Rect2(1075.159f, 818.412f, 29.966f, 95.844f),
    new Rect2(1075.159f, 818.412f, 29.966f, 95.844f),
    new Rect2(1046.708f, 527.378f, 45.187f, 45.187f)};

    private int[] itemHealth = {
    50,
    80,
    30,
    10,
    10,
    0};

    [Export]
    public Items type = Items.crate_wood;

    protected AudioStream explosionMusicClip = (AudioStream)GD.Load("res://assets/sounds/explosion_large_07.wav");

    private int _health;
    private int _maxHealth;

    public override void _Ready()
    {
        Sprite icon = (Sprite)GetNode("Icon");
        icon.RegionRect = itemRegions[(int)type];
        _maxHealth = itemHealth[(int)type];
        _health = itemHealth[(int)type];
    }

    public void TakeEnvironmentDamage(int amount)
    {
        _health -= amount;

        if (_health < 0)
        {
            EmitSignal(nameof(Obstacle.ObstacleDestroySignal), Name);
        }

        if (_health <= _maxHealth / 2)
        {
            Particles2D smoke = (Particles2D)GetNode("Smoke");
            smoke.Emitting = true;
        }
    }

    public void explode()
    {
        CollisionShape2D collisionShape2D = (CollisionShape2D)GetNode("CollisionShape2D");
        collisionShape2D.CallDeferred("set", "disabled", true);

        Sprite icon = (Sprite)GetNode("Icon");
        icon.Hide();

        AnimatedSprite animatedSprite = (AnimatedSprite)GetNode("Explosion");
        animatedSprite.Show();
        animatedSprite.Play("smoke");

        RemainParticles remainParticles = (RemainParticles)((PackedScene)GD.Load("res://effects/RemainParticles.tscn")).Instance();
        remainParticles.GlobalPosition = this.GlobalPosition;
        GetParent().GetParent().GetNode("RemainEffectManager").AddChild(remainParticles);

        AudioManager audioManager = (AudioManager)GetNode("/root/AUDIOMANAGER");
        audioManager.playSoundEffect(explosionMusicClip);
    }

    private void _OnExplosionAnimationFinished()
    {
        QueueFree();
    }
}
