using Godot;
using System;

public class CapaturableBaseManager : Node2D
{
    private Godot.Collections.Array _bases;

    public override void _Ready()
    {
        _bases = GetChildren();
    }

    public Godot.Collections.Array GetBases()
    {
        return _bases;
    }

}
