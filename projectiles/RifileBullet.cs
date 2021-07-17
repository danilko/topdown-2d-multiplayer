using Godot;
using System;

public class RifileBullet : Projectile
{

    public override void _Ready()
    {
        base._Ready();
    }

    public override void _PhysicsProcess(float delta)
    {
        base._PhysicsProcess(delta);
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
