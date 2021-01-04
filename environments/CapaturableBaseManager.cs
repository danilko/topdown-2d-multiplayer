using Godot;
using System;

public class CapaturableBaseManager : Node2D
{
    private Godot.Collections.Array _bases;
    private GameWorld _gameWorld;

    public override void _Ready()
    {
        _bases = GetChildren();
    }

    public Godot.Collections.Array GetBases()
    {
        return _bases;
    }

    public void Initailize(GameWorld gameWorld)
    {
        _gameWorld = gameWorld;

        foreach(CapturableBase capturableBase in GetBases())
        {
            capturableBase.Initialize(_gameWorld);
        }
    }

}
