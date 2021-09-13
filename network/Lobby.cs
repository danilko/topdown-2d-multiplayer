using Godot;
using System;

public class Lobby : Control
{
    Network _network;

    public enum GameModeState { Server, Client }

    private GameModeState gameModeState;

    private OptionButton playerTeams;

    private Godot.Collections.Array<TeamMapAISetting> _teamMapAISettings;
    private GridContainer _gridcontainerTeamManagement;

    private GameStates _gameStates;

    private WaitingRoom _waitingRoom;

    private Popup _popupNetork;

    private Popup _join;
    private Popup _player;
    private Popup _host;


    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _gameStates = (GameStates)GetNode("/root/GAMESTATES");

        _network = (Network)GetNode("/root/NETWORK");
        _network.Connect(nameof(Network.ServerCreatedSignal), this, nameof(enterWaitingRoom));
        _network.Connect(nameof(Network.JoinSuccessSignal), this, nameof(enterWaitingRoom));
        _network.Connect(nameof(Network.JoinFailSignal), this, nameof(_networkFail));
        _network.Connect(nameof(Network.ServerCreateFailSignal), this, nameof(_networkFail));

        _waitingRoom = (WaitingRoom)GetNode("WaitingRoom");
        _popupNetork = (Popup)GetNode("NetworkError");
        _join = (Popup)GetNode("Join");
        _player = (Popup)GetNode("Player");
        _host = (Popup)GetNode("Host");

        _gridcontainerTeamManagement = (GridContainer)_host.GetNode("MarginContainerTeamManagement/ScrollContainerTeamManagement/GridContainerTeamManagement");

        playerTeams = (OptionButton)_player.GetNode("optPlayerTeam");

        _teamMapAISettings = new Godot.Collections.Array<TeamMapAISetting>();

        _populatePlayerTeams();
    }

    private void setPlayerInfo()
    {
        _network.gamestateNetworkPlayer.name = ((LineEdit)GetNode("Player/txtPlayerName")).Text;
        _network.gamestateNetworkPlayer.team = playerTeams.Selected;
    }

    private void _populatePlayerTeams()
    {
        playerTeams.Connect("item_selected", this, "_playerTeamSelected");

        for (int index = 0; index < (int)(Team.TeamCode.NEUTRAL); index++)
        {
            Team.TeamCode team = (Team.TeamCode)index;
            playerTeams.AddItem("" + team);

            _populateTeamSettings(team);
        }

        // Pre Select the 0 index
        playerTeams.Select(0);
        _playerTeamSelected(0);
    }

    private void _populateTeamSettings(Team.TeamCode team)
    {
        TeamSettingPanel teamSettingPanel = (TeamSettingPanel)_host.GetNode("PanelTeamSetting").Duplicate();
        _gridcontainerTeamManagement.AddChild(teamSettingPanel);

        teamSettingPanel.Initialize(team);
        teamSettingPanel.Show();

        TeamMapAISetting teamMapAISetting = new TeamMapAISetting();
        teamMapAISetting.TeamCode = team;
        teamMapAISetting.Budget = teamSettingPanel.GetTeamBudget();
        teamMapAISetting.AutoSpawnMember = teamSettingPanel.GetTeamAutoSpawnMember();

        _teamMapAISettings.Add(teamMapAISetting);
    }

    private void _playerTeamSelected(int index)
    {
        TextureRect textureRect = (TextureRect)_player.GetNode("optTextrect");
        textureRect.Modulate = Team.TeamColor[index];
    }

    private void enterWaitingRoom()
    {
         _hideAllPopup();

        _waitingRoom.Initialize(gameModeState);
        _waitingRoom.Show();
    }

    private void onReadyGameStart()
    {
        ((GameStates)GetNode("/root/GAMESTATES")).EnterNetworkLevel();
    }

    private void _networkFail(String message)
    {
        _popupNetork.Show();
        ((RichTextLabel)_popupNetork.GetNode("CenterContainer/VBoxContainer/RichTextLabelMessage")).Text = message;
    }

    private void _onCreateServerSettingPressed()
    {
        _hideAllPopup();

        _host.Visible = true;
    }

    private void _onJoinServerSettingPressed()
    {
        _hideAllPopup();

        _join.Visible = true;
    }

    private void _collectTeamMapAPISettings()
    {
        foreach (TeamSettingPanel teamSettingPanel in _gridcontainerTeamManagement.GetChildren())
        {
            TeamMapAISetting teamMapAISetting = _teamMapAISettings[(int)teamSettingPanel.GetTeamCode()];
            teamMapAISetting.Budget = teamSettingPanel.GetTeamBudget();
            teamMapAISetting.AutoSpawnMember = teamSettingPanel.GetTeamAutoSpawnMember();
        }

        _gameStates.SetTeamMapAISettings(_teamMapAISettings);
    }

    private void _createServer()
    {
        //  Gather values from the GUI and fill the network info
        LineEdit lineEdit = (LineEdit)_host.GetNode("txtServerPort");
        int port = Int32.Parse(lineEdit.Text);

        SpinBox spinBox = (SpinBox)_host.GetNode("txtMaxPlayers");
        int maxPlayers = (int)(spinBox.Value);

        lineEdit = (LineEdit)_host.GetNode("txtServerName");
        String ServerName = lineEdit.Text;

        // update the settings
        _collectTeamMapAPISettings();

        //And create the server, using the function previously added into the code 
        _network.createServer(ServerName, port, maxPlayers);
    }

    private void _joinServer()
    {
        LineEdit lineEdit = (LineEdit)_join.GetNode("txtJoinPort");
        int port = Int32.Parse(lineEdit.Text);
        lineEdit = (LineEdit)_join.GetNode("txtJoinIp");
        String ip = lineEdit.Text;
        _network.joinServer(ip, port);

        // Prepare the network error message
        _networkFail("JOIN SERVER AT " + ip + ":" + port + " FAIL. PLEASE CHECK DETAIL AND TRY AGAIN.");
    }

    private void _onCreateServerPressed()
    {
        _hideAllPopup();
        gameModeState = GameModeState.Server;

        _player.Visible = true;
    }

    private void _onJoinServerPressed()
    {
        _hideAllPopup();
        gameModeState = GameModeState.Client;

        _player.Visible = true;
    }

    private void _hideAllPopup()
    {
        _join.Visible = false;
        _host.Visible = false;
        _player.Visible = false;
        _popupNetork.Visible = false;
        _waitingRoom.Visible = false;
    }

    private void _onPlayerConfirmPressed()
    {
        _hideAllPopup();

        // Properly set the local player information
        setPlayerInfo();

        if (gameModeState == GameModeState.Server)
        {
            _createServer();
        }
        else
        {
            _joinServer();
        }
    }


    private void _onExitLobbyPressed()
    {
        _gameStates.EnterTitleScreen();
    }
}
