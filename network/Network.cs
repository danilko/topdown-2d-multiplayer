using Godot;
using System;
using System.Collections.Generic;

public class Network : Node
{

    //when server is successfully created
    [Signal]
    public delegate void ServerCreatedSignal();
    // when server create fail
    [Signal]
    public delegate void ServerCreateFailSignal();
    //  When the peer successfully joins a server
    [Signal]
    public delegate void JoinSuccessSignal();
    // Failed to join a server
    [Signal]
    public delegate void JoinFailSignal();

    // Player List changed
    [Signal]
    public delegate void PlayerListChangedSignal();

    // A player has been removed from the list
    [Signal]
    public delegate void PlayerRemovedSignal();

    // Outside code can act to disconnections from the server
    [Signal]
    public delegate void DisconnectedSignal();

    [Signal]
    public delegate void PingUpdatedSignal();

    const float PINGINTERVAL = 1.0f;          // Wait one second between ping requests
    const float PINGTIMEOUT = 5.0f;           // Wait 5 seconds before considering a ping request as lost

    public class PingEntry
    {
        public Timer timer = new Timer();  // Timer object to control the ping/pong loop
        public float signature = 0;          // Used to match ping/pong packets
        public float packetLost = 0;         // Count number of lost packets
        public float lastPing = 0;          // Last measured time taken to get an answer from the peer
    }
    public Dictionary<int, PingEntry> pingEntries = new Dictionary<int, PingEntry>();

    public NetworkPlayer gamestateNetworkPlayer = new NetworkPlayer();

    private String hostType;

    public class ServerInfo
    {
        public String name = "Server";      // Holds the name of the server
        public int max_players = 0;      // Maximum allowed connections
        public int used_port = 0;         // Listening port
    }

    public ServerInfo serverinfo { get; set; }

    public Dictionary<int, NetworkPlayer> networkPlayers = new Dictionary<int, NetworkPlayer>();

    public override void _Ready()
    {
        GetTree().Connect("network_peer_disconnected", this, nameof(_on_player_disconnected));
        GetTree().Connect("network_peer_connected", this, nameof(_on_player_connected));
        GetTree().Connect("connected_to_server", this, nameof(_on_connected_to_server));
        GetTree().Connect("connection_failed", this, nameof(_on_connection_failed));
        GetTree().Connect("server_disconnected", this, nameof(_on_disconnected_from_server));

        serverinfo = new ServerInfo();
    }

    public void createServer(String serverName, int port, int max_players)
    {
        // Clear out any previous states remain
        closeServer();

        hostType = "Server";
        serverinfo.name = serverName;
        serverinfo.used_port = port;
        serverinfo.max_players = max_players;

        // Initialize the networking system
        NetworkedMultiplayerENet peer = new NetworkedMultiplayerENet();
        peer.CompressionMode = NetworkedMultiplayerENet.CompressionModeEnum.Zstd;

        // Try to create the server
        if (peer.CreateServer(serverinfo.used_port, serverinfo.max_players) != Error.Ok)
        {
            // Tell the server crate fail
            EmitSignal(nameof(ServerCreateFailSignal), "SERVER CREATE FAIL AT " + serverName + ":" + port + ". PLEASE CHECK DETAIL AND TRY AGAIN.");
            return;
        }

        // 	# Assign it into the tree
        GetTree().NetworkPeer = peer;
        // Tell the server has been created successfully
        EmitSignal(nameof(ServerCreatedSignal));

        registerPlayer(gamestateNetworkPlayer.ToString());
    }

    public void closeServer()
    {
        // Unregister all player except masters
        foreach (KeyValuePair<int, NetworkPlayer> item in networkPlayers)
        {
            if (item.Key != 1)
            {
                // Unregister the player from the server's list
                ((NetworkedMultiplayerENet)GetTree().NetworkPeer).DisconnectPeer(item.Key);
            }
        }

        networkPlayers.Clear();

        // Close network server
        if (GetTree().NetworkPeer != null)
        {
            GetTree().NetworkPeer.Dispose();
        }
        GetTree().NetworkPeer = null;
    }

    public void joinServer(String ip, int port)
    {
        hostType = "Client";
        NetworkedMultiplayerENet peer = new NetworkedMultiplayerENet();
        peer.CompressionMode = NetworkedMultiplayerENet.CompressionModeEnum.Zstd;

        if (peer.CreateClient(ip, port) != Error.Ok)
        {
            EmitSignal(nameof(JoinFailSignal), "JOIN SERVER AT " + ip + ":" + port + " FAIL. PLEASE CHECK DETAIL AND TRY AGAIN.");
            return;
        }

        GetTree().NetworkPeer = peer;
    }


