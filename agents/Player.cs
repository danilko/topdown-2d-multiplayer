using Godot;
using System;

public class Player : Agent
{

    private HUD _hud = null;
    private InventoryUI _inventoryUI = null;

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
            playerInput.RightWeaponAction = Int32.Parse(inputData.Split(";")[parseIndex]);
            parseIndex++;
            playerInput.LeftWeaponAction = Int32.Parse(inputData.Split(";")[parseIndex]);
            parseIndex++;
            playerInput.MousePosition.x = float.Parse(inputData.Split(";")[parseIndex]);
            parseIndex++;
            playerInput.MousePosition.y = float.Parse(inputData.Split(";")[parseIndex]);
            parseIndex++;
            playerInput.RightWeaponIndex = Int32.Parse(inputData.Split(";")[parseIndex]);
            parseIndex++;
            playerInput.LeftWeaponIndex = Int32.Parse(inputData.Split(";")[parseIndex]);
            parseIndex++;

            // Then cache the decoded data
            gameStates.cacheInput(GetTree().GetRpcSenderId(), playerInput);

        }
    }

    public void SetHUD(HUD hud, InventoryManager _inventoryManager)
    {
        isCurrentPlayer = true;
        
        _hud = hud;

        Connect(nameof(Agent.WeaponChangeSignal), _hud, nameof(HUD.UpdateWeapon));
        Connect(nameof(Agent.HealthChangedSignal), _hud, nameof(HUD.UpdateHealth));
        Connect(nameof(Agent.DefeatedAgentChangedSignal), _hud, nameof(HUD.UpdateDefeatedAgent));

        setHealth(MaxHealth);
        setEnergy(MaxEnergy);

        for (int index = 0; index <= (int)Weapon.WeaponOrder.Left; index++)
        {
            Weapon.WeaponOrder weaponOrder = (Weapon.WeaponOrder)index;
            Weapon weapon = GetWeapons(weaponOrder)[GetCurrentWeaponIndex(weaponOrder)];
            if (weapon != null)
            {
                ConnectWeapon(weapon, weaponOrder);

                EmitSignal(nameof(WeaponChangeSignal), CurrentInventory.GetItems()[CurrentInventory.GetEquipItemIndex(weaponOrder, GetCurrentWeaponIndex(weaponOrder))], weaponOrder);
                // Emit signal to update info
                weapon.EmitSignal(nameof(Weapon.AmmoChangeSignal), weapon.GetAmmo(), weapon.GetMaxAmmo(), weaponOrder);
            }
            else
            {
                EmitSignal(nameof(WeaponChangeSignal), null, weaponOrder);
            }
        }

        // Set up the player indicator screen
        ScreenIndicator screenIndicator = (ScreenIndicator)((PackedScene)GD.Load("res://ui/ScreenIndicator.tscn")).Instance();
        AddChild(screenIndicator);
        screenIndicator.Initialize(this);

        // Setup Inventory UI
        _inventoryUI = (InventoryUI)_hud.GetNode("controlGame/InventoryUI");
        _inventoryUI.Initialize(_inventoryManager, CurrentInventory);

        if (!_teamMapAI.IsConnected(nameof(TeamMapAI.TeamUnitUsageAmountChangeSignal), _hud, nameof(HUD.UpdateTeamUnitUsageAmount)))
        {
            _teamMapAI.Connect(nameof(TeamMapAI.TeamUnitUsageAmountChangeSignal), _hud, nameof(HUD.UpdateTeamUnitUsageAmount));
            // Notify the HUD to change
            _teamMapAI.ChargeAmount(0);
        }
    }

    protected override void DisconnectWeapon(Weapon currentWeapon, Weapon.WeaponOrder weaponOrder)
    {
        base.DisconnectWeapon(currentWeapon, weaponOrder);

        if (currentWeapon != null && _hud != null)
        {
            // Deregister weapon from UI if connected
            if (currentWeapon.IsConnected(nameof(Weapon.AmmoChangeSignal), _hud, nameof(HUD.UpdateWeaponAmmo)))
            {
                currentWeapon.Disconnect(nameof(Weapon.AmmoChangeSignal), _hud, nameof(HUD.UpdateWeaponAmmo));
                currentWeapon.Disconnect(nameof(Weapon.AmmoOutSignal), _hud, nameof(HUD.UpdateWeaponAmmoOut));
                currentWeapon.Disconnect(nameof(Weapon.ReloadSignal), _hud, nameof(HUD.UpdateWeaponReload));
            }
        }
    }

    protected override void ConnectWeapon(Weapon currentWeapon, Weapon.WeaponOrder weaponOrder)
    {
        base.ConnectWeapon(currentWeapon, weaponOrder);

        if (currentWeapon != null && _hud != null)
        {
            // Register new weapon with UI if not connect (as cannot connect again)
            if (!currentWeapon.IsConnected(nameof(Weapon.AmmoChangeSignal), _hud, nameof(HUD.UpdateWeaponAmmo)))
            {
                currentWeapon.Connect(nameof(Weapon.AmmoChangeSignal), _hud, nameof(HUD.UpdateWeaponAmmo));
                currentWeapon.Connect(nameof(Weapon.AmmoOutSignal), _hud, nameof(HUD.UpdateWeaponAmmoOut));
                currentWeapon.Connect(nameof(Weapon.ReloadSignal), _hud, nameof(HUD.UpdateWeaponReload));
            }

            base.ConnectWeapon(currentWeapon, weaponOrder);
        }
    }


    public void gatherInput(float delta)
    {
        if (Input.IsActionJustReleased("inventory"))
        {
            if (!_inventoryUI.Visible)
            {
                _inventoryUI.PopupCentered();
            }
            else
            {
                _inventoryUI.Hide();
            }
        }


        // Only read input is inventory is not open
        if (_inventoryUI == null || !_inventoryUI.Visible)
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
                playerInput.RightWeaponAction = (int)(GameStates.PlayerInput.InputAction.RELOAD);
                playerInput.LeftWeaponAction = (int)(GameStates.PlayerInput.InputAction.RELOAD);
            }
            else
            {
                if (Input.IsActionPressed("left_click"))
                {
                    playerInput.LeftWeaponAction = (int)(GameStates.PlayerInput.InputAction.TRIGGER);
                }
                else
                {
                    playerInput.LeftWeaponAction = (int)(GameStates.PlayerInput.InputAction.NOT_TRIGGER);
                }

                if (Input.IsActionPressed("right_click"))
                {
                    playerInput.RightWeaponAction = (int)(GameStates.PlayerInput.InputAction.TRIGGER);
                }
                else
                {
                    playerInput.RightWeaponAction = (int)(GameStates.PlayerInput.InputAction.NOT_TRIGGER);
                }
            }

            playerInput.MousePosition = GetGlobalMousePosition();

            if (Input.IsKeyPressed((int)KeyList.Key4))
            {
                playerInput.RightWeaponIndex = 0;
            }
            else if (Input.IsKeyPressed((int)KeyList.Key5))
            {
                playerInput.RightWeaponIndex = 1;
            }
            else if (Input.IsKeyPressed((int)KeyList.Key6))
            {
                playerInput.RightWeaponIndex = 2;
            }
            else
            {
                playerInput.RightWeaponIndex = CurrentWeaponIndex[Weapon.WeaponOrder.Right];
            }

            if (Input.IsKeyPressed((int)KeyList.Key1))
            {
                playerInput.LeftWeaponIndex = 0;
            }
            else if (Input.IsKeyPressed((int)KeyList.Key2))
            {
                playerInput.LeftWeaponIndex = 1;
            }
            else if (Input.IsKeyPressed((int)KeyList.Key3))
            {
                playerInput.LeftWeaponIndex = 2;
            }
            else
            {
                playerInput.LeftWeaponIndex = CurrentWeaponIndex[Weapon.WeaponOrder.Left];
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
                inputData = inputData + playerInput.RightWeaponAction + ";";
                inputData = inputData + playerInput.LeftWeaponAction + ";";
                inputData = inputData + playerInput.MousePosition.x + ";";
                inputData = inputData + playerInput.MousePosition.y + ";";
                inputData = inputData + playerInput.RightWeaponIndex + ";";
                inputData = inputData + playerInput.LeftWeaponIndex + ";";

                RpcUnreliableId(1, nameof(serverGetPlayerInput), inputData);
            }
        }
    }

    public override void _PhysicsProcess(float delta)
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

        if (GetTree().NetworkPeer != null && IsNetworkMaster())
        {
            gatherInput(delta);
        }

    }
}
