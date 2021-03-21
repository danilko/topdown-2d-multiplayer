using Godot;
using System;

public class Trail : Node
{
    [Export]
    private int _length = 10;

    [Export]
    private float _width = 10;


    private Line2D _line;

    public override void _Ready()
    {
        _line = (Line2D)GetNode("Line");

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

}
