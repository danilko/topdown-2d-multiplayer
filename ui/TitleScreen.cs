using Godot;
using System;

public class TitleScreen : Control
{
    Network network;

    enum GameModeState { Server, Client }
    private GameModeState gameModeState;

    private OptionButton playerTeams;

    public override void _Ready()
    {
        network = (Network)GetNode("/root/NETWORK");
        network.Connect("ServerCreatedSignal", this, "readyToPlay");
        network.Connect("JoinSuccessSignal", this, "readyToPlay");
        network.Connect("JoinFailSignal", this, "joinFail");

        // Ensure the pause is false to recover from disconnection
        GetTree().Paused = false;

        playerTeams = (OptionButton)GetNode("CanvasLayer/PanelPlayer/optPlayerTeam");
        _populatePlayerTeams();
    }

    private void setPlayerInfo()
    {
        network.gamestateNetworkPlayer.name = ((LineEdit)GetNode("CanvasLayer/PanelPlayer/txtPlayerName")).Text;
        network.gamestateNetworkPlayer.team = playerTeams.Selected;
    }

    private void _populatePlayerTeams()
    {
        playerTeams.Connect("item_selected", this, "_playerTeamSelected");

        for (int index = 0; index < (int)(Team.TeamCode.NEUTRAL); index++)
        {


            Team.TeamCode team = (Team.TeamCode)index;
            playerTeams.AddItem("" + team);
        }

        // Pre Select the 0 index
        playerTeams.Select(0);
        _playerTeamSelected(0);
    }

    private void _playerTeamSelected(int index)
    {
        TextureRect textureRect = (TextureRect)GetNode("CanvasLayer/PanelPlayer/optTextrect");
        textureRect.Modulate = Team.TeamColor[index];
    }

    private void readyToPlay()
    {
        ((GameStates)GetNode("/root/GAMESTATES")).next_level();
    }

    private void joinFail()
    {
        Label label = (Label)GetNode("CanvasLayer/PanelJoin/lblJoinStatus");
        label.Text = "Join Failed";
    }

    private void _onbtnCreateServerPanel()
    {
        ((Panel)GetNode("CanvasLayer/PanelJoin")).Visible = false;
        ((Panel)GetNode("CanvasLayer/PanelPlayer")).Visible = false;

        ((Panel)GetNode("CanvasLayer/PanelHost")).Visible = true;
    }

    private void _onbtnJoinServerPanel()
    {
        ((Panel)GetNode("CanvasLayer/PanelHost")).Visible = false;
        ((Panel)GetNode("CanvasLayer/PanelPlayer")).Visible = false;

        ((Panel)GetNode("CanvasLayer/PanelJoin")).Visible = true;
    }

    private void _onbtnExit()
    {
        GetTree().Quit();
    }

    private void createServer()
    {
        //  Gather values from the GUI and fill the network info
        LineEdit lineEdit = (LineEdit)GetNode("CanvasLayer/PanelHost/txtServerPort");
        int port = Int32.Parse(lineEdit.Text);

        SpinBox spinBox = (SpinBox)GetNode("CanvasLayer/PanelHost/txtMaxPlayers");
        int maxPlayers = (int)(spinBox.Value);

        lineEdit = (LineEdit)GetNode("CanvasLayer/PanelHost/txtServerName");
        String ServerName = lineEdit.Text;
        //And create the server, using the function previously added into the code 
        network.createServer(ServerName, port, maxPlayers);
    }

    private void joinServer()
    {
        LineEdit lineEdit = (LineEdit)GetNode("CanvasLayer/PanelJoin/txtJoinPort");
        int port = Int32.Parse(lineEdit.Text);
        lineEdit = (LineEdit)GetNode("CanvasLayer/PanelJoin/txtJoinIp");
        String ip = lineEdit.Text;
        network.joinServer(ip, port);
    }

    private void _onbtCreatePressed()
    {
        ((Panel)GetNode("CanvasLayer/PanelJoin")).Visible = false;
        ((Panel)GetNode("CanvasLayer/PanelHost")).Visible = false;

        gameModeState = GameModeState.Server;

        ((Panel)GetNode("CanvasLayer/PanelPlayer")).Visible = true;
    }

    private void _onbtJoinPressed()
    {
        ((Panel)GetNode("CanvasLayer/PanelJoin")).Visible = false;
        ((Panel)GetNode("CanvasLayer/PanelHost")).Visible = false;

        gameModeState = GameModeState.Client;

        ((Panel)GetNode("CanvasLayer/PanelPlayer")).Visible = true;
    }

    private void _onbtPlayerConfirmPressed()
    {
        ((Panel)GetNode("CanvasLayer/PanelJoin")).Visible = false;
        ((Panel)GetNode("CanvasLayer/PanelHost")).Visible = false;
        ((Panel)GetNode("CanvasLayer/PanelPlayer")).Visible = false;

        // Properly set the local player information
        setPlayerInfo();

        if (gameModeState == GameModeState.Server)
        {
            createServer();
        }
        else
        {
            joinServer();
        }
    }


    private void _onbtCancelPressed()
    {
        ((Panel)GetNode("CanvasLayer/PanelJoin")).Visible = false;
        ((Panel)GetNode("CanvasLayer/PanelHost")).Visible = false;
        ((Panel)GetNode("CanvasLayer/PanelPlayer")).Visible = false;
    }

    public override void _Input(InputEvent @event)
    {
        //if(@event.IsActionPressed("ui_select"))
        // {
        //    ((GameStates)GetNode("/root/GAMESTATES")).next_level();
        //}
    }

}
