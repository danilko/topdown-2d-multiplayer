using Godot;
using System;
using System.Collections.Generic;

using System.IO;
using System.IO.Compression;
using System.Text;

public class GameWorld : Node2D
{

    [Signal]
    private delegate void SnapshotReceivedSignal();

    [Signal]
    private delegate void NeworkRateUpdateSignal();

    [Signal]
    private delegate void PlayerDefeatedSignal();

    [Signal]
    private delegate void WaitingPeriodSignal();

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

    public class ClientData : Godot.Object
    {
        public String id;
        public Vector2 position;
        public float rotation;
        public bool primaryWepaon;
        public bool secondaryWepaon;
        public int health;
        public int primaryWeaponIndex;
        public int secondaryWeaponIndex;
    }

    public class Snapshot : Godot.Object
    {
        public int signature;
        public Godot.Collections.Dictionary playerData = new Godot.Collections.Dictionary();
        public Godot.Collections.Dictionary botData = new Godot.Collections.Dictionary();
    }



    public class ClientState
    {
        public Vector2 fromPosition;
        public float fromRotation;
        public Vector2 toPosition;
        public float toRotation;
        public bool primaryWepaon;
        public bool secondaryWepaon;
        public int health;
        public float time;
        public Node2D node;
    }

    Dictionary<String, ClientState> clientStates = new Dictionary<String, ClientState>();

    private Godot.Collections.Dictionary<String, Vector2> obstacles = new Godot.Collections.Dictionary<String, Vector2>();
    private Godot.Collections.Array obstaclesDestroyed = new Godot.Collections.Array();

    int spawned_bots = 0;

    Dictionary<String, SpawnBot> spawnBots = new Dictionary<String, SpawnBot>();
    Dictionary<String, SpawnBot> spawnPlayers = new Dictionary<String, SpawnBot>();

    Network network;

    long botCounter = 0;
    private long MAX_BOT_COUNT = 100;

    float currentTime;

    // The "signature" (timestamp) added into each generated state snapshot
    // int max is 2,147,483,647 
    // 2,147,483,647 / 60 (snapshots) / 60 (seconds => 1 min) /60 (mins = > 1 hr) /24 (24 hrs => 1 day) = 414 days (before the snapshot expire)
    // So as long as this round of games end in 1 year, the snapshot signature will be unique
    int snapshotSignature = 1;

    // The signature of the last snapshot received
    int lastSnapshotSignature = 0;

    private Navigation2D navigation2D;

    private AStar aStar;

    private Godot.RandomNumberGenerator random;

    private RaycastAStar aStarSolver;

    // Called when the node enters the scene tree for the first time.

    private float currentNetworkBytes = 0;
    private float currentNetworkSnapshots = 0;
    private float currentAppliedNetworkSnapshots = 0;

    private bool waitingPeriod;

    [Export]
    private int MaxWaitingTime = 15;

    [Export]
    private int MaxGameTime = 3600;

    // Use as tick to track countdown time
    private int internalTimer;

    private Timer timer;

    public override void _Ready()
    {
        buildObstacles();

        random = new RandomNumberGenerator();
        gameStates = (GameStates)GetNode("/root/GAMESTATES");

        Navigation2D navigation2D = (Navigation2D)GetNode("Navigation2D");

        Input.SetCustomMouseCursor(GD.Load("res://assets/ui/blue_cross.png"), Input.CursorShape.Arrow, new Vector2(16, 16));
        //  AudioManager audioManager = (AudioManager)GetNode("/root/AUDIOMANAGER");
        //  audioManager.playMusic(musicClip);

        // After receiving and fully decoding a new snapshot, apply it to the game world
        this.Connect(nameof(SnapshotReceivedSignal), this, nameof(applySnapshot));

        network = (Network)GetNode("/root/NETWORK");
        network.Connect("DisconnectedSignal", this, nameof(onDisconnected));

        if (GetTree().IsNetworkServer())
        {
            network.Connect("PlayerRemovedSignal", this, "onPlayerRemoved");
        }

        // Spawn the players
        if (GetTree().IsNetworkServer())
        {
            aStarSolver = new RaycastAStar();
            spwanPlayer(convertToString(network.gamestateNetworkPlayer, 1));
            // The amount doesn't matter because it will be calculated in the function body
            syncBots(-1);
        }
        else
        {
            RpcId(1, nameof(spwanPlayer), convertToString(network.gamestateNetworkPlayer, -1));
        }


        // Update network flow
        this.Connect(nameof(NeworkRateUpdateSignal), GetNode("HUD"), "_onNetworkRateUpdate");
        this.Connect(nameof(PlayerDefeatedSignal), GetNode("HUD"), "_onPlayerDefeatedMessage");

        this.Connect("PlayerListChangedSignal", GetNode("HUD"), "_onPlayerDefeatedMessage");

        this.Connect(nameof(WaitingPeriodSignal), GetNode("HUD"), "_onUpdateTimer");

        // Update playerlist
        EmitSignal("PlayerListChangedSignal");

        waitingPeriod = true;

        // Set the timer on server to do waiting period count down
        if (GetTree().IsNetworkServer())
        {
            timer = (Timer)GetNode("Timer");
            internalTimer = MaxWaitingTime;
            timer.WaitTime = 1;
            timer.Connect("timeout", this, nameof(waitingPeriodTimerTimeout));
            timer.Start();
        }

    }

