using Godot;
using System;

public class CapaturableBaseManager : Node2D
{
    private Godot.Collections.Array<CapturableBase> _bases;

    public override void _Ready()
    {
        _bases = new Godot.Collections.Array<CapturableBase>();
        foreach(CapturableBase captureableBase in GetChildren())
        {
           _bases.Add(captureableBase);
        }
    }

    public Godot.Collections.Array<CapturableBase> GetBases()
    {
        return _bases;
    }

}
