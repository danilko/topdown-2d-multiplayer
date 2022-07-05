using Godot;
using System;

public class RemoteWeapon : Weapon
{

    [Signal]
    public delegate void HealthChangedSignal();

    private float MaxSpeed = 1000f;

    protected Vector2 Velocity;

    private RandomNumberGenerator _rand;

    private Boolean _activate;

    private float _withinRange = 20.0f;

    private Vector2 _nextTargetPosition;

    private float theta = 0.0f;

    private Boolean _reachPosition = true;

    private Agent _currentTargetAgent;

    private GameWorld _gameWorld;

    private Timer _damageEffectTimer;

    private RemoteWeaponManager _remoteWeaponManager;

    private Sprite _body;

    [Export]
    public int MaxHealth = 50;

    private int _health;

    private UnitDisplay _unitDisplay;

    protected AudioStream explosionMusicClip = (AudioStream)GD.Load("res://assets/sounds/explosion_large_07.wav");

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        base._Ready();

        _rand = new RandomNumberGenerator();
        // Randomize seed
        _rand.Randomize();
        _activate = false;

        _unitDisplay = (UnitDisplay)GetNode("UnitDisplay");

        _damageEffectTimer = (Timer)GetNode("DamageEffectTimer");

        _body = (Sprite)GetNode("Body");

