using Godot;
using System;
using System.Collections.Generic;

public class WaitingRoom : Popup
{
    private GridContainer _gridContainer;

    private Network _network;

    private GameStates _gameStates;

    private Button _startMatch;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _gameStates = (GameStates)GetNode("/root/GAMESTATES");

        _gridContainer = (GridContainer)GetNode("CenterContainer/VBoxContainer/MarginContainer/ScrollContainer/GridContainer");
        _startMatch = (Button)GetNode("CenterContainer/VBoxContainer/StartMatchButton");

        _initializeNetwork();
    }

    public void Initialize(Lobby.GameModeState gameModeState)
    {
        _refreshPlayers();

        if (gameModeState == Lobby.GameModeState.Server)
        {
            _startMatch.Text = "START MATCH";
            _startMatch.Disabled = false;
        }
        else
        {
            _startMatch.Text = "WAITING FOR SERVER...";
            _startMatch.Disabled = true;
        }
    }

    private void _clearPlayerList()
    {
        // Clean up all nodes in list
        foreach (Node node in _gridContainer.GetChildren())
        {
            _gridContainer.RemoveChild(node);
            node.QueueFree();
        }
    }

    public void _onStartMatchPressed()
    {
        _startMatchSetup();
    }



    [Remote]
    private void _startMatchSetup()
    {
        // Notify other clients to start to join the map
        if (GetTree().IsNetworkServer())
        {
            Rpc(nameof(_startMatchSetup));
        }

        _gameStates.EnterNetworkLevel();

    }

    public void _refreshPlayers()
    {
        _clearPlayerList();

        HBoxContainer hBoxContainerReference = (HBoxContainer)GetNode("HBoxContainer");

        foreach (KeyValuePair<int, NetworkPlayer> player in _network.networkPlayers)
        {
            HBoxContainer hBoxContainer = (HBoxContainer)hBoxContainerReference.Duplicate();
            _gridContainer.AddChild(hBoxContainer);

            hBoxContainer.Visible = true;
            ((Label)hBoxContainer.GetNode("PlayerName")).Text = player.Value.name;
            ((TextureRect)hBoxContainer.GetNode("TextrectTeam")).Modulate = Team.TeamColor[(int)player.Value.team];
        }
    }

    private void _initializeNetwork()
    {
        _network = (Network)GetNode("/root/NETWORK");
        _network.Connect(nameof(Network.DisconnectedSignal), this, nameof(_refreshPlayers));
        _network.Connect(nameof(Network.PlayerListChangedSignal), this, nameof(_refreshPlayers));
    }


    //  // Called every frame. 'delta' is the elapsed time since the previous frame.
    //  public override void _Process(float delta)
    //  {
    //      
    //  }
}
