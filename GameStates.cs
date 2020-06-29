using Godot;
using System;
using System.Collections.Generic;

public class GameStates : Node
{

    public int current_level = 0;

    // How many game updates per second
    public float updateRate = 30;
    public float updateDelta = 0;

    public String[] levels = { "res://ui/TitleScreen.tscn", "res://map/Map.tscn" };


    public float currentTime = 0;

    public class PlayerInput
    {
        public bool up;

        public bool down;

        public bool left;

        public bool right;

        public Vector2 mousePosition;

        public bool primaryWepaon;
        public bool secondaryWepaon;

    }

    // Holds player input data (including the local one) which will be used to update the game state
    //This will be filled only on the server
    public Dictionary<int, Dictionary<int, PlayerInput>> playerInputs = new Dictionary<int, Dictionary<int, PlayerInput>>();


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
