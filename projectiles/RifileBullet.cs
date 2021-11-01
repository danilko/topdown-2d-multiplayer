using Godot;
using System;

public class RifileBullet : Projectile
{

    public override void _Ready()
    {
        base._Ready();
    }

    public override void Initialize(Vector2 position, Vector2 direction, Node2D inSource, Team sourceTeam, Node2D inTarget, Vector2 defaultTargetPosition)
    {
        // This weapon does not support tracking, so disable target
        base.Initialize(position, direction, inSource, sourceTeam, null, defaultTargetPosition);
    }


    public override void Explode()
    {
        base.Explode();
        
        ((Trail)GetNode("LineTrail")).Stop();

        Sprite sprite = (Sprite)GetNode("Sprite");
        sprite.Hide();
        AnimatedSprite explosion = (AnimatedSprite)GetNode("Explosion");
        explosion.Show();
        explosion.Play("smoke");
    }
}
