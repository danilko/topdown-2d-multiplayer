using Godot;
using System;
using System.Collections.Generic;

public class HUD : CanvasLayer
{
    Network network;

    bool lblMessage = false;

    private Dictionary<Weapon.WeaponOrder, WeaponControl> _weaponControls;

    private Control _gameControl;
    private MiniMap _miniMap;

    private Control _overallMessageControll;

    private CharacterDialog _characterDialog;

    private PopUpMessage _popUpMessage;

    private GameTimerManager _gameTimerManager;

    private GameTimerManager.GameTimerState _gameTimerState;

    private GameWorld _gameWorld;

    private Label _timerTickLabel;

    public override void _Ready()
    {

    }

    public void Initailize(GameWorld gameWorld)
    {
        _gameWorld = gameWorld;
        _gameTimerManager = _gameWorld.GetGameTimerManager();
        _gameTimerManager.Connect(nameof(GameTimerManager.GameTimerTickSignal), this, nameof(_onUpdateTimerTick));

        _gameTimerState = _gameTimerManager.GetGameTimerState();

        _timerTickLabel =  ((Label)GetNode("lblTimerStatus"));

        _characterDialog = (CharacterDialog)GetNode("CharacterDialog");

        _gameControl = (Control)(GetNode("GameControl"));
        _overallMessageControll = ((Control)GetNode("controlOverallMessage"));
        _overallMessageControll.Visible = false;

        _weaponControls = new Dictionary<Weapon.WeaponOrder, WeaponControl>();
        _weaponControls.Add(Weapon.WeaponOrder.Right, (WeaponControl)(_gameControl.GetNode("RightWeaponControl")));
        _weaponControls.Add(Weapon.WeaponOrder.Left, (WeaponControl)(_gameControl.GetNode("LeftWeaponControl")));

        _miniMap = (MiniMap)_gameControl.GetNode("MiniMap");
        _popUpMessage = (PopUpMessage)GetNode("PopUpMessage");

        _miniMap.Iniitialize(gameWorld.GetCapturableBaseManager());

        //_postProcess = (PostProcess)GetNode("PostProcess");

        _gameWorld.GetAgentSpawnManager().Connect(nameof(AgentSpawnManager.PlayerDefeatedSignal), this, nameof(_onPlayerDefeated));
        _gameWorld.GetAgentSpawnManager().Connect(nameof(AgentSpawnManager.PlayerCreateSignal), this, nameof(_onPlayerCreated));
        _gameWorld.GetAgentSpawnManager().Connect(nameof(AgentSpawnManager.AgentDefeatedSignal), this, nameof(_onAgentDefeated));
    }

    private void _onAgentDefeated(String agentId, String unitName, Team.TeamCode teamCode)
    {
        _miniMap.RemoveAgent(unitName);
        _popUpMessage.NotifyMessage("NOTIFICATION", unitName + " (" + teamCode + ") IS ELIMINATED");

    }

    public PopUpMessage GetPopUpMessage()
    {
        return _popUpMessage;
    }

    public MiniMap GetMiniMap()
    {
        return _miniMap;
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
        _gameControl.Visible = false;
        _overallMessageControll.Visible = true;

        ((AnimationPlayer)GetNode("AnimationPlayer")).Play("MessageAnnounce");
    }

    private void _onPlayerDefeated()
    {
        _overallMessageControll.Visible = false;

        _gameControl.Visible = true;
        lblMessage = false;
    }

    public void UpdateTeamUnitUsageAmount(int cost)
    {
        ((Label)GetNode("lblTeamUnitUsageAmount")).Text = "" + cost;
    }

    private void _onUpdateTimerTick(int time)
    {
        String message = _gameTimerManager.ConvertToDateFormat(time) + " " + _gameTimerState;

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

    public override void _PhysicsProcess(float delta)
    {
        if (lblMessage && Input.IsKeyPressed((int)Godot.KeyList.Space))
        {
            _overallMessageControll.Visible = false;
            lblMessage = false;
        }

        // COmment out for now as part of test
        //if (!_characterDialog.Visible && Input.IsKeyPressed((int)Godot.KeyList.Space))
        // {
        //     _characterDialog.Activate();
        // }
    }

}
