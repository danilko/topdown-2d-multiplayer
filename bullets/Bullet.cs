using Godot;
using System;

public class Bullet : Area2D
{
   [Export]
   int Speed;

   [Export]
   int Damage;

   [Export]
   float Lifetime;

   [Export]
   float steer_force = 0;

   Node2D target = null;

   Vector2 velocity;
   Vector2 acceleration;

// https://gamesounds.xyz/?dir=FXHome
AudioStream musicClip = (AudioStream)GD.Load("res://assets/sounds/Future Weapons 2 - Energy Gun - shot_single_2.wav");
AudioStream musicHitClip = (AudioStream)GD.Load("res://assets/sounds/Bullet Impact 22.wav");

   public void start(Vector2 position, Vector2 direction, Node2D inTarget)
   {
       Position = position;
       Rotation = direction.Angle();
       velocity = direction * Speed;

       acceleration = new Vector2();
       
       target = inTarget;

       Timer timer = (Timer)GetNode("Lifetime");
       timer.WaitTime = Lifetime;
       timer.Start();


       AudioManager audioManager = (AudioManager)GetNode("/root/AUDIOMANAGER");
       audioManager.playSoundEffect(musicClip);

   }

    private Vector2 seek(){
        Vector2 desired = (target.Position - Position).Normalized() * Speed;
        Vector2 steer = (desired - velocity).Normalized() * steer_force;
        return steer;
    }

   public override void _Process(float delta)
   {
       if (target != null){
           acceleration += seek();
            velocity += acceleration * delta;
            Rotation = velocity.Angle();
       }
       Position = Position + velocity * delta;
    
   }

       public override void _Ready()
    {
    }

private void explode(){

velocity = new Vector2();
Sprite sprite = (Sprite)GetNode("Sprite");
sprite.Hide();
AnimatedSprite explosion = (AnimatedSprite) GetNode("Explosion");
explosion.Show();
explosion.Play("smoke");


}

    private void _on_Bullet_body_entered(Node2D body)
    {
        // Need to see how to avoid missle hit missle from enemy during spread sheet
        // This is the code responsible for able to shoot down bullet with bullet
       Vector2 hitDir = velocity.Normalized();
       explode();

       AudioManager audioManager = (AudioManager)GetNode("/root/AUDIOMANAGER");
       audioManager.playSoundEffect(musicHitClip);
        if (body.HasMethod("TakeDamage")){
            Tank tank = (Tank)(body);
            tank.TakeDamage(Damage, hitDir);
        }
 
    }

    private void _onBulletAreaEntered(Area2D body)
    {
     //   if (body.HasMethod("TakeBulletDamage")){
     //     explode();
     //   }
    }

  	public void TakeBulletDamage(int amount)
	{
    }

    private void _on_Lifetime_timeout()
    {
        explode();
    }

    private void _OnExplosionAnimationFinished()
    {
QueueFree();
    }
}
