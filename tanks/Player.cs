using Godot;
using System;

public class Player : Tank
{


    public GameStates gameStates;
    public Network network;

    public override void _Ready()
    {
        gameStates = (GameStates)GetNode("/root/GAMESTATES");
        network = (Network)GetNode("/root/NETWORK");

        base._Ready();
    }


    [Remote]
    public void serverGetPlayerInput(String inputData)
    {
        if (GetTree().IsNetworkServer())
        {
            // Decode the input data
            GameStates.PlayerInput playerInput = new GameStates.PlayerInput();

            playerInput.right = bool.Parse(inputData.Split(";")[0]);
            playerInput.left = bool.Parse(inputData.Split(";")[1]);
            playerInput.up = bool.Parse(inputData.Split(";")[2]);
            playerInput.down = bool.Parse(inputData.Split(";")[3]);
            playerInput.primaryWepaon = bool.Parse(inputData.Split(";")[4]);
            playerInput.secondaryWepaon = bool.Parse(inputData.Split(";")[5]);
            playerInput.mousePosition.x = float.Parse(inputData.Split(";")[6]);
            playerInput.mousePosition.y = float.Parse(inputData.Split(";")[7]);

            // Then cache the decoded data
            gameStates.cacheInput(GetTree().GetRpcSenderId(), playerInput);

        }
    }


    public void gatherInput(float delta)
    {




        GameStates.PlayerInput playerInput = new GameStates.PlayerInput();

        playerInput.right = Input.IsActionPressed("turn_right");
        playerInput.left = Input.IsActionPressed("turn_left");
        playerInput.up = Input.IsActionPressed("forward");
        playerInput.down = Input.IsActionPressed("backward");
        playerInput.primaryWepaon = Input.IsActionPressed("left_click");
        playerInput.secondaryWepaon = Input.IsActionPressed("right_click");
        playerInput.mousePosition = GetGlobalMousePosition();

        if (GetTree().IsNetworkServer())
        {
            gameStates.cacheInput(1, playerInput);
        }
        else
        {
            String inputData = "";
            inputData = inputData + playerInput.right + ";";
            inputData = inputData + playerInput.left + ";";
            inputData = inputData + playerInput.up + ";";
            inputData = inputData + playerInput.down + ";";
            inputData = inputData + playerInput.primaryWepaon + ";";
            inputData = inputData + playerInput.secondaryWepaon + ";";
            inputData = inputData + playerInput.mousePosition.x + ";";
            inputData = inputData + playerInput.mousePosition.y + ";";

            RpcUnreliableId(1, nameof(serverGetPlayerInput), inputData);
        }
    }

    public override void _Process(float delta)
    {
        if (!Alive)
        {
            return;
        }


        // Update the timeout counter and if "outside of the update window", bail
        gameStates.currentTime += delta;
        if (gameStates.currentTime < gameStates.updateDelta)
        {
            return;
        }

        // Inside an "input" window. First "reset" the time counting variable.
        // Rather than just resetting to 0, subtract update_delta from it to try to compensate
        // for some variances in the time counting. Ideally it would be a good idea to check if the
        // current_time is still bigger than update_delta after this subtraction which would indicate
        //some major lag in the game
        gameStates.currentTime -= gameStates.updateDelta;

        if (IsNetworkMaster())
        {
            gatherInput(delta);
        }

    }
}
