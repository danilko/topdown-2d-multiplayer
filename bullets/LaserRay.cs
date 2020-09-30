using Godot;
using System;

public class LaserRay : RayCast2D
{
    [Signal]
    public delegate void RayDamageSignal();

    [Export]
    int Damage;

    bool isCasting;

    private Node2D source;

    Tween tween;
    Line2D line2DLaser;
    Particles2D particles2Dcasting;
    Particles2D particles2Dcollision;
    Particles2D particles2DBeam;

    AudioManager audioManager;

    // https://gamesounds.xyz/?dir=FXHome
    AudioStream musicClip = (AudioStream)GD.Load("res://assets/sounds/Future Weapons 2 - Energy Gun - shot_single_2.wav");
    AudioStream musicHitClip = (AudioStream)GD.Load("res://assets/sounds/Bullet Impact 22.wav");

    public override void _Ready()
    {
        isCasting = false;
        SetPhysicsProcess(isCasting);

        tween = ((Tween)GetNode("Tween"));
        line2DLaser = ((Line2D)GetNode("Line2D"));
        line2DLaser.Points[1] = Vector2.Zero;

        particles2Dcasting = ((Particles2D)GetNode("particles2DCasting"));
        particles2Dcollision = ((Particles2D)GetNode("particles2DCollision"));
        particles2DBeam = ((Particles2D)GetNode("particles2DBeam"));

        audioManager = (AudioManager)GetNode("/root/AUDIOMANAGER");
    }

    public override void _PhysicsProcess(float delta)
    {
        Vector2 castPoint = CastTo;
        ForceRaycastUpdate();

        particles2Dcollision.Emitting = IsColliding();

        if (IsColliding())
        {
            audioManager.playSoundEffect(musicHitClip);

            castPoint = ToLocal(GetCollisionPoint());
            particles2Dcollision.GlobalRotation = GetCollisionNormal().Angle();
            particles2Dcollision.Position = castPoint;

            EmitSignal(nameof(RayDamageSignal), Damage, GetCollisionNormal() * -1, source, GetCollider());
        }

        // Workaround to update points, as the Line2D points are not updatable
        Vector2[] newPoints = { new Vector2(0, 0), new Vector2(castPoint) };
        line2DLaser.Points = newPoints;

        particles2DBeam.Position = castPoint * 0.5f;
        particles2DBeam.ProcessMaterial.Set("emission_box_extents", new Vector3(castPoint.x, 10.0f, 0.0f));
    }

    public void setSource(Node2D source)
    {
        this.source = source;

        // Set the parent to player
        if (!IsConnected(nameof(RayDamageSignal), source.GetParent(), "_onDamageCalculation"))
        {
            Connect(nameof(RayDamageSignal), source.GetParent(), "_onDamageCalculation");
        }
    }

    public Node2D getSource(Node2D source)
    {
        return source;
    }

    public bool getIsCasting()
    {
        return isCasting;
    }
    public void setIsCasting(bool isCasting)
    {
        this.isCasting = isCasting;
        particles2Dcasting.Emitting = isCasting;
        particles2DBeam.Emitting = isCasting;

        if (isCasting)
        {
            appear();

            audioManager.playSoundEffect(musicClip);
        }
        else
        {
            particles2Dcollision.Emitting = false;
            disappear();
        }

        SetPhysicsProcess(isCasting);
    }


    public void appear()
    {
        tween.StopAll();
        tween.InterpolateProperty(line2DLaser, "width", 0.0f, 10.0f, 0.2f);
        tween.Start();
    }


    public void disappear()
    {
        tween.StopAll();
        tween.InterpolateProperty(line2DLaser, "width", 10.0f, 0.0f, 0.1f);
        tween.Start();
    }

}
