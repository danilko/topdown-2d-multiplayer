using Godot;
using System;

public class LaserGun : Weapon
{

    [Export]

    protected int MaxRange = 2000;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        base._Ready();
    }

    public void drawBeam()
    {
        Node2D laser = (Node2D)GetNode("Laser");
        if (laser != null)
        {
            Physics2DDirectSpaceState ray = GetWorld2d().DirectSpaceState;
            Node2D triggerPoint = (Node2D)GetNode("TriggerPoint");
            Godot.Collections.Dictionary hit = ray.IntersectRay(triggerPoint.GlobalPosition, triggerPoint.GlobalPosition + Transform.x * MaxRange, new Godot.Collections.Array() { this }, 1, true, true);

            if (hit.Count > 0)
            {
                Vector2 hit_position = (Vector2)hit["position"];

                float laserLength = laser.GlobalPosition.DistanceTo(hit_position);
                Vector2 laserScale = laser.Scale;
                laserScale.x = laserLength;
                laser.Scale = laserScale;

                // TODO: FIX TO FIND THE CORRECT OBJECT
                //    Node body = GetNode("/root/Map/" + hit["collider_id"]);

                //  if (body != null && body.HasMethod("TakeDamage"))
                //   {
                //       Tank tank = (Tank)(body);
                //       tank.TakeDamage(5, Position - hit_position);
                //   }

            }
            else
            {
                Vector2 laserScale = laser.Scale;
                laserScale.x = MaxRange;
                laser.Scale = laserScale;
            }
        }
    }

    public void clearBeam()
    {
        Node2D laser = (Node2D)GetNode("TriggerPoint");
        if (laser != null)
        {
            Vector2 laserScale = laser.Scale;
            laserScale.x = 0;
            laser.Scale = laserScale;
        }
    }

    public override void onWeaponTimerTimeout()
    {
        base.onWeaponTimerTimeout();
        clearBeam();
    }

    public override bool fire(Node2D source, Node2D target)
    {
        Timer timer = (Timer)GetNode("WeaponTimer");

        if (CanShoot && Ammo != 0)
        {
            CanShoot = false;
            Ammo -= 1;
            EmitSignal(nameof(AmmoChangedSignal), Ammo * 100 / MaxAmmo);

            drawBeam();

            timer.Start();

            return true;
        }

        if (!timer.IsStopped())
        {
            // Draw beam update base on location
            drawBeam();
        }

        return false;
    }
}
