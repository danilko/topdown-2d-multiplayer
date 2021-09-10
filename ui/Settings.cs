using Godot;
using System;

public class Settings : Popup
{
    AudioManager _audioManager;
    HScrollBar _soundVolume;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _audioManager = (AudioManager)GetNode("/root/AUDIOMANAGER");
        _soundVolume = (HScrollBar)GetNode("CenterContainer/VBoxContainer/SoundVolumeSetting");

    }

    public void Initialize()
    {
        _initializeSoundVolume();
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
        this.Hide();
    }
}
