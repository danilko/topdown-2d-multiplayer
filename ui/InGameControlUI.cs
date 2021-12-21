using Godot;
using System;

public class InGameControlUI : Popup
{
    private GameWorld _gameWorld;
    private Settings _settings;

    public override void _Ready()
    {
        _settings = (Settings)GetNode("Settings");
    }

    public void Initialize(GameWorld gameWorld)
    {
        _gameWorld = gameWorld;
    }

    private void _onEnableSettingUI()
    {
        _settings.PopupCentered();
    }

    private void _onExitGame()
    {
        _gameWorld.GetGameStateManager().GetGameStates().EnterTitleScreen();
    }

//  // Called every frame. 'delta' is the elapsed time since the previous frame.
//  public override void _Process(float delta)
//  {
//      
//  }
}