    public Vector2 getSpawnPointPosition(int spawnPointIndex)
    {
        if (spawnPointIndex >= GetNode("SpawnPoints").GetChildCount())
        {
            spawnPointIndex = GetNode("SpawnPoints").GetChildCount() - 1; ;
        }

        return ((Node2D)GetNode("SpawnPoints/SpawnPoint_" + spawnPointIndex)).GlobalPosition;
    }

    public int getNextSpawnIndex(int spawnIndex)
    {
        int nextSpawnIndex = spawnIndex + 1;

        if (nextSpawnIndex >= GetNode("SpawnPoints").GetChildCount())
        {
            nextSpawnIndex = 0;
        }

        return nextSpawnIndex;
    }

    // Cacluate network rate base on send bytes, received snapshots, applied snapshots
    private void _onNetworkRateTimerUpdate()
    {
        // Convert from bytes to Kb (kio bits)
        String message = (currentNetworkBytes / (8 * 1000)) + " Kb/s, "
        + currentNetworkSnapshots + " obtained snapshots/s, "
        + currentAppliedNetworkSnapshots + " applied snapshots/s";

        EmitSignal(nameof(NeworkRateUpdateSignal), message);

        currentNetworkBytes = 0;
        currentNetworkSnapshots = 0;
        currentAppliedNetworkSnapshots = 0;
    }

    // Build obstacles base on tile map
    // Will not build obstacles on the road automatically
    public void buildObstacles()
    {
        TileMap tilemap = (TileMap)GetNode("Navigation2D/Ground");

        Vector2 cellSize = tilemap.CellSize;
        Rect2 usedRect = tilemap.GetUsedRect();

        int startPointX = (int)usedRect.Position.x;
        int startPointY = (int)usedRect.Position.y;

        int maxLengthX = (int)usedRect.Size.x;
        int maxLengthY = (int)usedRect.Size.y;

        for (int xIndex = startPointX; xIndex < maxLengthX; xIndex++)
        {
            for (int yIndex = startPointY; yIndex < maxLengthY; yIndex++)
            {
                Vector2 position;
                Obstacle.Items item = Obstacle.Items.remain;

                if (tilemap.GetCell(xIndex, yIndex) == 0)
                {
                    item = Obstacle.Items.crate_wood;
                }
                else if (tilemap.GetCell(xIndex, yIndex) == 45 || tilemap.GetCell(xIndex, yIndex) == 46)
                {
                    item = Obstacle.Items.crate_steel;
                }
                else if (tilemap.GetCell(xIndex, yIndex) == 7)
                {
                    item = Obstacle.Items.roadblock_red;
                }

                if (item != Obstacle.Items.remain)
                {
                    position = tilemap.MapToWorld(new Vector2(xIndex, yIndex));

                    //for (int tileIndex = 0; tileIndex < 4; tileIndex++)
                    //{
                    Obstacle obstacle = (Obstacle)((PackedScene)GD.Load("res://environments/Obstacle.tscn")).Instance();
                    obstacle.type = item;
                    obstacle.Position = position + (cellSize / 2);

                    obstacle.Name = "obstacle_" + xIndex + "_" + yIndex;

                    GetNode("Obstacles").AddChild(obstacle);
                    //}
                }
            }
        }

        if (GetTree().IsNetworkServer())
        {
            buildObstaclesCache();
        }
    }

    public Godot.Collections.Array getPaths(Vector2 start, Vector2 end, World2D space_state, Godot.Collections.Array excludes)
    {
        return aStarSolver.path(start, end, space_state, excludes, this);
    }


