using Godot;
using System;

public class Trail : Node
{
    [Export]
    private int _length = 10;

    [Export]
    private float _width = 10;

    [Export]
    private Gradient _gradientColor;

    [Export]
    private float _minSpawnDistance = 5.0f;

    private Godot.RandomNumberGenerator _random;


    private Line2D _line;

    public override void _Ready()
    {
        _line = (Line2D)GetNode("Line");

        _line.Gradient = _gradientColor;
        _line.ClearPoints();

        _random = new Godot.RandomNumberGenerator();
		_random.Randomize();

        _line.GlobalPosition = Vector2.Zero;
        _line.GlobalRotation = 0;
        _line.Width = _width;
    }

    public override void _PhysicsProcess(float delta)
    {
        Vector2 _point = ((Node2D)(GetParent())).GlobalPosition;
        _line.AddPoint(_point);
        while (_line.GetPointCount() > _length)
        {
            _line.RemovePoint(0);
        }
    }

    public void Stop()
    {
    }

    public void _onTweenAllCompleted()
    {
        QueueFree();
    }
}
