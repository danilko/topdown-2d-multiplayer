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


    private int MAX_BOT_COUNT = 100;

    public class ClientData : Godot.Object
    {
        public String Id;
        public Vector2 Position;
        public float Rotation;
        public int PrimaryWeapon;
        public int SecondaryWeapon;
        public int Health;
        public int PrimaryWeaponIndex;
        public int SecondaryWeaponIndex;
    }

    public class Snapshot : Godot.Object
    {
        public int signature;
        public Godot.Collections.Dictionary playerData = new Godot.Collections.Dictionary();
        public Godot.Collections.Dictionary botData = new Godot.Collections.Dictionary();
    }



    public class ClientState
    {
        public Vector2 FromPosition;
        public float FromRotation;
        public Vector2 ToPosition;
        public float ToRotation;
        public int primaryWeapon;
        public int SecondaryWeapon;
        public int Health;
        public float Time;
        public Node2D Node;
    }

    Dictionary<String, ClientState> clientStates = new Dictionary<String, ClientState>();

    private Godot.Collections.Dictionary<String, Vector2> obstacles = new Godot.Collections.Dictionary<String, Vector2>();
    private Godot.Collections.Array obstaclesDestroyed = new Godot.Collections.Array();

    int spawned_bots = 0;

    Dictionary<String, Agent> spawnBots = new Dictionary<String, Agent>();
    Dictionary<String, Agent> spawnPlayers = new Dictionary<String, Agent>();


    Network network;

    long botCounter = 0;

    float currentTime;

    // The "signature" (timestamp) added into each generated state snapshot
    // int max is 2,147,483,647 
    // 2,147,483,647 / 60 (snapshots) / 60 (seconds => 1 min) /60 (mins = > 1 hr) /24 (24 hrs => 1 day) = 414 days (before the snapshot expire)
    // So as long as this round of games end in 1 year, the snapshot signature will be unique
    int snapshotSignature = 1;

    // The signature of the last snapshot received
    int lastSnapshotSignature = 0;

    private Navigation2D navigation2D;

    private AgentAStar aStar;

    private Godot.RandomNumberGenerator random;

    // Called when the node enters the scene tree for the first time.

    private float currentNetworkBytes = 0;
    private float currentNetworkSnapshots = 0;
    private float currentAppliedNetworkSnapshots = 0;

    private bool waitingPeriod;

    [Export]
    private int MaxWaitingTime = 1;

    [Export]
    private int MaxGameTime = 3600;

    // Use as tick to track countdown time
    private int internalTimer;

    private CapaturableBaseManager _capaturableBaseManager;

    private Godot.Collections.Array<TeamMapAI> _teamMapAIs;

    private Timer timer;

    public override void _Ready()
    {
        //buildObstacles();

        random = new RandomNumberGenerator();
        gameStates = (GameStates)GetNode("/root/GAMESTATES");

        _capaturableBaseManager = (CapaturableBaseManager)GetNode("CapaturableBaseManager");

        _initializeTeamMapAI();

        Input.SetCustomMouseCursor(GD.Load("res://assets/ui/blue_cross.png"), Input.CursorShape.Arrow, new Vector2(16, 16));

        // After receiving and fully decoding a new snapshot, apply it to the game world
        this.Connect(nameof(SnapshotReceivedSignal), this, nameof(applySnapshot));

        network = (Network)GetNode("/root/NETWORK");
        network.Connect(nameof(Network.DisconnectedSignal), this, nameof(onDisconnected));

        if (GetTree().IsNetworkServer())
        {
            network.Connect(nameof(Network.PlayerRemovedSignal), this, nameof(onPlayerRemoved));
        }

        // Spawn the players
        if (GetTree().IsNetworkServer())
        {
            spwanPlayer(convertToString(network.gamestateNetworkPlayer, 1));
            _syncBots();
        }
        else
        {
            RpcId(1, nameof(spwanPlayer), convertToString(network.gamestateNetworkPlayer, -1));
        }


        // Update network flow
        this.Connect(nameof(NeworkRateUpdateSignal), GetNode("HUD"), "_onNetworkRateUpdate");
        this.Connect(nameof(PlayerDefeatedSignal), GetNode("HUD"), "_onPlayerDefeatedMessage");

        this.Connect(nameof(Network.PlayerListChangedSignal), GetNode("HUD"), "onPlayerListChanged");

        this.Connect(nameof(WaitingPeriodSignal), GetNode("HUD"), "_onUpdateTimer");

        // Update playerlist
        EmitSignal(nameof(Network.PlayerListChangedSignal));

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

    private void _initializeTeamMapAI()
    {
        _teamMapAIs = new Godot.Collections.Array<TeamMapAI>();

        // Start with neutral and above
        for (int index = 0; index < (int)(Team.TeamCode.NEUTRAL); index++)
        {
            TeamMapAI ai = (TeamMapAI)((PackedScene)GD.Load("res://ai/TeamMapAI.tscn")).Instance();
            ai.Name = nameof(TeamMapAI) + "_" + (Team.TeamCode)index;
            AddChild(ai);

            ai.Initialize(this, _capaturableBaseManager.GetBases(), (Team.TeamCode)index);

            _teamMapAIs.Add(ai);

            foreach (CapturableBase capturable in _capaturableBaseManager.GetBases())
            {
                capturable.Connect(nameof(CapturableBase.BaseTeamChangeSignal), ai, nameof(TeamMapAI.HandleCapturableBaseCaptured));
            }
        }
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

        Godot.Collections.Dictionary prebuildObstacles = new Godot.Collections.Dictionary();

        if (GetTree().IsNetworkServer())
        {
            aStar = new AgentAStar(tilemap);

            // Add pre - added obstacles
            foreach (Node2D obstacle in GetNode("Obstacles").GetChildren())
            {
                Vector2 pos = tilemap.WorldToMap(obstacle.Position);
                prebuildObstacles.Add(pos.x + "+" + pos.y, pos);

                float x = pos.x - 2;
                float y = pos.y - 2;
                prebuildObstacles.Add(x + "+" + y, new Vector2(x, y));

                x = pos.x;
                y = pos.y - 2;
                prebuildObstacles.Add(x + "+" + y, new Vector2(x, y));

                x = pos.x - 2;
                y = pos.y;
                prebuildObstacles.Add(x + "+" + y, new Vector2(x, y));

                x = pos.x + 2;
                y = pos.y + 2;
                prebuildObstacles.Add(x + "+" + y, new Vector2(x, y));

                x = pos.x;
                y = pos.y + 2;
                prebuildObstacles.Add(x + "+" + y, new Vector2(x, y));

                x = pos.x + 2;
                y = pos.y;
                prebuildObstacles.Add(x + "+" + y, new Vector2(x, y));
            }
        }


        // As the grid use in this game is 2 x 2 of a normal godot grid, so need to increment by 2
        for (int xIndex = startPointX; xIndex < maxLengthX; xIndex = xIndex + 2)
        {
            for (int yIndex = startPointY; yIndex < maxLengthY; yIndex = yIndex + 2)
            {

                // if there is already obstacle on it, then ignore this tile, this is also not workable tile, so skip entire logic to next tile
                if (prebuildObstacles.Contains(xIndex + "+" + yIndex))
                {
                    continue;
                }

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

                Label mapLabel = (Label)GetNode("MapCoordinate").Duplicate();
                mapLabel.Text = ("(" + xIndex + "," + yIndex + ")");
                mapLabel.Name = "maplabel_" + xIndex + "_" + yIndex;

                position = tilemap.MapToWorld(new Vector2(xIndex, yIndex));

                if (item != Obstacle.Items.remain)
                {

                    Obstacle obstacle = (Obstacle)((PackedScene)GD.Load("res://environments/Obstacle.tscn")).Instance();
                    obstacle.type = item;

                    obstacle.Name = "obstacle_" + xIndex + "_" + yIndex;

                    GetNode("Obstacles").AddChild(obstacle);

                    obstacle.GlobalPosition = position + cellSize;

                    mapLabel.Set("custom_colors/font_color", new Color("#ff0000"));
                }
                else
                {
                    if (GetTree().IsNetworkServer())
                    {
                        // As there is no obstacle, this cell is a workable path
                        aStar.addCell(new Vector2(xIndex, yIndex));
                    }

                    mapLabel.Set("custom_colors/font_color", new Color("#0016ff"));
                }

                mapLabel.SetGlobalPosition(position + cellSize);

                this.AddChild(mapLabel);
            }
        }



        if (GetTree().IsNetworkServer())
        {
            aStar.connectPoints();
            buildObstaclesCache();
        }
    }



    public Godot.Collections.Array getPaths(Vector2 start, Vector2 end, World2D space_state, Godot.Collections.Array excludes)
    {
        //return aStarSolver.path(start, end, space_state, excludes, this);
        return aStar.getPath(start, end);
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
                    encodeClientData(encodedData, botValues[index]);
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
                    encodeClientData(encodedData, botValues[index]);
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

            parseIndex = _parseClientData(encodedData, clientData, parseIndex);

            snapshot.playerData.Add(clientData.Id, clientData);
        }

        // Extract bot data count
        clientCount = int.Parse(encodedData.Split(";")[parseIndex]);
        parseIndex++;

        // Then the bot data
        for (int index = 0; index < clientCount; index++)
        {
            ClientData clientData = new ClientData();
            parseIndex = _parseClientData(encodedData, clientData, parseIndex);

            snapshot.botData.Add(clientData.Id, clientData);
        }

        //  Update the "last_snapshot"
        lastSnapshotSignature = signature;

        // Update snapshots counter
        currentAppliedNetworkSnapshots++;

        // Emit the signal indicating that there is a new snapshot do be applied
        EmitSignal(nameof(SnapshotReceivedSignal), snapshot);
    }

    private int _parseClientData(String encodedData, ClientData clientData, int parseIndex)
    {
        clientData.Id = encodedData.Split(";")[parseIndex];
        parseIndex++;
        clientData.Position.x = float.Parse(encodedData.Split(";")[parseIndex]);
        parseIndex++;
        clientData.Position.y = float.Parse(encodedData.Split(";")[parseIndex]);
        parseIndex++;
        clientData.Rotation = float.Parse(encodedData.Split(";")[parseIndex]);
        parseIndex++;
        clientData.PrimaryWeapon = int.Parse(encodedData.Split(";")[parseIndex]);
        parseIndex++;
        clientData.SecondaryWeapon = int.Parse(encodedData.Split(";")[parseIndex]);
        parseIndex++;
        clientData.Health = int.Parse(encodedData.Split(";")[parseIndex]);
        parseIndex++;
        clientData.PrimaryWeaponIndex = int.Parse(encodedData.Split(";")[parseIndex]);
        parseIndex++;
        clientData.SecondaryWeaponIndex = int.Parse(encodedData.Split(";")[parseIndex]);
        parseIndex++;

        return parseIndex;
    }

    private void encodeClientData(String encodedData, ClientData clientData)
    {
        encodedData = encodedData + clientData.Id + ";";
        encodedData = encodedData + clientData.Position.x + ";";
        encodedData = encodedData + clientData.Position.y + ";";
        encodedData = encodedData + clientData.Rotation + ";";
        encodedData = encodedData + clientData.PrimaryWeapon + ";";
        encodedData = encodedData + clientData.SecondaryWeapon + ";";
        encodedData = encodedData + clientData.Health + ";";
        encodedData = encodedData + clientData.PrimaryWeaponIndex + ";";
        encodedData = encodedData + clientData.SecondaryWeaponIndex + ";";
    }

    private void applySnapshot(Snapshot snapshot)
    {
        // In here we assume the obtained snapshot is newer than the last one
        // Iterate through player data 
        foreach (ClientData item in snapshot.playerData.Values)
        {
            _updateAgentStateFromSnapshot("client_" + item.Id, item);
        }

        foreach (ClientData item in snapshot.botData.Values)
        {
            // Only need to do on client, as logic already perform on server through calculation
            if (!GetTree().IsNetworkServer())
            {
                _updateAgentStateFromSnapshot(item.Id, item);
            }
        }
    }

    private void _updateAgentStateFromSnapshot(String agentNodeName, ClientData item)
    {
        // Depending on the synchronization mechanism, this may not be an error!
        // For now assume the entities are spawned and kept in sync so just continue
        // the loop
        if (!HasNode(item.Id) || !IsInstanceValid(GetNode(item.Id)))
        {
            return;
        }

        Agent client = (Agent)GetNode(agentNodeName);

        if (client.currentPrimaryWeaponIndex != item.PrimaryWeaponIndex) { client.changePrimaryWeapon(item.PrimaryWeaponIndex); }
        if (client.currentSecondaryWeaponIndex != item.SecondaryWeaponIndex) { client.changeSecondaryWeapon(item.SecondaryWeaponIndex); }

        client.Sync(item.Position, item.Rotation, item.PrimaryWeapon, item.SecondaryWeapon);
        client.setHealth(item.Health);
    }

    private void onPlayerRemoved(int id)
    {
        removeDisconnectedPlayer(id);
        _syncBots();
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

        _removeUnit("client_" + id, true);
    }


    private void _removeUnit(String id, bool player)
    {

        Vector2 existingPosition = Vector2.Zero;

        Dictionary<String, Agent> currentSpawnCache = null;

        // Assign to different cache base on team
        if (player)
        {
            currentSpawnCache = spawnPlayers;
        }
        else
        {
            currentSpawnCache = spawnBots;
        }

        if (currentSpawnCache.ContainsKey(id) && _teamMapAIs[(int)currentSpawnCache[id].GetTeam()].GetUnitsContainer().HasNode(id))
        {
            existingPosition = currentSpawnCache[id].GlobalPosition;

            _teamMapAIs[(int)currentSpawnCache[id].GetTeam()].RemoveUnit(id);
        }

        if (currentSpawnCache.ContainsKey(id))
        {
            currentSpawnCache.Remove(id);
        }

        // If this is the player attach to current client, respawn the client with observer
        if (player && id == "client_" + network.gamestateNetworkPlayer.net_id)
        {
            // Load the scene and create an instance
            Observer client;

            client = (Observer)((PackedScene)GD.Load("res://agents/Observer.tscn")).Instance();

            client.Position = existingPosition;

            client.Name = "client_observer_" + id;

            AddChild(client);

            client.setCameraLimit();
            EmitSignal(nameof(PlayerDefeatedSignal));
        }

        if (GetTree().IsNetworkServer())
        {
            _checkGameWinningCondition();
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
            String playerId = "client_" + networkPlayer.Value.net_id;
            // Node may not being created yet
            if (!spawnPlayers.ContainsKey(playerId))
            {
                // Ideally should give a warning that a player node wasn't found
                continue;
            }

            // Locate the player's node. Even if there is no input/update, it's state will be dumped
            // into the snapshot anyway
            Agent playerNode = (Agent)_teamMapAIs[(int)spawnPlayers[playerId].GetTeam()].GetUnitsContainer().GetNode(playerId);

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

                    int primaryWeapon = 0;
                    int secondaryWeapon = 0;

                    // Calculate the delta
                    float delta = gameStates.updateDelta / (float)(gameStates.playerInputs[networkPlayer.Key].Count);

                    foreach (KeyValuePair<int, GameStates.PlayerInput> input in gameStates.playerInputs[networkPlayer.Key])
                    {

                        Vector2 moveDir = new Vector2();
                        moveDir.y = -1 * input.Value.Up;
                        moveDir.y += 1 * input.Value.Down;
                        moveDir.x = -1 * input.Value.Left;
                        moveDir.x += 1 * input.Value.Right;

                        playerNode.changePrimaryWeapon(input.Value.PrimaryWeaponIndex);
                        playerNode.changeSecondaryWeapon(input.Value.SecondaryWeaponIndex);

                        // If waiting period, no fire, else use the current user setup
                        if (!waitingPeriod)
                        {
                            primaryWeapon = input.Value.PrimaryWeaponAction;
                            secondaryWeapon = input.Value.SecondaryWeaponAction;
                        }

                        playerNode.Fire(primaryWeapon, secondaryWeapon);

                        playerNode.MoveToward(moveDir, delta);
                        playerNode.RotateToward(input.Value.MousePosition, delta);
                    }

                    // Cleanup the input vector
                    gameStates.playerInputs[networkPlayer.Key].Clear();

                    gameStates.playerInputs.Remove(networkPlayer.Key);

                    ClientData clientData = new ClientData();
                    clientData.Id = networkPlayer.Key + "";
                    clientData.Position = playerNode.Position;
                    clientData.Rotation = playerNode.Rotation;
                    clientData.PrimaryWeapon = primaryWeapon;
                    clientData.SecondaryWeapon = secondaryWeapon;
                    clientData.PrimaryWeaponIndex = playerNode.currentPrimaryWeaponIndex;
                    clientData.SecondaryWeaponIndex = playerNode.currentSecondaryWeaponIndex;
                    clientData.Health = playerNode.getHealth();

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
            String unitName = "client_" + spawnPlayerId;
            Team.TeamCode teamCode = spawnPlayers[unitName].GetTeam();

            _removeUnitOnNetwork(unitName);

            // Respawn if possible
            if (_teamMapAIs[(int)teamCode].isNewUnitAllow())
            {
                _respawnPlayer((int)teamCode + ";" + unitName);
            }
        }

        Godot.Collections.Array<String> removeSpawnBots = new Godot.Collections.Array<String>();

        foreach (Agent agent in spawnBots.Values)
        {
            // Locate the bot node
            Agent enemyNode = (Agent)_teamMapAIs[(int)agent.GetTeam()].GetUnitsContainer().GetNode(agent.Name);

            if (enemyNode == null || !IsInstanceValid(enemyNode))
            {
                // Ideally should give a warning that a bot node wasn't found
                continue;
            }


            int primaryWeapon = (int)GameStates.PlayerInput.InputAction.NOT_TRIGGER;
            int secondaryWeapon = (int)GameStates.PlayerInput.InputAction.NOT_TRIGGER;

            // If waiting period, no fire, else use the current bot setup
            if (!waitingPeriod)
            {
                primaryWeapon = enemyNode.PrimaryWeaponAction;
                secondaryWeapon = enemyNode.SecondaryWeaponAction;
            }

            enemyNode.Fire(primaryWeapon, secondaryWeapon);

            if (enemyNode.getHealth() > 0)
            {
                // Build bot_data entry
                ClientData clientData = new ClientData();
                clientData.Id = enemyNode.Name;
                clientData.Position = enemyNode.GlobalPosition;
                clientData.Rotation = enemyNode.GlobalRotation;
                clientData.Health = enemyNode.getHealth();
                clientData.PrimaryWeapon = primaryWeapon;
                clientData.SecondaryWeapon = secondaryWeapon;
                clientData.PrimaryWeaponIndex = enemyNode.currentPrimaryWeaponIndex;
                clientData.SecondaryWeaponIndex = enemyNode.currentSecondaryWeaponIndex;

                // Append into the snapshot
                snapshot.botData.Add(enemyNode.Name, clientData);

                // This logic is necessary to notify the AI that reload is pick up, so can continue with next state
                if (primaryWeapon == (int)GameStates.PlayerInput.InputAction.RELOAD)
                {
                    enemyNode.PrimaryWeaponAction = (int)GameStates.PlayerInput.InputAction.NOT_TRIGGER;
                }
                if (secondaryWeapon == (int)GameStates.PlayerInput.InputAction.RELOAD)
                {
                    enemyNode.SecondaryWeaponAction = (int)GameStates.PlayerInput.InputAction.NOT_TRIGGER;
                }
            }
            else
            {
                removeSpawnBots.Insert(0, enemyNode.Name);
            }
        }

        foreach (String spawnBotId in removeSpawnBots)
        {
            _removeUnitOnNetwork(spawnBotId);
        }

        if (removeSpawnBots.Count > 0)
        {
            _syncBots();
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

        Rpc(nameof(_getWaitingPeriodStatus), message);
        if (GetTree().IsNetworkServer())
        {
            _getWaitingPeriodStatus(message);
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
    private void _getWaitingPeriodStatus(String message)
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

            _checkGameWinningCondition();
        }

    }

    [Remote]
    private void getGamePeriodStatus(String message)
    {
        EmitSignal(nameof(WaitingPeriodSignal), message + " GAME PERIOD");

    }

    private void _checkGameWinningCondition()
    {
        // Only check after waiting period is over
        if (!waitingPeriod && timer != null)
        {
            bool endGame = false;

            if (internalTimer == 0)
            {
                timer.Stop();
                endGame = true;
            }


            if (!endGame)
            {
                int currentFieldTeam = 0;
                foreach (TeamMapAI currentAI in _teamMapAIs)
                {
                    if (currentAI.isNewUnitAllow())
                    {
                        // If current unit count is 0 and no longer can generate new unit
                        if (currentAI.GetUnitsContainer().GetChildren().Count != 0 || currentAI.isNewUnitAllow())
                        {
                            currentFieldTeam++;
                        }
                    }
                }

                if (currentFieldTeam == 1)
                {
                    endGame = true;
                }
            }

            if (endGame)
            {
                String message = validateGameResult();

                Rpc(nameof(notifyEndGameStatus), validateGameResult());

                notifyEndGameStatus(message);
            }
        }
    }

    private String validateGameResult()
    {
        String message = "";

        Team.TeamCode winTeam = Team.TeamCode.NEUTRAL;
        int teamCounter = 0;

        // If it is time, choose the winer team as 
        if (internalTimer != 0)
        {
            foreach (TeamMapAI currentAI in _teamMapAIs)
            {
                if (currentAI.isNewUnitAllow())
                {
                    // If current unit count is 0 and no longer can generate new unit
                    if (currentAI.GetUnitsContainer().GetChildren().Count != 0 || currentAI.isNewUnitAllow())
                    {
                        winTeam = currentAI.GetCurrentTeam();
                        teamCounter = 1;
                    }
                }
            }

        }
        else
        {
            int largestUnitCount = -1;

            foreach (TeamMapAI currentAI in _teamMapAIs)
            {
                if (currentAI.isNewUnitAllow())
                {
                    // If current unit count is 0 and no longer can generate new unit
                    if (currentAI.isNewUnitAllow())
                    {
                        if (currentAI.GetUnitsContainer().GetChildren().Count >= largestUnitCount)
                        {
                            largestUnitCount = currentAI.GetUnitsContainer().GetChildren().Count;
                            winTeam = currentAI.GetCurrentTeam();
                            teamCounter++;

                        }
                    }
                }
            }

        }


        if (teamCounter == 1)
        {
            message = "Winning Team is " + winTeam;

            if (network.gamestateNetworkPlayer.team == (int)winTeam)
            {
                message = "You Win!;" + message;
            }
            else
            {
                message = "You Lost;" + message;
            }
        }
        else
        {
            message = "It Is a Tie; There are more than one team reach equal amount of winning condition";
        }


        // Record elaspse time
        int elapseTime = MaxGameTime - internalTimer;

        int hour = elapseTime / 3600;

        int minutes = (elapseTime % 3600) / 60;

        int seconds = (elapseTime % 3600) % 60;

        message = message + ";" + formatTwoDigits(hour) + ":" + formatTwoDigits(minutes) + ":" + formatTwoDigits(seconds);

        return message;
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

        if (GetTree().IsNetworkServer() && pininfo.net_id != 1)
        {
            // We are on the server and the requested spawn does not belong to the server
            // Iterate through the connected players
            int s_index = 1; // Will be used as spawn index

            foreach (KeyValuePair<int, NetworkPlayer> item in network.networkPlayers)
            {
                // If it is not one of active player, then no need to sync (as the player is either observer or dead)
                if (!spawnPlayers.ContainsKey("client_" + item.Key))
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
            foreach (Agent spawnAgent in spawnBots.Values)
            {
                RpcId(pininfo.net_id, nameof(_addBotOnNetwork), (int)spawnAgent.GetTeam() + ";" + spawnAgent.Name);
            }

            // Sync the destoryed obstacles
            foreach (String obstacle in obstaclesDestroyed)
            {
                RpcId(pininfo.net_id, nameof(destroyObstacle), obstacle);
            }

            // Sync the waiting period if it is already end
            if (!waitingPeriod)
            {
                RpcId(pininfo.net_id, nameof(_getWaitingPeriodStatus), "00:00");
            }
        }

        // Load the scene and create an instance
        Player agent = (Player)(_teamMapAIs[pininfo.team].SpawnUnit("client_" + pininfo.net_id, false));

        // If this actor does not belong to the server, change the network master accordingly
        if (pininfo.net_id != 1)
        {
            agent.SetNetworkMaster(pininfo.net_id);
        }

        // If this actor is the current client controlled, add camera and attach HUD
        if (pininfo.net_id == network.gamestateNetworkPlayer.net_id)
        {
            Camera2D camera2D = new Camera2D();
            camera2D.Name = "Camera2D";
            agent.AddChild(camera2D);
            agent.SetHUD((HUD)GetNode("HUD"));

            _setCameraLimit(agent);
        }

        spawnPlayers.Add(agent.Name, agent);
    }

    [Remote]
    private void _respawnPlayer(String unitInfo)
    {
        int team = int.Parse(unitInfo.Split(";")[0]);
        String unitName = unitInfo.Split(";")[1];
        int netId = int.Parse(unitName.Split("_")[1]);

        if (GetTree().IsNetworkServer())
        {
            foreach (KeyValuePair<int, NetworkPlayer> item in network.networkPlayers)
            {
                // Spawn the new player within the currently iterated player as long it's not the server
                // Because the server's list already contains the new player, that one will also get itself!
                if (item.Key != 1)
                {
                    RpcId(item.Key, nameof(_respawnPlayer), unitInfo);
                }
            }
        }

        // Load the scene and create an instance
        Player agent = (Player)(_teamMapAIs[team].SpawnUnit(unitName, false));

        // If this actor does not belong to the server, change the network master accordingly
        if (netId != 1)
        {
            agent.SetNetworkMaster(netId);
        }

        // If this actor is the current client controlled, add camera and attach HUD
        if (netId == network.gamestateNetworkPlayer.net_id)
        {
            Camera2D camera2D = new Camera2D();
            camera2D.Name = "Camera2D";
            agent.AddChild(camera2D);
            agent.SetHUD((HUD)GetNode("HUD"));

            _setCameraLimit(agent);
        }

        // If observer is setup, clean it up
        if (HasNode("client_observer_" + netId))
        {
            GetNode("client_observer_" + netId).QueueFree();
        }

        spawnPlayers.Add(agent.Name, agent);
    }

    [Remote]
    private void _removeUnitOnNetwork(String id)
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

                RpcId(item.Key, nameof(_removeUnitOnNetwork), id);
            }
        }

        if (spawnBots.ContainsKey(id))
        {
            _removeUnit(id, false);
        }
        else
        {
            _removeUnit(id, true);
        }
    }

    private void _syncBots()
    {
        if (GetTree().IsNetworkServer())
        {
            // Calculate the target amount of spawned bots
            int bot_count = network.serverinfo.max_players - network.networkPlayers.Count;

            if (spawnBots.Count > bot_count)
            {
                while (spawnBots.Count > bot_count)
                {
                    foreach (Agent spawnBot in spawnBots.Values)
                    {
                        _removeUnit(spawnBot.Name, false);

                        break;
                    }
                }
            }
            else if (spawnBots.Count < bot_count)
            {
                // If bot_count
                while (spawnBots.Count < bot_count)
                {
                    TeamMapAI targetAI = null;
                    // Set the initial to max bot count
                    int smallestUnitCount = bot_count;

                    foreach (TeamMapAI currentAI in _teamMapAIs)
                    {
                        if (currentAI.isNewUnitAllow())
                        {
                            if (currentAI.GetUnitsContainer().GetChildren().Count <= smallestUnitCount)
                            {
                                smallestUnitCount = currentAI.GetUnitsContainer().GetChildren().Count;
                                targetAI = currentAI;
                            }
                        }
                    }

                    if (targetAI != null)
                    {
                        GD.Print("GENERATE BOT FOR " + targetAI.GetCurrentTeam() + " BOT ID bot_" + botCounter);

                        String botId = (int)targetAI.GetCurrentTeam() + ";" + "bot_" + botCounter;
                        botCounter++;

                        Rpc(nameof(_addBotOnNetwork), botId);

                        _addBotOnNetwork(botId);
                    }
                    else
                    {
                        // No longer allowed to add more bot now, so exit loop
                        break;
                    }
                }
            }

            _checkGameWinningCondition();
        }

    }

    [Remote]
    private void _addBotOnNetwork(String botId)
    {
        Team.TeamCode team = (Team.TeamCode)int.Parse(botId.Split(";")[0]);
        String unitName = botId.Split(";")[1];

        if (!spawnBots.ContainsKey(unitName))
        {
            spawnBots.Add(unitName, _teamMapAIs[(int)team].SpawnUnit(unitName, true));
        }
    }

    public override void _PhysicsProcess(float delta)
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

    private void _setCameraLimit(Agent agent)
    {
        TileMap tileMap = (TileMap)GetNode("Navigation2D/Ground");
        Rect2 mapLimit = tileMap.GetUsedRect();
        Vector2 mapCellSize = tileMap.CellSize;

        Camera2D playerCamera = (Camera2D)(GetNode(_teamMapAIs[(int)agent.GetTeam()].Name + "/" + _teamMapAIs[(int)agent.GetTeam()].GetUnitsContainer().Name + "/" + agent.Name + "/Camera2D"));
        playerCamera.Current = true;
        playerCamera.Zoom = new Vector2(1.4f, 1.4f);
        playerCamera.LimitLeft = (int)(mapLimit.Position.x * mapCellSize.x);
        playerCamera.LimitRight = (int)(mapLimit.End.x * mapCellSize.x);
        playerCamera.LimitTop = (int)(mapLimit.Position.y * mapCellSize.y);
        playerCamera.LimitBottom = (int)(mapLimit.End.y * mapCellSize.y);
    }

    private void _onProjectileShoot(PackedScene projectile, Vector2 _position, Vector2 _direction, Node2D source, Team sourceTeam, Node2D target)
    {
        Projectile newProjectile = (Projectile)projectile.Instance();
        AddChild(newProjectile);
        newProjectile.Initialize(_position, _direction, source, sourceTeam, target);
    }

    private void _onDamageCalculation(int damage, Vector2 hitDir, Godot.Object source, Team sourceTeam, Godot.Object target)
    {
        if (target != null && IsInstanceValid(target))
        {
            if (target.HasMethod(nameof(Agent.TakeDamage)))
            {
                Agent targetAgent = (Agent)(target);
                Agent sourceAgent = (Agent)(source);
                targetAgent.TakeDamage(damage, hitDir, sourceAgent, sourceTeam);
            }
            else if (target.HasMethod(nameof(Obstacle.TakeEnvironmentDamage)))
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
