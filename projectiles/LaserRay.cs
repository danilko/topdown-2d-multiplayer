using Godot;
using System;

public class LaserRay : RayCast2D
{
    [Signal]
    public delegate void RayDamageSignal();

    [Export]
    int Damage;

    bool isCasting;

    private Agent _sourceAgent;
    private Team _sourceTeam;

    Tween tween;
    Line2D line2DLaser;
    Particles2D particles2Dcasting;
    Particles2D particles2Dcollision;
    Particles2D particles2DBeam;

    private GameWorld _gameWorld;
    private AudioManager _audioManager;

    // https://gamesounds.xyz/?dir=FXHome
    private AudioStream _musicClip = (AudioStream)GD.Load("res://assets/sounds/Future Weapons 2 - Energy Gun - shot_single_2.wav");
    private AudioStream _musicHitClip = (AudioStream)GD.Load("res://assets/sounds/Bullet Impact 22.wav");

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

        _audioManager = (AudioManager)GetNode("/root/AUDIOMANAGER");
    }


    public override void _PhysicsProcess(float delta)
    {
        Vector2 castPoint = CastTo;
        ForceRaycastUpdate();

        particles2Dcollision.Emitting = IsColliding();

        if (IsColliding())
        {
            _audioManager.playSoundEffect(_musicHitClip);

            castPoint = ToLocal(GetCollisionPoint());
            particles2Dcollision.GlobalRotation = GetCollisionNormal().Angle();
            particles2Dcollision.Position = castPoint;

            // Projectile will collide
            if (GetCollider().HasMethod("_onProjectileAreaEntered"))
            {
                ProjectileArea2D collider = (ProjectileArea2D)(GetCollider());

                // Only bullets from different team will cloide
                if (collider.GetTeam() != _sourceTeam.CurrentTeamCode)
                {
                    collider.Explode();
                }
            }
            // shield will collide
            else if (GetCollider().HasMethod("_onShieldAreaEntered"))
            {
                ShieldPhysics shieldPhysics = (ShieldPhysics)GetCollider();
                shieldPhysics.TakeShieldDamage(Damage);
            }
            else
            {
                EmitSignal(nameof(RayDamageSignal), Damage, GetCollisionNormal() * -1, _sourceAgent, _sourceTeam, GetCollider());
            }
        }

        // Workaround to update points, as the Line2D points are not updatable
        Vector2[] newPoints = { new Vector2(0, 0), new Vector2(castPoint) };
        line2DLaser.Points = newPoints;

        particles2DBeam.Position = castPoint * 0.5f;
        particles2DBeam.ProcessMaterial.Set("emission_box_extents", new Vector3(castPoint.x, 20.0f, 0.0f));
    }

    public void Initialize(GameWorld gameWorld, Agent sourceAgent, Team sourceTeam)
    {
        _sourceAgent = sourceAgent;
        _sourceTeam = sourceTeam;
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

            _audioManager.playSoundEffect(_musicClip);
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
        tween.InterpolateProperty(line2DLaser, "width", 0.0f, 20.0f, 0.2f);
        tween.Start();
    }


    public void disappear()
    {
        tween.StopAll();
        tween.InterpolateProperty(line2DLaser, "width", 20.0f, 0.0f, 0.1f);
        tween.Start();
    }

}
