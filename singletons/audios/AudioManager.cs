using Godot;
using System;

public class AudioManager : Node
{

    private float _defaultVolumeDb = -50.0f;

    AudioStreamPlayer[] soundEffectPlayers = new AudioStreamPlayer[100];

    AudioStream musicHitClip = (AudioStream)GD.Load("res://assets/sounds/bullethit.wav");


    public void SetSoundVolumeDb(float volumeDb)
    {
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Master"), volumeDb);
    }

    public float GetSoundVolumeDb()
    {
        return AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex("Master"));
    }

    public void playMusic(AudioStream musicClip)
    {
        AudioStreamPlayer musicPlayer = (AudioStreamPlayer)GetNode("Music/AudioStreamPlayer");
        musicPlayer.Stream = musicClip;
        musicPlayer.Play();
    }

    public override void _Ready()
    {
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Master"), _defaultVolumeDb);

        for (int index = 0; index < soundEffectPlayers.Length; index++)
        {
            soundEffectPlayers[index] = new AudioStreamPlayer();
            GetNode("SoundEffect").AddChild(soundEffectPlayers[index]);
        }
    }


    public void playSoundEffect(AudioStream musicClip)
    {
        int index = 0;
        for (index = 0; index < soundEffectPlayers.Length; index++)
        {
            if (!soundEffectPlayers[index].Playing)
            {
                soundEffectPlayers[index].Stream = musicClip;
                soundEffectPlayers[index].Play();

                break;
            }
        }
        // if no available player found, use the longest player
        if (index == soundEffectPlayers.Length)
        {
            AudioStreamPlayer currentPlayer = findOldestSoundEffectPlayer();
            if (currentPlayer != null)
            {
                currentPlayer.Stream = musicClip;
                currentPlayer.Play();
            }
        }
    }

    private AudioStreamPlayer findOldestSoundEffectPlayer()
    {

        AudioStreamPlayer lastPlayer = null;

        for (int index = 0; index < soundEffectPlayers.Length; index++)
        {
            if (lastPlayer == null)
            {
                lastPlayer = soundEffectPlayers[index];
            }

            if (soundEffectPlayers[index].GetPlaybackPosition() > lastPlayer.GetPlaybackPosition())
            {
                lastPlayer = soundEffectPlayers[index];
            }
        }

        return lastPlayer;
    }

}
