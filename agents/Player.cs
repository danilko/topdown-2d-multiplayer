using Godot;
using System;

public class Player : Agent
{

    private HUD _hud = null;
    private InventoryUI _inventoryUI = null;

    private ScreenIndicator _screenIndicator = null;

    [Remote]
    public void serverGetPlayerInput(String inputData)
    {
        if (GetTree().IsNetworkServer())
        {
            // Decode the input data
            NetworkSnapshotManager.PlayerInput playerInput = new NetworkSnapshotManager.PlayerInput();
            int parseIndex = 0;

            playerInput.Right = Int32.Parse(inputData.Split(";")[parseIndex]);
            parseIndex++;
            playerInput.Left = Int32.Parse(inputData.Split(";")[parseIndex]);
            parseIndex++;
            playerInput.Up = Int32.Parse(inputData.Split(";")[parseIndex]);
            parseIndex++;
            playerInput.Down = Int32.Parse(inputData.Split(";")[parseIndex]);
            parseIndex++;
            playerInput.MousePosition.x = float.Parse(inputData.Split(";")[parseIndex]);
            parseIndex++;
            playerInput.MousePosition.y = float.Parse(inputData.Split(";")[parseIndex]);
            parseIndex++;
            playerInput.RightWeaponIndex = Int32.Parse(inputData.Split(";")[parseIndex]);
            parseIndex++;
            playerInput.LeftWeaponIndex = Int32.Parse(inputData.Split(";")[parseIndex]);
            parseIndex++;
            playerInput.RightWeaponAction = Int32.Parse(inputData.Split(";")[parseIndex]);
            parseIndex++;
            playerInput.LeftWeaponAction = Int32.Parse(inputData.Split(";")[parseIndex]);
            parseIndex++;
            playerInput.RemoteWeaponAction = Int32.Parse(inputData.Split(";")[parseIndex]);
            parseIndex++;
            playerInput.TargetSelectionAction = Int32.Parse(inputData.Split(";")[parseIndex]);
            parseIndex++;

            // Then cache the decoded data
            _gameWorld.GetNetworkSnasphotManager().CacheInput(GetTree().GetRpcSenderId(), playerInput);

        }
    }

    public void SetHUD(HUD hud, InventoryManager _inventoryManager)
    {
        CurrentPlayer = true;

        _hud = hud;

        Connect(nameof(Agent.WeaponChangeSignal), _hud, nameof(HUD.UpdateWeapon));

        for (int index = 0; index <= (int)Weapon.WeaponOrder.Left; index++)
        {
            Weapon.WeaponOrder weaponOrder = (Weapon.WeaponOrder)index;
            Weapon weapon = GetWeapons(weaponOrder)[GetCurrentWeaponIndex(weaponOrder)];
            if (weapon != null)
            {
                ConnectWeapon(weapon, weaponOrder);

                EmitSignal(nameof(WeaponChangeSignal), CurrentInventory.GetItems()[CurrentInventory.GetEquipItemIndex(weaponOrder, GetCurrentWeaponIndex(weaponOrder))], weaponOrder, index);
                // Emit signal to update info
                weapon.EmitSignal(nameof(Weapon.AmmoChangeSignal), weapon.GetAmmo(), weapon.GetMaxAmmo(), weaponOrder);
            }
            else
            {
                EmitSignal(nameof(WeaponChangeSignal), null, weaponOrder, index);
            }
        }

        // Set up the player indicator screen
        _screenIndicator = (ScreenIndicator)((PackedScene)GD.Load("res://ui/ScreenIndicator.tscn")).Instance();
        AddChild(_screenIndicator);
        _screenIndicator.Initialize(_gameWorld, this);
        _screenIndicator.SetActivate(true);
        Connect(nameof(Agent.HealthChangedSignal), _screenIndicator, nameof(ScreenIndicator.UpdateHealth));

        SetHealth(MaxHealth);
        SetEnergy(MaxEnergy);

        DetectionZone.Connect(nameof(DetectionZone.AgentEnteredSignal), _screenIndicator, "_onAgentEntered");
        DetectionZone.Connect(nameof(DetectionZone.AgentExitedSignal), _screenIndicator, "_onAgentExited");
        DetectionZone.Connect(nameof(DetectionZone.AutoTargetAgentSelectionChangeSignal), _hud, "_updateAutoTargetSelection");



        // Setup Inventory UI
        _inventoryUI = _hud.GetInventoryUI();
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

        if (_screenIndicator != null && currentWeapon != null && _hud != null)
        {
            // Deregister weapon from UI if connected
            if (currentWeapon.IsConnected(nameof(Weapon.AmmoChangeSignal), _screenIndicator, nameof(ScreenIndicator.UpdateWeaponAmmo)))
            {
                currentWeapon.Disconnect(nameof(Weapon.AmmoChangeSignal), _screenIndicator, nameof(ScreenIndicator.UpdateWeaponAmmo));
            }
        }
    }