    // Based on the "High level" snapshot data, encodes into a byte array
    // ready to be sent across the network. This function does not return
    // the data, just broadcasts it to the connected players. To that end,
    // it is meant to be run only on the server
    private void encodeSnapshot(Snapshot snapshot)
    {
        int clientAgentInfoSentCount = 0;
        int botAgentInfoSentCount = 0;


        if (!GetTree().IsNetworkServer())
        {
            return;
        }

        ClientData[] clientValues = new ClientData[snapshot.playerData.Count];
        snapshot.playerData.Values.CopyTo(clientValues, 0);

        ClientData[] botValues = new ClientData[snapshot.botData.Count];
        snapshot.botData.Values.CopyTo(botValues, 0);

        // Loop through until all data are sent to clients
        while (clientAgentInfoSentCount < snapshot.playerData.Count || botAgentInfoSentCount < snapshot.botData.Count)
        {
            // Max agent sent info before being truncated
            int remainAvailableSlots = 20;

            String encodedData = "";

            // First add the snapshot signature (timestamp)
            encodedData = encodedData + snapshot.signature + ";";

            // Player data count
            // Only need to process if there are remain agent data not sent yet
            if (clientAgentInfoSentCount < snapshot.playerData.Count)
            {
                int targetCount = 0;

                // Chunck data base on available slots
                // Basically check snapshots into smaller size package, which each package maximize at remainAvailableSlots

                // If already sent count + available slots is still less than total client agents
                // Then maximum can sent is remainavailableslots
                if (snapshot.playerData.Count > (remainAvailableSlots + clientAgentInfoSentCount))
                {
                    encodedData = encodedData + remainAvailableSlots + ";";
                    targetCount = remainAvailableSlots + clientAgentInfoSentCount;
                }
                // Otherwise is remain agent counts need to be sent
                else
                {
                    encodedData = encodedData + (snapshot.playerData.Count - clientAgentInfoSentCount) + ";";
                    targetCount = snapshot.playerData.Count;
                }

                for (int index = clientAgentInfoSentCount; index < targetCount; index++)
                {
                    // snapshot_data should contain a "players" field which must be an array
                    // of player data. Each entry in this array should be a dictionary, containing
                    // the following fields: network_id, position, rotation, col
                    ClientData item = clientValues[index];

                    encodedData = encodedData + item.id + ";";
                    encodedData = encodedData + item.position.x + ";";
                    encodedData = encodedData + item.position.y + ";";
                    encodedData = encodedData + item.rotation + ";";
                    encodedData = encodedData + item.primaryWepaon + ";";
                    encodedData = encodedData + item.secondaryWepaon + ";";
                    encodedData = encodedData + item.health + ";";
                    encodedData = encodedData + item.primaryWeaponIndex + ";";
                    encodedData = encodedData + item.secondaryWeaponIndex + ";";

                    remainAvailableSlots--;
                    clientAgentInfoSentCount++;
                }
            }
            else
            {
                // Inidcate no client data to process
                encodedData = encodedData + 0 + ";";
            }

            // Bot data count
            // Only need to process if there are remain agent data not sent yet and there are available slots to send data
            if (botAgentInfoSentCount < snapshot.botData.Count && remainAvailableSlots > 0)
            {
                int targetCount = 0;

                // If already sent count + available slots is still less than total client agents
                // Then maximum can sent is remainavailableslots
                if (snapshot.botData.Count > (remainAvailableSlots + botAgentInfoSentCount))
                {
                    encodedData = encodedData + remainAvailableSlots + ";";
                    targetCount = remainAvailableSlots + botAgentInfoSentCount;
                }
                // Otherwise is remain agent counts need to be sent
                else
                {
                    encodedData = encodedData + (snapshot.botData.Count - botAgentInfoSentCount) + ";";
                    targetCount = snapshot.botData.Count;
                }

                // The bot_data field should be an array, each entry containing the following
                // fields: bot_id, position, rotation
                for (int index = botAgentInfoSentCount; index < targetCount; index++)
                {
                    ClientData item = botValues[index];
                    encodedData = encodedData + item.id + ";";
                    encodedData = encodedData + item.position.x + ";";
                    encodedData = encodedData + item.position.y + ";";
                    encodedData = encodedData + item.rotation + ";";
                    encodedData = encodedData + item.primaryWepaon + ";";
                    encodedData = encodedData + item.secondaryWepaon + ";";
                    encodedData = encodedData + item.health + ";";
                    encodedData = encodedData + item.primaryWeaponIndex + ";";
                    encodedData = encodedData + item.secondaryWeaponIndex + ";";

                    remainAvailableSlots--;
                    botAgentInfoSentCount++;
                }
            }
            else
            {
                // Inidcate no client data to process
                encodedData = encodedData + 0 + ";";
            }

            currentNetworkBytes += encodedData.Length * sizeof(Char);
            currentNetworkSnapshots++;

            // First add the snapshot signature (timestamp)
            RpcUnreliable(nameof(clientGetSnapshot), compressString(encodedData));
        }
    }


