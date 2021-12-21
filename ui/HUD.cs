using Godot;
using System;
using System.Collections.Generic;

public class HUD : CanvasLayer
{
    bool lblMessage = false;

    private Dictionary<Weapon.WeaponOrder, WeaponControl> _weaponControls;

    private Control _gameControl;
    private MiniMap _miniMap;

    private Control _overallMessageControll;

    private CharacterDialog _characterDialog;

    private PopUpMessage _popUpMessage;

    private GameTimerManager _gameTimerManager;

    private GameWorld _gameWorld;

    private Label _timerTickLabel;

    private UnitLaunchSetupUI _unitLaunchSetupUI;
    private InGameControlUI _inGameControlUI;

    public override void _Ready()
    {
        _timerTickLabel = ((Label)GetNode("lblTimerStatus"));

        _characterDialog = (CharacterDialog)GetNode("CharacterDialog");

        _gameControl = (Control)(GetNode("GameControl"));
        _overallMessageControll = ((Control)GetNode("controlOverallMessage"));
        _overallMessageControll.Visible = false;

        _weaponControls = new Dictionary<Weapon.WeaponOrder, WeaponControl>();
        _weaponControls.Add(Weapon.WeaponOrder.Right, (WeaponControl)(_gameControl.GetNode("RightWeaponControl")));
        _weaponControls.Add(Weapon.WeaponOrder.Left, (WeaponControl)(_gameControl.GetNode("LeftWeaponControl")));

        _miniMap = (MiniMap)GetNode("MiniMap");
        _popUpMessage = (PopUpMessage)GetNode("PopUpMessage");

        _unitLaunchSetupUI = (UnitLaunchSetupUI)GetNode("UnitLaunchSetupUI");

        _inGameControlUI = (InGameControlUI)GetNode("InGameControlUI");
    }

    public void Initailize(GameWorld gameWorld)
    {
        _gameWorld = gameWorld;
        _gameTimerManager = _gameWorld.GetGameTimerManager();
        _gameTimerManager.Connect(nameof(GameTimerManager.GameTimerTickSignal), this, nameof(_onUpdateTimerTick));

        _miniMap.Iniitialize(gameWorld);

        _unitLaunchSetupUI.Initalize(_gameWorld, _miniMap);
        //_postProcess = (PostProcess)GetNode("PostProcess");

        _gameWorld.GetAgentSpawnManager().Connect(nameof(AgentSpawnManager.PlayerDefeatedSignal), this, nameof(_onPlayerDefeated));
        _gameWorld.GetAgentSpawnManager().Connect(nameof(AgentSpawnManager.PlayerCreatedSignal), this, nameof(_onPlayerCreated));
        _gameWorld.GetAgentSpawnManager().Connect(nameof(AgentSpawnManager.AgentDefeatedSignal), this, nameof(_onAgentDefeated));
        _gameWorld.GetAgentSpawnManager().Connect(nameof(AgentSpawnManager.AgentCreatedSignal), this, nameof(_onAgentCreated));

        _gameWorld.GetAgentSpawnManager().Connect(nameof(AgentSpawnManager.AgentConfigSignal), this, nameof(_onAgentConfig));

        _inGameControlUI.Initialize(_gameWorld);

        // Simulation will not have HUD
        if (_gameWorld.GetGameStateManager().GetGameStates().GetGameType() == GameStates.GameType.SIMULATION)
        {
            _miniMap.Hide();
            _popUpMessage.Hide();
            _timerTickLabel.Hide();
        }
    }

    private void _onAgentConfig(String unitID, Team.TeamCode teamCode)
    {
        // If simulation, no need to setup
        if (_gameWorld.GetGameStateManager().GetGameStates().GetGameType() == GameStates.GameType.SIMULATION)
        {
            return;
        }

        if (unitID.Contains(AgentSpawnManager.AgentPlayerPrefix) &&
        int.Parse(unitID.Replace(AgentSpawnManager.AgentPlayerPrefix, "")) == _gameWorld.GetNetworkSnasphotManager().GetNetwork().gamestateNetworkPlayer.net_id)
        {
            _setMapMode(MiniMap.MapMode.BIGMAP_SELECT);
            _unitLaunchSetupUI.EnableSetup(unitID, teamCode);
        }
    }

