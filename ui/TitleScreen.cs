using Godot;
using System;

public class TitleScreen : Control
{
    Network network;

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

    private void onbtCreatePressed()
    {
        // Properly set the local player information
        setPlayerInfo();

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

    private void onbtJoinPressed()
    {
        // Properly set the local player information
        setPlayerInfo();

        LineEdit lineEdit = (LineEdit)GetNode("PanelJoin/txtJoinPort");
        int port = Int32.Parse(lineEdit.Text);
        lineEdit = (LineEdit)GetNode("PanelJoin/txtJoinIp");
        String ip = lineEdit.Text;
        network.joinServer(ip, port);
    }

    public override void _Input(InputEvent @event)
    {
        //if(@event.IsActionPressed("ui_select"))
        // {
        //    ((GameStates)GetNode("/root/GAMESTATES")).next_level();
        //}
    }

}
