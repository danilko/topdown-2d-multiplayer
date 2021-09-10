using Godot;
using System;

public class TitleScreen : Control
{    private GameStates _gameStates;
     private Settings _settings;

    public override void _Ready()
    {
        _gameStates = (GameStates)GetNode("/root/GAMESTATES");
        _settings = (Settings)GetNode("CanvasLayer/Settings");
        // Ensure the pause is false to recover from disconnection
        GetTree().Paused = false;
    }


    private void _onExitPressed()
    {
        GetTree().Quit();
    }

    private void _onLobbyPressed()
    {
        _gameStates.EnterLobbyScreen();
    }

    private void _onSettingsPressed()
    {
        _settings.Initialize();
        _settings.Show();
    }
}
