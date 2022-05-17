using Godot;
using System;

public class RemoteWeapon : Sprite
{

    private Node2D _target;

    private Vector2 _distance = new Vector2(500f, 500f);

    private float MaxSpeed = 500f;

    protected Vector2 Velocity;

    private RandomNumberGenerator _rand;

    private Boolean _activate;

    private int Speed = 500;

    private float _withinRange = 5.0f;

    private Vector2 _nextTargetPosition;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _rand = new RandomNumberGenerator();
        _activate = false;
    }

    public void initialize(Node2D target)
    {
        _target = target;
        Velocity = Vector2.Zero;
        _nextTargetPosition = Vector2.Zero;
    }

    public void Activate()
    {
        _activate = true;

        Visible = true;
    }

    public virtual void MoveToward(Vector2 moveDir, float delta)
    {
        Velocity = moveDir.Normalized() * MaxSpeed;
        Rotation = moveDir.Angle();
        GlobalPosition += Transform.x * MaxSpeed * delta;
        LookAt(_target.GlobalPosition);
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
        if (_activate)
        {
            if (isReachedPosition(_nextTargetPosition))
            {
                _nextTargetPosition = GetRandomPositionWithinTarget();
            }

            MoveToward((_nextTargetPosition - GlobalPosition).Normalized(), delta);

        }



    }

    public Vector2 GetRandomPositionWithinTarget()
    {
        if (_target != null && IsInstanceValid(_target))
        {
            Vector2 topLeftCorner = _target.GlobalPosition - _distance;
            Vector2 bottomRightCorner = _target.GlobalPosition + _distance;

            float randX = _rand.RandfRange(topLeftCorner.x, bottomRightCorner.x);
            float randY = _rand.RandfRange(topLeftCorner.y, bottomRightCorner.y);

            return new Vector2(randX, randY);
        }

        return Vector2.Zero;
    }

}