    protected override void ConnectWeapon(Weapon currentWeapon, Weapon.WeaponOrder weaponOrder)
    {
        base.ConnectWeapon(currentWeapon, weaponOrder);

        if (_screenIndicator != null && currentWeapon != null && _hud != null)
        {
            // Register new weapon with UI if not connect (as cannot connect again)

            if (!currentWeapon.IsConnected(nameof(Weapon.AmmoChangeSignal), _screenIndicator, nameof(ScreenIndicator.UpdateWeaponAmmo)))
            {
                currentWeapon.Connect(nameof(Weapon.AmmoChangeSignal), _screenIndicator, nameof(ScreenIndicator.UpdateWeaponAmmo));
            }
            base.ConnectWeapon(currentWeapon, weaponOrder);
        }
    }

    public override void OnTargetAgentChange()
    {
        base.OnTargetAgentChange();

        if (_screenIndicator != null)
        {
            _screenIndicator.setCurrentTargetAgent(CurrentTargetAgent);
        }
    }


    public void gatherInput(float delta)
    {
        if (Input.IsActionJustReleased("inventory"))
        {
            // Show UI if UI is not visible, and also hide UI if UI is already visible 
            _inventoryUI.Activate(!_inventoryUI.Visible);
        }

        // Only read input if inventory is not open
        if (_inventoryUI == null || !_inventoryUI.Visible)
        {

            if (Input.IsActionJustReleased("zoom_in"))
            {
                _gameWorld.GetGameCamera().ZoomIn();
            }
            else if (Input.IsActionJustReleased("zoom_out"))
            {
                _gameWorld.GetGameCamera().ZoomOut();
            }

            NetworkSnapshotManager.PlayerInput playerInput = new NetworkSnapshotManager.PlayerInput();

            if (Input.IsActionPressed("turn_right"))
            {
                playerInput.Right = (int)(NetworkSnapshotManager.PlayerInput.InputAction.TRIGGER);
            }
            else
            {
                playerInput.Right = (int)(NetworkSnapshotManager.PlayerInput.InputAction.NOT_TRIGGER);
            }

            if (Input.IsActionPressed("turn_left"))
            {
                playerInput.Left = (int)(NetworkSnapshotManager.PlayerInput.InputAction.TRIGGER);
            }
            else
            {
                playerInput.Left = (int)(NetworkSnapshotManager.PlayerInput.InputAction.NOT_TRIGGER);
            }

            if (Input.IsActionPressed("forward"))
            {
                playerInput.Up = (int)(NetworkSnapshotManager.PlayerInput.InputAction.TRIGGER);
            }
            else
            {
                playerInput.Up = (int)(NetworkSnapshotManager.PlayerInput.InputAction.NOT_TRIGGER);
            }

            if (Input.IsActionPressed("backward"))
            {
                playerInput.Down = (int)(NetworkSnapshotManager.PlayerInput.InputAction.TRIGGER);
            }
            else
            {
                playerInput.Down = (int)(NetworkSnapshotManager.PlayerInput.InputAction.NOT_TRIGGER);
            }

            if (Input.IsActionJustReleased("trigger_target_selection"))
            {
                playerInput.TargetSelectionAction = (int)(NetworkSnapshotManager.PlayerInput.TargetAction.TRIGGER);
            }
            else if (Input.IsActionJustReleased("next_target"))
            {
                playerInput.TargetSelectionAction = (int)(NetworkSnapshotManager.PlayerInput.TargetAction.NEXT);
            }
            else if (Input.IsActionJustReleased("previous_target"))
            {
                playerInput.TargetSelectionAction = (int)(NetworkSnapshotManager.PlayerInput.TargetAction.PREVIOUS);
            }
            else
            {
                playerInput.TargetSelectionAction = (int)(NetworkSnapshotManager.PlayerInput.TargetAction.NOT_TRIGGER);
            }

            if (Input.IsActionPressed("reload_weapon"))
            {
                playerInput.RightWeaponAction = (int)(NetworkSnapshotManager.PlayerInput.InputAction.RELOAD);
                playerInput.LeftWeaponAction = (int)(NetworkSnapshotManager.PlayerInput.InputAction.RELOAD);
                playerInput.RemoteWeaponAction = (int)(NetworkSnapshotManager.PlayerInput.InputAction.RELOAD);
            }
            else
            {
                if (Input.IsActionPressed("left_weapon_fire"))
                {
                    playerInput.LeftWeaponAction = (int)(NetworkSnapshotManager.PlayerInput.InputAction.TRIGGER);
                }
                else
                {
                    playerInput.LeftWeaponAction = (int)(NetworkSnapshotManager.PlayerInput.InputAction.NOT_TRIGGER);
                }

                if (Input.IsActionPressed("right_weapon_fire"))
                {
                    playerInput.RightWeaponAction = (int)(NetworkSnapshotManager.PlayerInput.InputAction.TRIGGER);
                }
                else
                {
                    playerInput.RightWeaponAction = (int)(NetworkSnapshotManager.PlayerInput.InputAction.NOT_TRIGGER);
                }

                if (Input.IsActionPressed("remote_weapon_fire"))
                {
                    playerInput.RemoteWeaponAction = (int)(NetworkSnapshotManager.PlayerInput.InputAction.TRIGGER);
                }
                else
                {
                    playerInput.RemoteWeaponAction = (int)(NetworkSnapshotManager.PlayerInput.InputAction.NOT_TRIGGER);
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


            if (GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
            {
                _gameWorld.GetNetworkSnasphotManager().CacheInput(1, playerInput);
            }
            else
            {
                String inputData = "";
                inputData = inputData + playerInput.Right + ";";
                inputData = inputData + playerInput.Left + ";";
                inputData = inputData + playerInput.Up + ";";
                inputData = inputData + playerInput.Down + ";";
                inputData = inputData + playerInput.MousePosition.x + ";";
                inputData = inputData + playerInput.MousePosition.y + ";";
                inputData = inputData + playerInput.RightWeaponIndex + ";";
                inputData = inputData + playerInput.LeftWeaponIndex + ";";
                inputData = inputData + playerInput.RightWeaponAction + ";";
                inputData = inputData + playerInput.LeftWeaponAction + ";";
                inputData = inputData + playerInput.RemoteWeaponAction + ";";
                inputData = inputData + playerInput.TargetSelectionAction + ";";

                RpcUnreliableId(1, nameof(serverGetPlayerInput), inputData);
            }
        }
    }

    public override void Explode()
    {
        base.Explode();

        if (_screenIndicator != null)
        {
            _screenIndicator.SetActivate(false);
            _inventoryUI.Hide();
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

        if (GetTree().NetworkPeer == null || IsNetworkMaster())
        {
            gatherInput(delta);
        }

    }
}