    private void _onAgentDefeated(String unitID, Team.TeamCode teamCode, String displayName)
    {
        if (unitID.Contains(AgentSpawnManager.AgentPlayerPrefix) &&
          int.Parse(unitID.Replace(AgentSpawnManager.AgentPlayerPrefix, "")) == _gameWorld.GetNetworkSnasphotManager().GetNetwork().gamestateNetworkPlayer.net_id)
        {
            _miniMap.Hide();
            _miniMap.RemovePlayer();
        }
        else
        {
            _miniMap.RemoveAgent(unitID);
        }

        _popUpMessage.NotifyMessage("NOTIFICATION", displayName + " (" + teamCode + ") IS ELIMINATED");
    }

    private void _onAgentCreated(String unitID, Team.TeamCode teamCode)
    {
        Agent agent = _gameWorld.GetTeamMapAIManager().GetTeamMapAIs()[(int)teamCode].GetUnit(unitID);

        if (agent.IsCurrentPlayer())
        {
            _miniMap.SetPlayer(agent);
            _setMapMode(MiniMap.MapMode.MINIMAP);
        }
        else
        {
            _miniMap.AddAgent(agent);
        }

        _popUpMessage.NotifyMessage("NOTIFICATION", agent.GetDisplayName() + " (" + teamCode + ") IS IDENTITIED");
    }

    public InventoryUI GetInventoryUI()
    {
        return (InventoryUI)GetNode("GameControl/InventoryUI");
    }

    public PopUpMessage GetPopUpMessage()
    {
        return _popUpMessage;
    }

    private void _onNetworkRateUpdate(String message)
    {
        // Updating the network rate for local machine
        ((Label)GetNode("lblNetworkRate")).Text = "Network Rate: " + message;
    }

    public void UpdateWeapon(ItemResource itemResource, Weapon.WeaponOrder weaponOrder, int weaponIndex)
    {
        _weaponControls[weaponOrder].UpdateWeapon(itemResource, weaponOrder, weaponIndex);
    }

    private void _onPlayerCreated()
    {
        _gameControl.Visible = true;
        _overallMessageControll.Visible = false;
    }

    private void _onPlayerDefeated()
    {
        _overallMessageControll.Visible = true;

        _gameControl.Visible = false;
        lblMessage = false;
    }

    public void UpdateTeamUnitUsageAmount(int cost)
    {
        ((Label)GetNode("lblTeamUnitUsageAmount")).Text = "" + cost;
    }

    private void _onUpdateTimerTick(int time)
    {
        String message = _gameTimerManager.ConvertToDateFormat(time) + " " + _gameTimerManager.GetGameTimerState();

        // If less than 60 seconds, modify color
        if (time < 60)
        {
            _timerTickLabel.Set("custom_colors/font_color", new Color("#ffc65b"));
        }
        else
        {
            _timerTickLabel.Set("custom_colors/font_color", new Color("#96ff5b"));
        }

        _timerTickLabel.Text = message;
    }

    private void _setMapMode(MiniMap.MapMode mapMode)
    {
        _miniMap.SetMapMode(mapMode);
        _miniMap.Show();

    }

    public override void _PhysicsProcess(float delta)
    {
        if (lblMessage && Input.IsKeyPressed((int)Godot.KeyList.Space))
        {
            _overallMessageControll.Visible = false;
            lblMessage = false;
        }

        if (_gameControl.Visible && Input.IsKeyPressed((int)Godot.KeyList.Tab))
        {
            _setMapMode(MiniMap.MapMode.BIGMAP);
        }
        else if (_gameControl.Visible)
        {
            _setMapMode(MiniMap.MapMode.MINIMAP);
        }

        
        if (!_inGameControlUI.Visible && Input.IsActionJustReleased("ui_cancel") && _gameWorld.GetGameStateManager().GetGameStates().GetGameType() != GameStates.GameType.SIMULATION)
        {
            Input.SetMouseMode(Input.MouseMode.Visible);
            _inGameControlUI.PopupCentered();
        }
        else if (_inGameControlUI.Visible && Input.IsActionJustReleased("ui_cancel"))
        {
            Input.SetMouseMode(Input.MouseMode.Hidden);
            _inGameControlUI.Hide();
        }


        // COmment out for now as part of test
        //if (!_characterDialog.Visible && Input.IsKeyPressed((int)Godot.KeyList.Space))
        // {
        //     _characterDialog.Activate();
        // }
    }

}
