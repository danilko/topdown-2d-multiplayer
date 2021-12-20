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

        int index = 0;

        foreach(CapturableBase capturableBase in GetChildren())
        {
            _bases.Add(capturableBase);
            capturableBase.Initialize(_gameWorld, index);
            index++;
        }
    }

    public List <CapturableBase> GetAvailableBases(Team.TeamCode teamCode)
    {
        List <CapturableBase> baseList = new List<CapturableBase>();

       foreach(CapturableBase capturableBase in GetChildren())
        {
            if(capturableBase.GetCaptureBaseTeam() == teamCode)
            {
                baseList.Add(capturableBase);
            }
        }

        return baseList;
    }
}
