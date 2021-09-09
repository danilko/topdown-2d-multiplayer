using Godot;
using System;

public class TitleScreen : Control
{    private GameStates _gameStates;

    public override void _Ready()
    {
        _gameStates = (GameStates)GetNode("/root/GAMESTATES");

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
}
