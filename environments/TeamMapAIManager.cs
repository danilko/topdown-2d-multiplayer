using Godot;
using System;

using System.Collections.Generic;

public class TeamMapAIManager : Node
{
    private GameWorld _gameWorld;

    private List<TeamMapAI>  _teamMapAIs;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _teamMapAIs = new List<TeamMapAI>();
    }

    public List<TeamMapAI> GetTeamMapAIs()
    {
        return _teamMapAIs;
    }


    public void Initialize(GameWorld gameWorld)
    {
        _gameWorld = gameWorld;

        List<TeamMapAISetting> teamMapAISettings = _gameWorld.GetGameStateManager().GetGameStates().GetTeamMapAISettings();

        // Start with neutral and above
        for (int index = 0; index < (int)(Team.TeamCode.NEUTRAL); index++)
        {
            TeamMapAI ai = (TeamMapAI)((PackedScene)GD.Load("res://ai/TeamMapAI.tscn")).Instance();
            ai.Name = nameof(TeamMapAI) + "_" + (Team.TeamCode)index;
            AddChild(ai);

            ai.Initialize(_gameWorld, _gameWorld.getInventoryManager(), _gameWorld.GetCapturableBaseManager(), (Team.TeamCode)index, _gameWorld.GetPathManager().GetPathFinding());

            if (teamMapAISettings != null)
            {
                ai.SetMaxUnitUsageAmount(teamMapAISettings[index].Budget);
                ai.SetAIControl(teamMapAISettings[index].AIControl);
                ai.SetTeamInitialUnitCount(teamMapAISettings[index].TotalUnitCount);
                ai.SetTeamAILevel(teamMapAISettings[index].TeamAILevel);
            }

            _teamMapAIs.Add(ai);

            foreach (CapturableBase capturable in _gameWorld.GetCapturableBaseManager().GetCapturableBases())
            {
                capturable.Connect(nameof(CapturableBase.BaseTeamChangeSignal), ai, nameof(TeamMapAI.HandleCapturableBaseCaptured));
            }
        }
    }

}
