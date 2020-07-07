using Godot;
using System;
using System.Collections.Generic;

public class Map : Node2D
{

    private GameStates gameStates;
    private class SpwanInfo
    {
        public int spawn_index { get; set; }
        public NetworkPlayer networkPlayer { get; set; }
    }

    private class SpawnBot
    {
        public SpawnBot(String name, PackedScene packedScene)
        {
            this.name = name;
            this.packedScene = packedScene;
        }

        public String name;
        public PackedScene packedScene;
    }


    int spawned_bots = 0;

    Dictionary<int, SpawnBot> spawnBots = new Dictionary<int, SpawnBot>();

    Network network;

    float currentTime;

    [Signal]
    public delegate void SnapshotReceivedSignal();

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        gameStates = (GameStates)GetNode("/root/GAMESTATES");

        Input.SetCustomMouseCursor(GD.Load("res://assets/ui/blue_cross.png"), Input.CursorShape.Arrow, new Vector2(16, 16));
        //  AudioManager audioManager = (AudioManager)GetNode("/root/AUDIOMANAGER");
        //  audioManager.playMusic(musicClip);

        network = (Network)GetNode("/root/NETWORK");
        network.Connect("DisconnectedSignal", this, nameof(onDisconnected));

        if (GetTree().IsNetworkServer())
        {
            network.Connect("PlayerRemovedSignal", this, "onPlayerRemoved");
        }