        _health = MaxHealth;
        
    }

    private void _onDamageEffectTimerTimeout()
    {
        _body.SelfModulate = new Color(1.0f, 1.0f, 1.0f, 1.0f);
    }

    public void Initialize(GameWorld gameWorld, Agent agent, RemoteWeaponManager remoteWeaponManager)
    {
        Agent = agent;
        _currentTargetAgent = Agent.GetTargetAgent();
        GlobalPosition = Agent.GlobalPosition;
        _gameWorld = gameWorld;

        _team = new Team();
        _team.CurrentTeamCode = agent.GetTeam();
        _gameWorld = gameWorld;

        Velocity = Vector2.Zero;
        _nextTargetPosition = Vector2.Zero;

        _remoteWeaponManager = remoteWeaponManager;

		Connect(nameof(FireSignal), _gameWorld.GetProjectileManager(), "_onProjectileShoot");

        //Set team indicator
        ((Sprite)(GetNode("TeamIndicator"))).Modulate = new Color(_team.getTeamColor(_team.CurrentTeamCode), 0.5f);
        ((Label)(_unitDisplay.GetNode("Name"))).Text = agent.GetUnitID() + "(" + _team.CurrentTeamCode + ")";

        Connect(nameof(HealthChangedSignal), _unitDisplay, "UpdateUnitBar");

        EmitSignal(nameof(HealthChangedSignal), _health * 100 / MaxHealth);

        _activate = true;
        _body.Visible = true;
    }

    public Team.TeamCode GetTeam()
    {
        return _team.CurrentTeamCode;
    }

    public virtual void Explode()
    {
        _activate = false;
        _body.Visible = false;

        CollisionShape2D collisionShape2D = (CollisionShape2D)GetNode("CollisionShape2D");
        collisionShape2D.Disabled = true;

        AnimatedSprite animatedSprite  = (AnimatedSprite)GetNode("Explosion");
        animatedSprite.Visible = true;
        animatedSprite.Play();

        AudioManager audioManager = (AudioManager)GetNode("/root/AUDIOMANAGER");
        audioManager.playSoundEffect(explosionMusicClip);
    }

    public int GetHealth()
    {
        return _health;
    }

    public void SetHealth(int health)
    {
        _health = health;
        EmitSignal(nameof(HealthChangedSignal), _health * 100 / MaxHealth);
    }

    public virtual void MoveToward(Vector2 moveDir, float delta)
    {
        Velocity = moveDir.Normalized() * MaxSpeed;
        Rotation = moveDir.Angle();
        GlobalPosition += Transform.x * MaxSpeed * delta;
    }


    public Boolean isReachedPosition(Vector2 targetPosition)
    {
        if (GlobalPosition.DistanceTo(targetPosition) <= _withinRange)
        {
            return true;
        }

        return false;
    }


    //  // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _PhysicsProcess(float delta)
    {
        if (GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
        {
            if (_activate)
            {
                if (isReachedPosition(_nextTargetPosition))
                {
                    _reachPosition = true;
                }

                // Calculate next point if position is reached
                if (_reachPosition)
                {
                    if (Agent.GetTargetAgent() != null)
                    {
                        _currentTargetAgent = Agent.GetTargetAgent();
                    }
                    else
                    {
                        _currentTargetAgent = Agent;
                    }

                    _nextTargetPosition = GetRandomPositionWithinTarget();
                    _reachPosition = false;
                }

                // Move toward next point
                MoveToward((_nextTargetPosition - GlobalPosition).Normalized(), delta);

                if (IsInstanceValid(_currentTargetAgent))
                {
                    if (_currentTargetAgent == Agent)
                    {
                        LookAt(_currentTargetAgent.GlobalPosition + new Vector2(2000, 0).Rotated(_currentTargetAgent.GlobalRotation));
                    }
                    else
                    {
                        LookAt(_currentTargetAgent.GlobalPosition);
                    }
                }
            }
        }
    }

    public void TakeDamage(int amount, Vector2 dir, Agent source, Team sourceTeam)
    {
        int originalHealth = _health;
        bool trackDamage = true;
        bool sourceAlive = true;

        if (source == null || !IsInstanceValid(source))
        {
            sourceAlive = false;
        }

        if (sourceTeam.CurrentTeamCode == _team.CurrentTeamCode)
        {
            trackDamage = false;
        }

        if (trackDamage)
        {
            if (_damageEffectTimer.IsStopped())
            {
               _body.SelfModulate = new Color(5.0f, 5.0f, 5.0f, 1.0f);
                _damageEffectTimer.Start();
            }

            _health -= amount;

            EmitSignal(nameof(HealthChangedSignal), _health * 100 / MaxHealth);

            if (_health < MaxHealth / 2)
            {
                Particles2D smoke = (Particles2D)GetNode("Smoke");
                smoke.Emitting = true;
            }

            // Only the one that actually damage agent to 0 will be count as the one defeated
            if (originalHealth > 0 && _health <= 0)
            {
                if (sourceAlive)
                {
                    source.IncrementDefeatedAgentCount();
                }
            }
        }
    }

    public override bool Fire(Agent targetAgent, Vector2 targetGlobalPosition)
    {
        if (Cooldown && Ammo != 0)
        {
            Cooldown = false;
            Ammo -= 1;
            EmitSignal(nameof(AmmoChangeSignal), Ammo, MaxAmmo, GetWeaponOrder());

            CooldownTimer.Start();

            Position2D triggerPoint = (Position2D)GetNode("TriggerPoint");

            Vector2 dir = Vector2.Right.Rotated(GlobalRotation).Normalized();

            EmitSignal(nameof(FireSignal), Bullet, triggerPoint.GlobalPosition, dir, Agent, _team, targetAgent, targetGlobalPosition);

            FireEffect();

            return true;
        }

        if (Ammo == 0)
        {
            EmitSignal(nameof(AmmoOutSignal), GetWeaponOrder());

            // Auto reload
            StartReload();
        }

        return false;
    }

    public void _onExplosionAnimationFinished()
    {
        QueueFree();
    }

    public Vector2 GetRandomPositionWithinTarget()
    {
        float dtheta = _rand.RandfRange(Mathf.Pi / 3, 2 * Mathf.Pi / 6);

        float multiplier = _rand.RandiRange(0, 1);

        if (multiplier == 0)
        {
            multiplier = -1;
        }

        theta += multiplier * dtheta;

        if (theta > 2 * Mathf.Pi)
        {
            theta = 0;
        }

        if (theta < -2 * Mathf.Pi)
        {
            theta = 0;
        }

        float raidus = _rand.RandfRange(400.0f, 700.0f);


        return new Vector2(_currentTargetAgent.GlobalPosition.x + raidus * Mathf.Cos(theta),
                _currentTargetAgent.GlobalPosition.y + raidus * Mathf.Sin(theta));


    }
}
