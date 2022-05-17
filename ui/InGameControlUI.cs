using Godot;
using System;

public class InGameControlUI : Popup
{
    private GameWorld _gameWorld;
    private Settings _settings;

    private Input.MouseMode _previousMouseState;

    public override void _Ready()
    {
        _settings = (Settings)GetNode("Settings");

        _previousMouseState = Input.MouseMode.Hidden;
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


    public void Activate(Boolean activate)
    {
        // Enable mouse
        if(activate)
        {
            _previousMouseState = Input.GetMouseMode();
            // Enable mouse for selecting item
            Input.SetMouseMode(Input.MouseMode.Visible);
            // Use exclusive to force user must use command to close instead of random click in game screen cause unexpected weapon firing
            PopupExclusive = true;
            PopupCentered();
        }
        else
        {
            // Enable mouse for selecting item
            Input.SetMouseMode(_previousMouseState);
            Hide();
        }
    }
}
