using Godot;
using System;

public class Settings : Popup
{
    AudioManager _audioManager;
    HScrollBar _soundVolume;

    private Input.MouseMode _previousMouseState;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _audioManager = (AudioManager)GetNode("/root/AUDIOMANAGER");
        _soundVolume = (HScrollBar)GetNode("CenterContainer/VBoxContainer/SoundVolumeSetting");

        _previousMouseState = Input.MouseMode.Hidden;
    }

    public void Activate(Boolean activate)
    {
        // Enable mouse
        if(activate)
        { 
            _initializeSoundVolume();
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

    private void _initializeSoundVolume()
    {
        _soundVolume.Value = (double)_audioManager.GetSoundVolumeDb();
    }

    private void _onSoundVolumeSettingValueChanged(float volumeDb)
    {
        _audioManager.SetSoundVolumeDb(volumeDb);
    }

    private void _onClosePressed()
    {
        Activate(false);
    }
}
