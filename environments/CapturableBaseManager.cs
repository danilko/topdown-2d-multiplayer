using Godot;
using System.Collections.Generic;

public class CapturableBaseManager : Node
{
    private List<CapturableBase> _bases;
    private GameWorld _gameWorld;

    public override void _Ready()
    {
        _bases = new List<CapturableBase>();
    }

    public List<CapturableBase> GetCapturableBases()
    {
        return _bases;
    }

    public void Initailize(GameWorld gameWorld)
    {
        _gameWorld = gameWorld;

        foreach(CapturableBase capturableBase in GetChildren())
        {
            _bases.Add(capturableBase);
            capturableBase.Initialize(_gameWorld);
        }
    }

}