    // Compress tring into gzip compressed bytes
    // Referece https://gigi.nullneuron.net/gigilabs/compressing-strings-using-gzip-in-c/
    public byte[] compressString(String input)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);

        using (var outputStream = new MemoryStream())
        {
            using (var gZipStream = new GZipStream(outputStream, CompressionMode.Compress))
                gZipStream.Write(inputBytes, 0, inputBytes.Length);

            return outputStream.ToArray();
        }
    }

    // Decompress gzip bytes into string
    // Referece https://gigi.nullneuron.net/gigilabs/compressing-strings-using-gzip-in-c/
    public String decompressString(byte[] input)
    {

        using (var inputStream = new MemoryStream(input))
        using (var gZipStream = new GZipStream(inputStream, CompressionMode.Decompress))
        using (var outputStream = new MemoryStream())
        {
            gZipStream.CopyTo(outputStream);

            var outputBytes = outputStream.ToArray();

            return Encoding.UTF8.GetString(outputBytes);
        }
    }


    [Remote]
    private void clientGetSnapshot(byte[] encodedDataCompress)
    {
        String encodedData = decompressString(encodedDataCompress);

        currentNetworkBytes += encodedData.Length * sizeof(Char);
        currentNetworkSnapshots++;

        int parseIndex = 0;

        // Extract the signature
        int signature = int.Parse(encodedData.Split(";")[parseIndex]);
        parseIndex++;

        // If the received snapshot is older (or even equal) to the last received one, ignore the rest
        if (signature <= lastSnapshotSignature)
        {
            return;
        }

        Snapshot snapshot = new Snapshot();
        snapshot.signature = signature;

        // Initialize the player data and bot data arrays

        // Extract player data count
        int clientCount = int.Parse(encodedData.Split(";")[parseIndex]);
        parseIndex++;

        // Then the player data itself
        for (int index = 0; index < clientCount; index++)
        {
            ClientData clientData = new ClientData();
            clientData.id = encodedData.Split(";")[parseIndex];
            parseIndex++;
            clientData.position.x = float.Parse(encodedData.Split(";")[parseIndex]);
            parseIndex++;
            clientData.position.y = float.Parse(encodedData.Split(";")[parseIndex]);
            parseIndex++;
            clientData.rotation = float.Parse(encodedData.Split(";")[parseIndex]);
            parseIndex++;
            clientData.primaryWepaon = bool.Parse(encodedData.Split(";")[parseIndex]);
            parseIndex++;
            clientData.secondaryWepaon = bool.Parse(encodedData.Split(";")[parseIndex]);
            parseIndex++;
            clientData.health = int.Parse(encodedData.Split(";")[parseIndex]);
            parseIndex++;
            clientData.primaryWeaponIndex = int.Parse(encodedData.Split(";")[parseIndex]);
            parseIndex++;
            clientData.secondaryWeaponIndex = int.Parse(encodedData.Split(";")[parseIndex]);
            parseIndex++;

            snapshot.playerData.Add(clientData.id, clientData);
        }

        // Extract bot data count
        clientCount = int.Parse(encodedData.Split(";")[parseIndex]);
        parseIndex++;

        // Then the bot data
        for (int index = 0; index < clientCount; index++)
        {
            ClientData clientData = new ClientData();
            clientData.id = encodedData.Split(";")[parseIndex];
            parseIndex++;
            clientData.position.x = float.Parse(encodedData.Split(";")[parseIndex]);
            parseIndex++;
            clientData.position.y = float.Parse(encodedData.Split(";")[parseIndex]);
            parseIndex++;
            clientData.rotation = float.Parse(encodedData.Split(";")[parseIndex]);
            parseIndex++;
            clientData.primaryWepaon = bool.Parse(encodedData.Split(";")[parseIndex]);
            parseIndex++;
            clientData.secondaryWepaon = bool.Parse(encodedData.Split(";")[parseIndex]);
            parseIndex++;
            clientData.health = int.Parse(encodedData.Split(";")[parseIndex]);
            parseIndex++;
            clientData.primaryWeaponIndex = int.Parse(encodedData.Split(";")[parseIndex]);
            parseIndex++;
            clientData.secondaryWeaponIndex = int.Parse(encodedData.Split(";")[parseIndex]);
            parseIndex++;

            snapshot.botData.Add(clientData.id, clientData);
        }

        //  Update the "last_snapshot"
        lastSnapshotSignature = signature;

        // Update snapshots counter
        currentAppliedNetworkSnapshots++;

        // Emit the signal indicating that there is a new snapshot do be applied
        EmitSignal(nameof(SnapshotReceivedSignal), snapshot);
    }
    private void applySnapshot(Snapshot snapshot)
    {
        // In here we assume the obtained snapshot is newer than the last one
        // Iterate through player data 
        foreach (ClientData item in snapshot.playerData.Values)
        {

            // Depending on the synchronization mechanism, this may not be an error!
            // For now assume the entities are spawned and kept in sync so just continue
            // the loop
            if (!HasNode("client_" + item.id) || !IsInstanceValid(GetNode("client_" + item.id)))
            {
                continue;
            }

            Player client = (Player)GetNode("client_" + item.id);

            client.changePrimaryWeapon(item.primaryWeaponIndex);
            client.changeSecondaryWeapon(item.secondaryWeaponIndex);
            client.set(item.position, item.rotation, item.primaryWepaon, item.secondaryWepaon, (item.position != client.Position));
            client.setHealth(item.health);
        }

        foreach (ClientData item in snapshot.botData.Values)
        {
            // Only need to do on client, as logic already perform on server through calculation
            if (!GetTree().IsNetworkServer())
            {
                if (!HasNode(item.id) || !IsInstanceValid(GetNode(item.id)))
                {
                    continue;
                }

                Enemy client = (Enemy)GetNode(item.id);

                client.changePrimaryWeapon(item.primaryWeaponIndex);
                client.changeSecondaryWeapon(item.secondaryWeaponIndex);
                client.set(item.position, item.rotation, item.primaryWepaon, item.secondaryWepaon, (item.position != client.Position));
                client.setHealth(item.health);
            }
        }
    }

    private void onPlayerRemoved(int id)
    {
        removeDisconnectedPlayer(id);
        syncBots(-1);
    }

    private void onDisconnected()
    {
        gameStates.restart();
    }


    [Remote]
    private void removeDisconnectedPlayer(int id)
    {

        if (GetTree().IsNetworkServer())
        {
            foreach (KeyValuePair<int, NetworkPlayer> item in network.networkPlayers)
            {
                // Skip disconnecte player and server from replication code
                if (item.Key == id || item.Key == 1)
                {
                    continue;
                }
                // Replicate despawn into currently iterated player
                RpcId(item.Key, "removeDisconnectedPlayer", id);
            }
        }

        removePlayer(id);
    }


    private void removePlayer(int id)
    {
        if (!HasNode("client_" + id))
        {
            GD.Print("Cannoot remove invalid node from tree");
            return;
        }

        // Try to locate the player actor
        Tank playerNode = (Tank)GetNode("client_" + id);

        if (playerNode == null || !IsInstanceValid(playerNode))
        {
            GD.Print("Cannoot remove invalid node from tree");
            return;
        }

        if (spawnPlayers.ContainsKey(id + ""))
        {
            spawnPlayers.Remove(id + "");
        }

        // If this is the player attach to current client, respawn the client with observer
        if (id == network.gamestateNetworkPlayer.net_id)
        {
            // Load the scene and create an instance
            Observer client;

            client = (Observer)((PackedScene)GD.Load("res://tanks/Observer.tscn")).Instance();

            client.Position = playerNode.GlobalPosition;

            client.Name = "client_observer_" + id;

            AddChild(client);

            client.setCameraLimit();
            EmitSignal(nameof(PlayerDefeatedSignal));
        }

        // Mark the node for deletion
        playerNode.explode();

        if (GetTree().IsNetworkServer())
        {
            if (spawnPlayers.Count == 0)
            {
                endGame();
            }
        }

    }

    // Update and generate a game state snapshot
    private void updateState()
    {
        // If not on the server, bail
        if (!GetTree().IsNetworkServer())
        {
            return;
        }
        // Initialize the "high level" snapshot
        Snapshot snapshot = new Snapshot();
        snapshot.signature = snapshotSignature;

        Godot.Collections.Array<int> removeSpawnPlayers = new Godot.Collections.Array<int>();

        foreach (KeyValuePair<int, NetworkPlayer> networkPlayer in network.networkPlayers)
        {
            // Node may not being created yet
            if (!HasNode("client_" + networkPlayer.Value.net_id))
            {
                // Ideally should give a warning that a player node wasn't found
                continue;
            }

            // Locate the player's node. Even if there is no input/update, it's state will be dumped
            // into the snapshot anyway
            Player playerNode = (Player)GetNode("client_" + networkPlayer.Value.net_id);

            if (playerNode == null || !IsInstanceValid(playerNode))
            {
                // Ideally should give a warning that a player node wasn't found
                continue;
            }

            Vector2 pPosition = playerNode.Position;
            float pRotation = playerNode.Rotation;

            // Only update if player is not dead yet
            if (playerNode.getHealth() > 0)
            {
                // Check if there is any input for this player. In that case, update the state
                if (gameStates.playerInputs.ContainsKey(networkPlayer.Key) && gameStates.playerInputs[networkPlayer.Key].Count > 0)
                {

                    bool primaryWeapon = false;
                    bool secondaryWeapon = false;

                    // Calculate the delta
                    float delta = gameStates.updateDelta / (float)(gameStates.playerInputs[networkPlayer.Key].Count);

                    foreach (KeyValuePair<int, GameStates.PlayerInput> input in gameStates.playerInputs[networkPlayer.Key])
                    {

                        Vector2 moveDir = new Vector2();
                        if (input.Value.up) { moveDir.y = -1; }
                        if (input.Value.down) { moveDir.y = 1; }
                        if (input.Value.left) { moveDir.x = -1; }
                        if (input.Value.right) { moveDir.x = 1; }

                        // If waiting period, no fire, else use the current user setup
                        if (!waitingPeriod)
                        {
                            primaryWeapon = input.Value.primaryWepaon;
                            secondaryWeapon = input.Value.secondaryWepaon;
                        }

                        if (input.Value.changePrimaryWeapon) { playerNode.changePrimaryWeapon(playerNode.currentPrimaryWeaponIndex + 1); }
                        if (input.Value.changeSecondaryWeapon) { playerNode.changeSecondaryWeapon(playerNode.currentSecondaryWeaponIndex + 1); }
                        playerNode._shoot(primaryWeapon, secondaryWeapon);
                        playerNode.move(moveDir, input.Value.mousePosition, delta);
                    }

                    // Cleanup the input vector
                    gameStates.playerInputs[networkPlayer.Key].Clear();

                    gameStates.playerInputs.Remove(networkPlayer.Key);

                    ClientData clientData = new ClientData();
                    clientData.id = networkPlayer.Key + "";
                    clientData.position = playerNode.Position;
                    clientData.rotation = playerNode.Rotation;
                    clientData.primaryWepaon = primaryWeapon;
                    clientData.secondaryWepaon = secondaryWeapon;
                    clientData.primaryWeaponIndex = playerNode.currentPrimaryWeaponIndex;
                    clientData.secondaryWeaponIndex = playerNode.currentSecondaryWeaponIndex;
                    clientData.health = playerNode.getHealth();

                    snapshot.playerData.Add(networkPlayer.Key, clientData);
                }
            }
            else
            {
                removeSpawnPlayers.Insert(0, networkPlayer.Key);
            }
        }

        // Clean the input
        gameStates.playerInputs.Clear();

        foreach (int spawnPlayerId in removeSpawnPlayers)
        {
            removeClient(spawnPlayerId + "");
        }

        Godot.Collections.Array<String> removeSpawnBots = new Godot.Collections.Array<String>();

        foreach (SpawnBot spawnBot in spawnBots.Values)
        {
            // Locate the bot node
            Enemy enemyNode = (Enemy)GetNode(spawnBot.name);

            if (enemyNode == null || !IsInstanceValid(enemyNode))
            {
                // Ideally should give a warning that a bot node wasn't found
                continue;
            }


            bool primaryWeapon = false;
            bool secondaryWeapon = false;

            // If waiting period, no fire, else use the current bot setup
            if (!waitingPeriod)
            {
                primaryWeapon = enemyNode.isPrimaryWeapon;
                secondaryWeapon = enemyNode.isSecondaryWeapon;
            }

            enemyNode._shoot(primaryWeapon, secondaryWeapon);

            if (enemyNode.getHealth() > 0)
            {
                // Build bot_data entry
                ClientData clientData = new ClientData();
                clientData.id = spawnBot.name;
                clientData.position = enemyNode.GlobalPosition;
                clientData.rotation = enemyNode.GlobalRotation;
                clientData.health = enemyNode.getHealth();
                clientData.primaryWepaon = primaryWeapon;
                clientData.secondaryWepaon = secondaryWeapon;
                clientData.primaryWeaponIndex = enemyNode.currentPrimaryWeaponIndex;
                clientData.secondaryWeaponIndex = enemyNode.currentSecondaryWeaponIndex;

                // Append into the snapshot
                snapshot.botData.Add(spawnBot.name, clientData);
            }
            else
            {
                removeSpawnBots.Insert(0, spawnBot.name);
            }
        }

        foreach (String spawnBotId in removeSpawnBots)
        {
            removeClient(spawnBotId);
        }

        if (removeSpawnBots.Count > 0)
        {
            syncBots(-1);
        }

        // Encode and broadcast the snapshot - if there is at least one connected client
        if (network.networkPlayers.Count > 1)
        {
            encodeSnapshot(snapshot);
        }
        // Make sure the next update will have the correct snapshot signature
        snapshotSignature += 1;
    }

    private SpwanInfo convertToObject(String info)
    {
        SpwanInfo spwanInfo = new SpwanInfo();
        spwanInfo.networkPlayer = new NetworkPlayer(info);
        spwanInfo.spawn_index = Int32.Parse(info.Split(";")[3]);
        return spwanInfo;
    }

    private String formatTwoDigits(int input)
    {
        if (input < 10)
        {
            return "0" + input;
        }
        else
        {
            return "" + input;
        }
    }

    private void waitingPeriodTimerTimeout()
    {
        internalTimer -= 1;
        String message = "00:00:" + formatTwoDigits(internalTimer);

        // Stop the timer
        if (internalTimer == 0)
        {
            timer.Stop();
        }

        Rpc(nameof(getWaitingPeriodStatus), message);
        if (GetTree().IsNetworkServer())
        {
            getWaitingPeriodStatus(message);
        }

        // Start to count against game time
        if (internalTimer == 0)
        {
            internalTimer = MaxGameTime;

            if (timer.IsConnected("timeout", this, nameof(waitingPeriodTimerTimeout)))
            {
                timer.Disconnect("timeout", this, nameof(waitingPeriodTimerTimeout));
            }

            timer.Connect("timeout", this, nameof(gamePeriodTimeout));
            timer.Start();
        }
    }

    [Remote]
    private void getWaitingPeriodStatus(String message)
    {
        EmitSignal(nameof(WaitingPeriodSignal), message + " WAITING PERIOD");
        if (message == "00:00:00")
        {
            waitingPeriod = false;
        }
    }

    private void gamePeriodTimeout()
    {
        internalTimer -= 1;

        int hour = internalTimer / 3600;

        int minutes = (internalTimer % 3600) / 60;

        int seconds = (internalTimer % 3600) % 60;

        String message = formatTwoDigits(hour) + ":" + formatTwoDigits(minutes) + ":" + formatTwoDigits(seconds);

        RpcUnreliable(nameof(getGamePeriodStatus), message);

        if (GetTree().IsNetworkServer())
        {
            getGamePeriodStatus(message);
        }

        if (internalTimer == 0)
        {
            timer.Stop();
            endGame();
        }
    }

    [Remote]
    private void getGamePeriodStatus(String message)
    {
        EmitSignal(nameof(WaitingPeriodSignal), message + " GAME PERIOD");

    }

    private String validateGameResult()
    {
        String message = "";


        // Record elaspse time
        int elapseTime = MaxGameTime - internalTimer;

        if (spawnPlayers.Count < spawnBots.Count)
        {
            message = "You Lost;Players Remain (" + spawnPlayers.Count + ") is less than Bots Remain(" + spawnBots.Count + ")";
        }
        else if (spawnPlayers.Count == spawnBots.Count)
        {
            message = "Tie;Players Remain (" + spawnPlayers.Count + ") is equal to Bots Remain(" + spawnBots.Count + ")";
        }
        else if (spawnPlayers.Count > spawnBots.Count)
        {
            message = "You Win; Players Remain (" + spawnPlayers.Count + ") is greater than Bots Remain(" + spawnBots.Count + ")";
        }


        if (elapseTime == MaxGameTime)
        {
            message = message + "at game period timeout";
        }

        int hour = elapseTime / 3600;

        int minutes = (elapseTime % 3600) / 60;

        int seconds = (elapseTime % 3600) % 60;

        message = message + ";" + formatTwoDigits(hour) + ":" + formatTwoDigits(minutes) + ":" + formatTwoDigits(seconds);

        return message;
    }

    private void endGame()
    {
        String message = validateGameResult();

        Rpc(nameof(notifyEndGameStatus), validateGameResult());

        notifyEndGameStatus(message);
    }

    [Remote]
    private void notifyEndGameStatus(String message)
    {
        gameStates.setMessagesForNextScene(message);
        gameStates.endGameScreen();
    }

    private String convertToString(NetworkPlayer networkPlayer, int spawn_index)
    {
        return networkPlayer.ToString() + ";" + spawn_index;
    }


    [Remote]
    private void spwanPlayer(String info)
    {
        SpwanInfo spwanInfo = convertToObject(info);
        NetworkPlayer pininfo = new NetworkPlayer(info);
        int spawnIndex = spwanInfo.spawn_index;

        // If the spawn_index is -1 then we define it based on the size of the player list
        if (spawnIndex == -1)
        {
            spawnIndex = network.networkPlayers.Count;
        }

        spawnPlayers.Add(pininfo.net_id + "", null);

        if (GetTree().IsNetworkServer() && pininfo.net_id != 1)
        {
            // We are on the server and the requested spawn does not belong to the server
            // Iterate through the connected players
            int s_index = 1; // Will be used as spawn index

            foreach (KeyValuePair<int, NetworkPlayer> item in network.networkPlayers)
            {
                // If it is not one of active player, then no need to sync (as the player is either observer or dead)
                if (!spawnPlayers.ContainsKey(item.Key + ""))
                {
                    continue;
                }

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

            // Add current bot info to new player
            foreach (SpawnBot spawnBot in spawnBots.Values)
            {
                RpcId(pininfo.net_id, nameof(addBot), spawnBot.name);
            }

            // Sync the destoryed obstacles
            foreach (String obstacle in obstaclesDestroyed)
            {
                RpcId(pininfo.net_id, nameof(destroyObstacle), obstacle);
            }

            // Sync the waiting period if it is already end
            if (!waitingPeriod)
            {
                RpcId(pininfo.net_id, nameof(getWaitingPeriodStatus), "00:00");
            }
        }

        // Load the scene and create an instance
        Player client;

        client = (Player)((PackedScene)GD.Load("res://tanks/Player.tscn")).Instance();

        // Get spawn position, -1 as to utilize 0 spawn point
        Node2D nodeSpawnPoint = (Node2D)GetNode("SpawnPoints/SpawnPoint_" + getNextSpawnIndex(spawnIndex - 1));
        client.Position = nodeSpawnPoint.GlobalPosition;

        client.Name = "client_" + pininfo.net_id;
        client.setUnitName(pininfo.name);
        client.setTeamIdentifier(pininfo.team);

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
            client.Connect("PrimaryWeaponChangeSignal", GetNode("HUD"), "_updatePrimaryWeapon");
            client.Connect("PrimaryWeaponChangeSignal", GetNode("HUD"), "_updateSecondaryWeapon");
            client.Connect("HealthChangedSignal", GetNode("HUD"), "_updateHealthBar");
            client.Connect("DefeatedAgentChangedSignal", GetNode("HUD"), "_updateDefeatedAgentBar");

            // Notify HUD about weapon 2
            client.changePrimaryWeapon(2);

            _setCameraLimit();
        }


    }

    [Remote]
    private void removeClient(String id)
    {
        if (GetTree().IsNetworkServer())
        {
            foreach (KeyValuePair<int, NetworkPlayer> item in network.networkPlayers)
            {
                // Skip server from replication code
                if (item.Key == 1)
                {
                    continue;
                }
                // Replicate despawn into currently iterated player

                RpcId(item.Key, nameof(removeClient), id);
            }
        }

        if (spawnBots.ContainsKey(id))
        {
            removeBot(id);
        }
        else
        {
            removePlayer(Int32.Parse(id));
        }
    }

    [Remote]
    private void syncBots(int bot_count)
    {
        if (GetTree().IsNetworkServer())
        {
            // Calculate the target amount of spawned bots
            bot_count = network.serverinfo.max_players - network.networkPlayers.Count;

            if (spawnBots.Count > bot_count)
            {
                while (spawnBots.Count > bot_count)
                {
                    foreach (SpawnBot spawnBot in spawnBots.Values)
                    {
                        removeClient(spawnBot.name);

                        break;
                    }
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
                    // Only generated if bot have not created excess MAX_BOT_COUNT
                    if (botCounter < MAX_BOT_COUNT)
                    {
                        String botId = "bot_" + botCounter;
                        botCounter++;

                        Rpc(nameof(addBot), botId);

                        addBot(botId);
                    }
                    else
                    {
                        // Break from loop
                        break;
                    }
                }
            }

            // If there is no new bots can be deployed, and there is no current bot, end game
            if (spawnBots.Count == 0 && botCounter == MAX_BOT_COUNT)
            {
                endGame();
            }
        }

    }

    private void removeBot(String botId)
    {
        if (HasNode(spawnBots[botId].name))
        {
            Tank node = null;
            node = (Tank)GetNode(botId);

            if (!IsInstanceValid(node))
            {
                GD.Print("Must remove bots from game but cannot find its node");
            }
            else
            {
                node.explode();
            }
        }

        if (spawnBots.ContainsKey(botId))
        {
            spawnBots.Remove(botId);
        }
    }

    [Remote]
    private void addBot(String botId)
    {
        if (!spawnBots.ContainsKey(botId))
        {
            spawnBots.Add(botId, new SpawnBot(botId, (PackedScene)GD.Load("res://tanks/Enemy.tscn")));

            Enemy bot = (Enemy)((PackedScene)GD.Load("res://tanks/Enemy.tscn")).Instance();

            // Get spawn position, -1 as to utilize 0 spawn point
            int currentSpawnPoint = (int)botCounter % GetNode("SpawnPoints").GetChildCount();
            Node2D nodeSpawnPoint = (Node2D)GetNode("SpawnPoints/SpawnPoint_" + currentSpawnPoint);

            bot.Name = botId;
            bot.setUnitName(botId);
            bot.setTeamIdentifier("TEAM_BOT");

            bot.Position = new Vector2(nodeSpawnPoint.GlobalPosition.x + random.RandiRange(-5, 5), nodeSpawnPoint.GlobalPosition.y + random.RandiRange(-5, 5));

            bot.SetNetworkMaster(1);
            bot.setCurrentSpawnIndex(currentSpawnPoint);

            AddChild(bot);

            // Randomize weapon between 0, 1, 2
            bot.changePrimaryWeapon((int)botCounter % 3);
        }
    }

    public override void _Process(float delta)
    {
        // Update the timeout counter
        currentTime += delta;
        if (currentTime < gameStates.updateDelta)
        {
            return;
        }

        // "Reset" the time counting
        currentTime -= gameStates.updateDelta;

        // And update the game state
        updateState();
    }

    private void _setCameraLimit()
    {
        TileMap tileMap = (TileMap)GetNode("Navigation2D/Ground");
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

    private void _onTankShoot(PackedScene bullet, Vector2 _position, Vector2 _direction, Node2D source = null, Node2D target = null)
    {
        Bullet newBullet = (Bullet)bullet.Instance();
        AddChild(newBullet);
        newBullet.start(_position, _direction, source, target);
    }

    private void _onDamageCalculation(int damage, Vector2 hitDir, Godot.Object source, Godot.Object target)
    {
        if (target != null && IsInstanceValid(target))
        {

            if (target.HasMethod("TakeDamage"))
            {
                Tank tankTarget = (Tank)(target);
                Tank tankSource = (Tank)(source);
                tankTarget.TakeDamage(damage, hitDir, tankSource);
            }
            else if (target.HasMethod("TakeEnvironmentDamage"))
            {
                ((Obstacle)(target)).TakeEnvironmentDamage(damage);
            }
        }
    }

    public void buildObstaclesCache()
    {
        TileMap tilemap = (TileMap)GetNode("Navigation2D/Ground");
        foreach (Node node in GetNode("Obstacles").GetChildren())
        {
            Obstacle obstacle = (Obstacle)node;

            Vector2 tileMapPosition = tilemap.WorldToMap(obstacle.Position);

            float xIndex = tileMapPosition.x;
            float yIndex = tileMapPosition.y;

            // Align to the cell deployment (2 x 2 index size)
            if (xIndex % 2 != 0)
            {
                xIndex = xIndex - 1;
            }
            if (yIndex % 2 != 0)
            {
                yIndex = yIndex - 1;
            }

            if (!obstacle.IsConnected("ObstacleDestroy", this, nameof(_onObstacleDestroy)))
            {
                obstacle.Connect("ObstacleDestroy", this, nameof(_onObstacleDestroy));
            }
            obstacles.Add("obstacle_" + xIndex + "_" + yIndex, obstacle.Position);
        }
    }

    public bool checkObstacles(Vector2 input)
    {
        TileMap tilemap = (TileMap)GetNode("Navigation2D/Ground");
        Vector2 tileMapPosition = tilemap.WorldToMap(input);

        float xIndex = tileMapPosition.x;
        float yIndex = tileMapPosition.y;

        // Align to the cell deployment (2 x 2 index size)
        if (xIndex % 2 != 0)
        {
            xIndex = xIndex - 1;
        }
        if (yIndex % 2 != 0)
        {
            yIndex = yIndex - 1;
        }

        return obstacles.ContainsKey("obstacle_" + xIndex + "_" + yIndex);
    }

    private void _onObstacleDestroy(String obstacleName)
    {
        if (GetTree().IsNetworkServer())
        {
            if (obstacles.ContainsKey(obstacleName))
            {
                obstacles.Remove(obstacleName);
            }

            obstaclesDestroyed.Add(obstacleName);

            destroyObstacle(obstacleName);

            Rpc(nameof(destroyObstacle), obstacleName);
        }

    }

    [Remote]
    private void destroyObstacle(String name)
    {
        if (GetNode("Obstacles").HasNode(name))
        {
            Obstacle obstacle = (Obstacle)GetNode("Obstacles/" + name);
            if (IsInstanceValid(obstacle))
            {
                obstacle.explode();
            }
        }
    }

}
