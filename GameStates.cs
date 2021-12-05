using Godot;
using System;
using System.Collections.Generic;

public class GameStates : Node
{

    public int current_level = 0;

    // How many game updates per second
    public float updateRate = 60;
    public float updateDelta = 0;

    public String[] levels = { "res://ui/TitleScreen.tscn", "res://network/Lobby.tscn", "res://map/GameWorld.tscn" };

    public String endResultScreen = "res://ui/EndGameScreen.tscn";

    public float currentTime = 0;

    public List<TeamMapAISetting> _teamMapAISettings = null;

    private String messages;

    public override void _Ready()
    {
        set_update_rate(updateRate);
    }

    public List<TeamMapAISetting> GetTeamMapAISettings()
    {
        return _teamMapAISettings;
    }

    public void SetTeamMapAISettings(List<TeamMapAISetting> teamMapAISettings)
    {
        _teamMapAISettings = teamMapAISettings;
    }


    public void setMessagesForNextScene(String inputMessages)
    {
        messages = inputMessages;

    }

    public String getMessgesForNextScene()
    {
        return messages;
    }

    private void set_update_rate(float rate)
    {
        this.updateRate = rate;
        this.updateDelta = 1.0f / updateRate;
    }

    private float getUpdateDelta()
    {
        return updateDelta;
    }

    private void noSet(float rate) { }

    public void endGameScreen()
    {
        // In menu, enable mouse
        Input.SetMouseMode(Input.MouseMode.Visible);
        GetTree().ChangeScene(endResultScreen);
    }

    public void restart()
    {
        Input.SetMouseMode(Input.MouseMode.Visible);
        current_level = 0;
        GetTree().CurrentScene.QueueFree();
        GetTree().ChangeScene(levels[current_level]);
    }

    public void EnterLobbyScreen()
    {
        // In menu, enable mouse
        Input.SetMouseMode(Input.MouseMode.Visible);
        current_level = 1;
        GetTree().CurrentScene.QueueFree();
        GetTree().ChangeScene(levels[current_level]);
    }

    public void EnterTitleScreen()
    {
        // In menu, enable mouse
        Input.SetMouseMode(Input.MouseMode.Visible);
        current_level = 0;
        GetTree().CurrentScene.QueueFree();
        GetTree().ChangeScene(levels[current_level]);
    }

    public void EnterNetworkLevel()
    {
        current_level = 2;
        // In game, disable mouse
        Input.SetMouseMode(Input.MouseMode.Hidden);
        GetTree().CurrentScene.QueueFree();
        GetTree().ChangeScene(levels[current_level]);
    }
}
