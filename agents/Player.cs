using Godot;
using System;

public class Player : Agent
{

    [Remote]
    public void serverGetPlayerInput(String inputData)
    {
        if (GetTree().IsNetworkServer())
        {
            // Decode the input data
            GameStates.PlayerInput playerInput = new GameStates.PlayerInput();
            int parseIndex = 0;

            playerInput.Right = Int32.Parse(inputData.Split(";")[parseIndex]);
            parseIndex++;
            playerInput.Left = Int32.Parse(inputData.Split(";")[parseIndex]);
            parseIndex++;
            playerInput.Up = Int32.Parse(inputData.Split(";")[parseIndex]);
            parseIndex++;
            playerInput.Down = Int32.Parse(inputData.Split(";")[parseIndex]);
            parseIndex++;
            playerInput.PrimaryWeaponAction = Int32.Parse(inputData.Split(";")[parseIndex]);
            parseIndex++;
            playerInput.SecondaryWeaponAction = Int32.Parse(inputData.Split(";")[parseIndex]);
            parseIndex++;
            playerInput.MousePosition.x = float.Parse(inputData.Split(";")[parseIndex]);
            parseIndex++;
            playerInput.MousePosition.y = float.Parse(inputData.Split(";")[parseIndex]);
            parseIndex++;
            playerInput.PrimaryWeaponIndex = Int32.Parse(inputData.Split(";")[parseIndex]);
            parseIndex++;
            playerInput.SecondaryWeaponIndex = Int32.Parse(inputData.Split(";")[parseIndex]);
            parseIndex++;

            // Then cache the decoded data
            gameStates.cacheInput(GetTree().GetRpcSenderId(), playerInput);

        }
    }


    public void gatherInput(float delta)
    {
        GameStates.PlayerInput playerInput = new GameStates.PlayerInput();

        if (Input.IsActionPressed("turn_right"))
        {
            playerInput.Right = (int)(GameStates.PlayerInput.InputAction.TRIGGER);
        }
        else
        {
            playerInput.Right = (int)(GameStates.PlayerInput.InputAction.NOT_TRIGGER);
        }

        if (Input.IsActionPressed("turn_left"))
        {
            playerInput.Left = (int)(GameStates.PlayerInput.InputAction.TRIGGER);
        }
        else
        {
            playerInput.Left = (int)(GameStates.PlayerInput.InputAction.NOT_TRIGGER);
        }

        if (Input.IsActionPressed("forward"))
        {
            playerInput.Up = (int)(GameStates.PlayerInput.InputAction.TRIGGER);
        }
        else
        {
            playerInput.Up = (int)(GameStates.PlayerInput.InputAction.NOT_TRIGGER);
        }

        if (Input.IsActionPressed("backward"))
        {
            playerInput.Down = (int)(GameStates.PlayerInput.InputAction.TRIGGER);
        }
        else
        {
            playerInput.Down = (int)(GameStates.PlayerInput.InputAction.NOT_TRIGGER);
        }


        if (Input.IsActionPressed("reload"))
        {
            playerInput.PrimaryWeaponAction = (int)(GameStates.PlayerInput.InputAction.RELOAD);
        }
        else
        {
            if (Input.IsActionPressed("left_click"))
            {
                playerInput.PrimaryWeaponAction = (int)(GameStates.PlayerInput.InputAction.TRIGGER);
            }
            else
            {
                playerInput.PrimaryWeaponAction = (int)(GameStates.PlayerInput.InputAction.NOT_TRIGGER);
            }
        }


        if (Input.IsActionPressed("right_click"))
        {
            playerInput.SecondaryWeaponAction = (int)(GameStates.PlayerInput.InputAction.TRIGGER);
        }
        else
        {
            playerInput.SecondaryWeaponAction = (int)(GameStates.PlayerInput.InputAction.NOT_TRIGGER);
        }

        playerInput.MousePosition = GetGlobalMousePosition();

        if(Input.IsKeyPressed((int)KeyList.Key1))
        {
            playerInput.PrimaryWeaponIndex = 0;
        }
        else if(Input.IsKeyPressed((int)KeyList.Key2))
        {
            playerInput.PrimaryWeaponIndex = 1;
        }
        else if(Input.IsKeyPressed((int)KeyList.Key3))
        {
            playerInput.PrimaryWeaponIndex = 2;
        }
        else
        {
            playerInput.PrimaryWeaponIndex = currentPrimaryWeaponIndex;
        }

        if(Input.IsKeyPressed((int)KeyList.Key4))
        {
            playerInput.SecondaryWeaponIndex = 0;
        }
        else if(Input.IsKeyPressed((int)KeyList.Key5))
        {
            playerInput.SecondaryWeaponIndex = 1;
        }
        else if(Input.IsKeyPressed((int)KeyList.Key6))
        {
            playerInput.SecondaryWeaponIndex = 2;
        }
        else
        {
            playerInput.SecondaryWeaponIndex = currentSecondaryWeaponIndex;
        }


        if (GetTree().IsNetworkServer())
        {
            gameStates.cacheInput(1, playerInput);
        }
        else
        {
            String inputData = "";
            inputData = inputData + playerInput.Right + ";";
            inputData = inputData + playerInput.Left + ";";
            inputData = inputData + playerInput.Up + ";";
            inputData = inputData + playerInput.Down + ";";
            inputData = inputData + playerInput.PrimaryWeaponAction + ";";
            inputData = inputData + playerInput.SecondaryWeaponAction + ";";
            inputData = inputData + playerInput.MousePosition.x + ";";
            inputData = inputData + playerInput.MousePosition.y + ";";
            inputData = inputData + playerInput.PrimaryWeaponIndex + ";";
            inputData = inputData + playerInput.SecondaryWeaponIndex + ";";

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
