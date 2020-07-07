using Godot;
using System;

public class Player : Tank
{

    [Puppet]
    public bool remotePrimaryWeapon;

    [Puppet]
    public bool remoteSecondaryWeapon;

    [Puppet]
    Vector2 remotePosition = new Vector2();

    [Puppet]
    float remoteRotation = 0.0f;

    [Puppet]
    int remoteHealth = 0;

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

            Vector2 moveDir = new Vector2();
            if (playerInput.up) { moveDir.y = -1; }
            if (playerInput.down) { moveDir.y = 1; }
            if (playerInput.left) { moveDir.x = -1; }
            if (playerInput.right) { moveDir.x = 1; }
            Velocity = moveDir.Normalized() * MaxSpeed;

            MoveAndSlide(Velocity);
            LookAt(playerInput.mousePosition);

            _shoot(playerInput.primaryWepaon, playerInput.secondaryWepaon);

            RsetUnreliable(nameof(remotePosition), Position);
            RsetUnreliable(nameof(remoteRotation), Rotation);
            RsetUnreliable(nameof(remoteHealth), getHealth());
            RsetUnreliable(nameof(remotePrimaryWeapon), playerInput.primaryWepaon);
            RsetUnreliable(nameof(remoteSecondaryWeapon), playerInput.secondaryWepaon);
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

        String inputData = "";
        inputData = inputData + playerInput.right + ";";
        inputData = inputData + playerInput.left + ";";
        inputData = inputData + playerInput.up + ";";
        inputData = inputData + playerInput.down + ";";
        inputData = inputData + playerInput.primaryWepaon + ";";
        inputData = inputData + playerInput.secondaryWepaon + ";";
        inputData = inputData + playerInput.mousePosition.x + ";";
        inputData = inputData + playerInput.mousePosition.y + ";";

        if (GetTree().IsNetworkServer())
        {
            serverGetPlayerInput(inputData);
        }
        else
        {

            RpcUnreliableId(1, nameof(serverGetPlayerInput), inputData);
        }
    }

    public override void _Process(float delta)
    {
        if (!Alive)
        {
            return;
        }

        if (IsNetworkMaster())
        {
            GameStates.PlayerInput playerInput = new GameStates.PlayerInput();

            playerInput.right = Input.IsActionPressed("turn_right");
            playerInput.left = Input.IsActionPressed("turn_left");
            playerInput.up = Input.IsActionPressed("forward");
            playerInput.down = Input.IsActionPressed("backward");
            playerInput.primaryWepaon = Input.IsActionPressed("left_click");
            playerInput.secondaryWepaon = Input.IsActionPressed("right_click");
            playerInput.mousePosition = GetGlobalMousePosition();


            Vector2 moveDir = new Vector2();
            if (playerInput.up) { moveDir.y = -1; }
            if (playerInput.down) { moveDir.y = 1; }
            if (playerInput.left) { moveDir.x = -1; }
            if (playerInput.right) { moveDir.x = 1; }
            Velocity = moveDir.Normalized() * MaxSpeed;

            MoveAndSlide(Velocity);
            LookAt(playerInput.mousePosition);

            _shoot(playerInput.primaryWepaon, playerInput.secondaryWepaon);

            RsetUnreliable(nameof(remotePosition), Position);
            RsetUnreliable(nameof(remoteRotation), Rotation);
            RsetUnreliable(nameof(remoteHealth), getHealth());
            RsetUnreliable(nameof(remotePrimaryWeapon), playerInput.primaryWepaon);
            RsetUnreliable(nameof(remoteSecondaryWeapon), playerInput.secondaryWepaon);
        }
        else
        {
            _shoot(remotePrimaryWeapon, remoteSecondaryWeapon);

            Position = remotePosition;
            Rotation = remoteRotation;
            setHealth(remoteHealth);
        }

    }
}
