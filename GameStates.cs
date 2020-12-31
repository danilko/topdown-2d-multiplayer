using Godot;
using System;
using System.Collections.Generic;

public class GameStates : Node
{

    public int current_level = 0;

    // How many game updates per second
    public float updateRate = 60;
    public float updateDelta = 0;

    public String[] levels = { "res://ui/TitleScreen.tscn", "res://map/GameWorld.tscn" };

    public String endResultScreen = "res://ui/EndGameScreen.tscn";

    public float currentTime = 0;

    public class PlayerInput
    {
        public enum InputAction {
            NOT_TRIGGER,
            TRIGGER,
            RELOAD
        }

        public int Up;

        public int Down;

        public int Left;

        public int Right;

        public Vector2 MousePosition;

        public int PrimaryWeaponAction;
        public int SecondaryWeaponAction;
        public int PrimaryWeaponIndex;
        public int SecondaryWeaponIndex;
    }

    // Holds player input data (including the local one) which will be used to update the game state
    //This will be filled only on the server
    public Dictionary<int, Dictionary<int, PlayerInput>> playerInputs = new Dictionary<int, Dictionary<int, PlayerInput>>();

    private String messages;

    public override void _Ready()
    {
        set_update_rate(updateRate);
    }


    public void cacheInput(int net_id, PlayerInput playerInput)
    {
        if (!GetTree().IsNetworkServer())
        {
            return;
        }

        if (! playerInputs.ContainsKey(net_id))
        {
            playerInputs.Add(net_id, new Dictionary<int, PlayerInput>());
        }

        playerInputs[net_id].Add(playerInputs[net_id].Count, playerInput);
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
        GetTree().ChangeScene(endResultScreen);
    }

    public void restart()
    {
        current_level = 0;
        GetTree().ChangeScene(levels[current_level]);
    }

    public void next_level()
    {
        current_level += 1;
        if (current_level < levels.Length)
        {
            GetTree().ChangeScene(levels[current_level]);
        }
        else
        {
            restart();
        }

    }
}