    // Peer trying to connect to server is notified on success
    private void _on_connected_to_server()
    {

        EmitSignal(nameof(JoinSuccessSignal));

        // Update the player_info dictionary with the obtained unique network ID
        gamestateNetworkPlayer.net_id = GetTree().GetNetworkUniqueId();
        GD.Print(hostType + ": Player " + gamestateNetworkPlayer.net_id + "connected _on_connected_to_server");

        RpcId(1, nameof(registerPlayer), gamestateNetworkPlayer.ToString());

        // And register itself on the local list
        registerPlayer(gamestateNetworkPlayer.ToString());
    }

    // Everyone gets notified whenever a new client joins the server
    private void _on_player_connected(int id)
    {
        if (GetTree().IsNetworkServer())
        {
            // Send the server info to the player
            RpcId(id, nameof(getServerInfo), serverinfo.name + ";" + serverinfo.max_players);
        }

    }

    // Everyone gets notified whenever someone disconnects from the server
    private void _on_player_disconnected(int id)
    {
        GD.Print(hostType + ":Player " + id + " disconnected from server");

        // Update the player tables
        if (GetTree().IsNetworkServer())
        {
            // Unregister the player from the server's list
            unregisterPlayer(id);
            //  Then on all remaining peers
            Rpc(nameof(unregisterPlayer), id);
        }
    }

    // Peer trying to connect to server is notified on failure
    private void _on_connection_failed()
    {
        EmitSignal(nameof(JoinFailSignal));
        GetTree().NetworkPeer = null;
    }

    [Remote]
    private void getServerInfo(String info)
    {
        if (!GetTree().IsNetworkServer())
        {
            serverinfo.name = info.Split(";")[0];
            serverinfo.max_players = int.Parse(info.Split(";")[1]);
        }
    }

    // Peer is notified when disconnected from server
    private void _on_disconnected_from_server()
    {
        GD.Print("Disconnected from server");

        // Stop processing any node in the world, so the client remains responsive
        GetTree().Paused = true;

        // Clear the network object
        GetTree().NetworkPeer = null;

        // Clear the internal player list
        networkPlayers.Clear();
        // Reset the player info network ID
        gamestateNetworkPlayer.net_id = 1;

        EmitSignal(nameof(DisconnectedSignal));

        networkPlayers.Clear();
        // Reset the player info network ID
        gamestateNetworkPlayer.net_id = 1;
    }

    [Remote]
    private void registerPlayer(String info)
    {
        NetworkPlayer pininfo = new NetworkPlayer(info);

        GD.Print(hostType + ": Register player preapre for " + pininfo.net_id);

        if (GetTree().IsNetworkServer())
        {
            // We are on the server, so distribute the player list information throughout the connected players
            foreach (KeyValuePair<int, NetworkPlayer> item in networkPlayers)
            {
                GD.Print(hostType + ": Register player info send to " + pininfo.net_id + " with" + item.Value.net_id);
                // Send currently iterated player info to the new player
                RpcId(pininfo.net_id, nameof(registerPlayer), item.Value.ToString());

                // Send new player info to currently iterated player, skipping the server (which will get the info shortly)
                if (item.Key != 1)
                {
                    GD.Print(hostType + ": Register player info send to " + item.Key + " with" + pininfo.net_id);
                    RpcId(item.Key, nameof(registerPlayer), pininfo.ToString());
                }
            }
        }


        // Now to code that will be executed regardless of being on client or server
        GD.Print(hostType + ": Registering player" + pininfo.name + " (" + pininfo.net_id + ") to internal player table");
        if (!networkPlayers.ContainsKey(pininfo.net_id))
        {
            networkPlayers.Add(pininfo.net_id, pininfo);  //Create the player entry in the dictionary
        }

        EmitSignal(nameof(PlayerListChangedSignal));    // And notify that the player list has been changed
    }

    [Remote]
    private void unregisterPlayer(int id)
    {

        GD.Print(hostType + ": Removing player " + id + " from internal table");

        // Remove the player from the list
        networkPlayers.Remove(id);

        // And notify the list has been changed
        EmitSignal(nameof(PlayerListChangedSignal));
        // Emit the signal that is meant to be intercepted only by the server
        EmitSignal(nameof(PlayerRemovedSignal), id);
    }
}