        // Spawn the players
        if (GetTree().IsNetworkServer())
        {
            spwanPlayer(convertToString(network.gamestateNetworkPlayer, 1));
            // The amount doesn't matter because it will be calculated in the function body
            syncBots(-1);
        }
        else
        {
            RpcId(1, nameof(spwanPlayer), convertToString(network.gamestateNetworkPlayer, -1));
            RpcId(1, nameof(syncBots), -1);
        }

    }

    private void onPlayerRemoved(int id)
    {
        despawnPlayer(id);
        syncBots(-1);
    }

    private void onDisconnected()
    {
        gameStates.restart();
    }

    private void despawnPlayer(int id)
    {

        if (GetTree().IsNetworkServer())
        {
            foreach (KeyValuePair<int, NetworkPlayer> item in network.networkPlayers)
            {
                // Skip disconnecte player and server from replication code
                if (item.Key == id || id == 1)
                {
                    continue;
                }
                // Replicate despawn into currently iterated player
                RpcId(id, "despawnPlayer", id);
            }
        }
        // Try to locate the player actor
        Node playerNode = GetNode("client_" + id);

        if (playerNode == null)
        {
            GD.Print("Cannoot remove invalid node from tree");
            return;
        }

        // Mark the node for deletion
        playerNode.QueueFree();
    }


    private SpwanInfo convertToObject(String info)
    {
        SpwanInfo spwanInfo = new SpwanInfo();
        spwanInfo.networkPlayer = network.convertToObject(info);
        spwanInfo.spawn_index = Int32.Parse(info.Split(";")[2]);
        return spwanInfo;
    }
    private String convertToString(NetworkPlayer networkPlayer, int spawn_index)
    {
        return network.convertToString(networkPlayer) + ";" + spawn_index;
    }


    [Remote]
    private void spwanPlayer(String info)
    {
        SpwanInfo spwanInfo = convertToObject(info);
        NetworkPlayer pininfo = network.convertToObject(info);
        int spawnIndex = spwanInfo.spawn_index;

        // If the spawn_index is -1 then we define it based on the size of the player list
        if (spawnIndex == -1)
        {
            spawnIndex = network.networkPlayers.Count;
        }

        if (GetTree().IsNetworkServer() && pininfo.net_id != 1)
        {
            // We are on the server and the requested spawn does not belong to the server
            // Iterate through the connected players
            int s_index = 1; // Will be used as spawn index

            foreach (KeyValuePair<int, NetworkPlayer> item in network.networkPlayers)
            {
                // Spawn currently iterated player within the new player's scene, skipping the new player for now
                if (item.Key != pininfo.net_id)
                {
                    RpcId(pininfo.net_id, nameof(spwanPlayer), convertToString(item.Value, s_index));
                }

                // Spawn the new player within the currently iterated player as long it's not the server
                // Because the server's list already contains the new player, that one will also get itself!
                if (item.Key != 1)
                {
                    RpcId(item.Key, nameof(spwanPlayer), convertToString(pininfo, spawnIndex));
                }

                s_index++;
            }
        }

        // Load the scene and create an instance
        Player client;

        client = (Player)((PackedScene)GD.Load("res://tanks/Player.tscn")).Instance();

        Node2D nodeSpawnPoint = (Node2D)GetNode("SpawnPoint" + spawnIndex);
        client.Position = nodeSpawnPoint.Position;

        client.Connect("ShootSingal", this, "_onTankShoot");
        client.Name = "client_" + pininfo.net_id;
        client.setUnitName(pininfo.name);


        // If this actor does not belong to the server, change the network master accordingly
        if (pininfo.net_id != 1)
        {
            client.SetNetworkMaster(pininfo.net_id);
        }

        AddChild(client);

        // If this actor is the current client controlled, add camera and attach HUD
        if (pininfo.net_id == network.gamestateNetworkPlayer.net_id)
        {
            Camera2D camera2D = new Camera2D();
            camera2D.Name = "Camera2D";
            client.AddChild(camera2D);
            client.Connect("AmmoChangedSignal", GetNode("HUD"), "_updateAmmoBar");
            // client.Connect("DeadSignal", this, "_on_Player_Dead");
            client.Connect("HealthChangedSignal", GetNode("HUD"), "_updateHealthBar");
            _setCameraLimit();
        }
    }

    [Remote]
    private void syncBots(int bot_count)
    {
        if (GetTree().IsNetworkServer())
        {
            // Calculate the target amount of spawned bots
            bot_count = network.serverinfo.max_players - network.networkPlayers.Count;
            Rpc(nameof(syncBots), bot_count);
        }

        if (spawnBots.Count > bot_count)
        {
            while (spawnBots.Count > bot_count)
            {
                Node node = GetNode("Paths/Path2D/PathFollow2D/" + spawnBots[spawnBots.Count - 1].name);


                if (node == null)
                {
                    GD.Print("Must remove bots from game but cannot find its node");
                    spawnBots.Remove(spawnBots.Count - 1);
                    continue;
                }

                node.QueueFree();
                spawnBots.Remove(spawnBots.Count - 1);
            }
        }
        else if (spawnBots.Count < bot_count)
        {
            // We have less bots than the target count - must add some
            //  Since every single bot uses the exact same scene path we can cahce it's loaded scene here
            // otherwise, we would have to move the following code into the while loop and change the dictionary
            // key ID to point into the correct bot info. In this case we are pointing to the 1

            while (spawnBots.Count < bot_count)
            {
                spawnBots.Add(spawnBots.Count, new SpawnBot("bot_" + spawnBots.Count, (PackedScene)GD.Load("res://tanks/Enemy.tscn")));

                Enemy bot = (Enemy)((PackedScene)GD.Load("res://tanks/Enemy.tscn")).Instance();

                bot.Connect("ShootSingal", this, "_onTankShoot");
                bot.Name = spawnBots[spawnBots.Count - 1].name;
                bot.setUnitName(spawnBots[spawnBots.Count - 1].name);

                bot.SetNetworkMaster(1);

                this.GetNode("Paths/Path2D/PathFollow2D").AddChild(bot);
            }
        }
    }

    private void _setCameraLimit()
    {
        TileMap tileMap = (TileMap)GetNode("Ground");
        Rect2 mapLimit = tileMap.GetUsedRect();
        Vector2 mapCellSize = tileMap.CellSize;

        Camera2D playerCamera = (Camera2D)(GetNode("client_" + network.gamestateNetworkPlayer.net_id + "/Camera2D"));
        playerCamera.Current = true;
        playerCamera.Zoom = new Vector2(1.4f, 1.4f);
        playerCamera.LimitLeft = (int)(mapLimit.Position.x * mapCellSize.x);
        playerCamera.LimitRight = (int)(mapLimit.End.x * mapCellSize.x);
        playerCamera.LimitTop = (int)(mapLimit.Position.y * mapCellSize.y);
        playerCamera.LimitBottom = (int)(mapLimit.End.y * mapCellSize.y);
    }

    private void _onTankShoot(PackedScene bullet, Vector2 _position, Vector2 _direction, Node2D target = null)
    {
        Bullet newBullet = (Bullet)bullet.Instance();
        AddChild(newBullet);
        newBullet.start(_position, _direction, target);
    }

    private void _onPlayerDead()
    {
        ((GameStates)GetNode("/root/GAMESTATES")).restart();
    }
}
