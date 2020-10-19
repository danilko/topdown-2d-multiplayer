using Godot;
using System;

public class TitleScreen : Control
{
    Network network;

    enum GameModeState { Server, Client }
    private GameModeState gameModeState;

    public override void _Ready()
    {
        network = (Network)GetNode("/root/NETWORK");
        network.Connect("ServerCreatedSignal", this, "readyToPlay");
        network.Connect("JoinSuccessSignal", this, "readyToPlay");
        network.Connect("JoinFailSignal", this, "joinFail");

        // Ensure the pause is false to recover from disconnection
        GetTree().Paused = false;
    }

    private void setPlayerInfo()
    {
        network.gamestateNetworkPlayer.name = ((LineEdit)GetNode("PanelPlayer/txtPlayerName")).Text;
        network.gamestateNetworkPlayer.team = ((LineEdit)GetNode("PanelPlayer/txtPlayerTeam")).Text;
    }

    private void readyToPlay()
    {
        ((GameStates)GetNode("/root/GAMESTATES")).next_level();
    }

    private void joinFail()
    {
        Label label = (Label)GetNode("PanelJoin/lblJoinStatus");
        label.Text = "Join Failed";
    }

    private void _onbtnCreateServerPanel()
    {
        ((Panel)GetNode("PanelJoin")).Visible = false;
        ((Panel)GetNode("PanelPlayer")).Visible = false;

        ((Panel)GetNode("PanelHost")).Visible = true;
    }

    private void _onbtnJoinServerPanel()
    {
        ((Panel)GetNode("PanelHost")).Visible = false;
        ((Panel)GetNode("PanelPlayer")).Visible = false;

        ((Panel)GetNode("PanelJoin")).Visible = true;
    }

    private void _onbtnExit()
    {
        GetTree().Quit();
    }

    private void createServer()
    {
        //  Gather values from the GUI and fill the network info
        LineEdit lineEdit = (LineEdit)GetNode("PanelHost/txtServerPort");
        int port = Int32.Parse(lineEdit.Text);

        SpinBox spinBox = (SpinBox)GetNode("PanelHost/txtMaxPlayers");
        int maxPlayers = (int)(spinBox.Value);

        lineEdit = (LineEdit)GetNode("PanelHost/txtServerName");
        String ServerName = lineEdit.Text;
        //And create the server, using the function previously added into the code 
        network.createServer(ServerName, port, maxPlayers);
    }

    private void joinServer()
    {
        LineEdit lineEdit = (LineEdit)GetNode("PanelJoin/txtJoinPort");
        int port = Int32.Parse(lineEdit.Text);
        lineEdit = (LineEdit)GetNode("PanelJoin/txtJoinIp");
        String ip = lineEdit.Text;
        network.joinServer(ip, port);
    }

    private void _onbtCreatePressed()
    {
        ((Panel)GetNode("PanelJoin")).Visible = false;
        ((Panel)GetNode("PanelHost")).Visible = false;

        gameModeState = GameModeState.Server;

        ((Panel)GetNode("PanelPlayer")).Visible = true;
    }

    private void _onbtJoinPressed()
    {
        ((Panel)GetNode("PanelJoin")).Visible = false;
        ((Panel)GetNode("PanelHost")).Visible = false;

        gameModeState = GameModeState.Client;

        ((Panel)GetNode("PanelPlayer")).Visible = true;
    }

    private void _onbtPlayerConfirmPressed()
    {
        ((Panel)GetNode("PanelJoin")).Visible = false;
        ((Panel)GetNode("PanelHost")).Visible = false;
        ((Panel)GetNode("PanelPlayer")).Visible = false;

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
        ((Panel)GetNode("PanelJoin")).Visible = false;
        ((Panel)GetNode("PanelHost")).Visible = false;
        ((Panel)GetNode("PanelPlayer")).Visible = false;
    }

    public override void _Input(InputEvent @event)
    {
        //if(@event.IsActionPressed("ui_select"))
        // {
        //    ((GameStates)GetNode("/root/GAMESTATES")).next_level();
        //}
    }

}
