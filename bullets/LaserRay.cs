using Godot;
using System;

public class LaserRay : RayCast2D
{
    [Signal]
    public delegate void RayDamageSignal();

    [Export]
    int Damage;

    bool isCasting;

    private Agent _agent;
    private Team _team;

    Tween tween;
    Line2D line2DLaser;
    Particles2D particles2Dcasting;
    Particles2D particles2Dcollision;
    Particles2D particles2DBeam;

    private GameWorld _gameWorld; 
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

            EmitSignal(nameof(RayDamageSignal), Damage, GetCollisionNormal() * -1, _agent, _team, GetCollider());
        }

        // Workaround to update points, as the Line2D points are not updatable
        Vector2[] newPoints = { new Vector2(0, 0), new Vector2(castPoint) };
        line2DLaser.Points = newPoints;

        particles2DBeam.Position = castPoint * 0.5f;
        particles2DBeam.ProcessMaterial.Set("emission_box_extents", new Vector3(castPoint.x, 10.0f, 0.0f));
    }

    public void Initialize(GameWorld gameWorld, Agent sourceAgent, Team sourceTeam)
    {
        _agent = sourceAgent;
        _team = sourceTeam;
        _gameWorld = gameWorld;
        
        // Set the parent to player
        if (!IsConnected(nameof(RayDamageSignal), _gameWorld, "_onDamageCalculation"))
        {
            Connect(nameof(RayDamageSignal), _gameWorld, "_onDamageCalculation");
        }
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
