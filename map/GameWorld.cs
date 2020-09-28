using Godot;
using System;
using System.Collections.Generic;

public class GameWorld : Node2D
{

    [Signal]
    public delegate void SnapshotReceivedSignal();

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


    int spawned_bots = 0;

    Dictionary<String, SpawnBot> spawnBots = new Dictionary<String, SpawnBot>();

    Network network;

    long botCounter = 0;
    private long MAX_BOT_COUNT = 1000;

    float currentTime;

    // The "signature" (timestamp) added into each generated state snapshot
    int snapshotSignature = 1;

    // The signature of the last snapshot received
    int lastSnapshotSignature = 0;

    private Navigation2D navigation2D;

    private AStar aStar;

    private Godot.RandomNumberGenerator random;

    private RaycastAStar aStarSolver;

    // Called when the node enters the scene tree for the first time.
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

                    Obstacle obstacle = (Obstacle)((PackedScene)GD.Load("res://environments/Obstacle.tscn")).Instance();
                    obstacle.type = item;
                    obstacle.Position = position;

                    obstacle.Name = "obstacle_" + xIndex + "_" + yIndex;

                    GetNode("Obstacles").AddChild(obstacle);
                }
            }
        }

    }

    public Godot.Collections.Array getPaths(Vector2 start, Vector2 end, World2D space_state, Godot.Collections.Array excludes)
    {
        return aStarSolver.path(start, end, space_state, excludes, this);
    }


    public Godot.Collections.Array getPaths2(Vector2 startPosition, Vector2 endPosition)
    {

        Godot.Collections.Array pathArray = new Godot.Collections.Array();
        if ((Navigation2D)GetNode("Navigation2D") != null)
        {
            Vector2[] paths = ((Navigation2D)GetNode("Navigation2D")).GetSimplePath(startPosition, endPosition);

            foreach (Vector2 point in paths)
            {
                pathArray.Add(point);
            }

        }

        return pathArray;
    }


    // Based on the "High level" snapshot data, encodes into a byte array
    // ready to be sent across the network. This function does not return
    // the data, just broadcasts it to the connected players. To that end,
    // it is meant to be run only on the server
    private void encodeSnapshot(Snapshot snapshot)
    {
        if (!GetTree().IsNetworkServer())
        {
            return;
        }
        String encodedData = "";

        // First add the snapshot signature (timestamp)
        encodedData = encodedData + snapshot.signature + ";";

        // Player data count
        encodedData = encodedData + snapshot.playerData.Count + ";";
        // snapshot_data should contain a "players" field which must be an array
        // of player data. Each entry in this array should be a dictionary, containing
        // the following fields: network_id, position, rotation, col
        foreach (ClientData item in snapshot.playerData.Values)
        {
            encodedData = encodedData + item.id + ";";
            encodedData = encodedData + item.position.x + ";";
            encodedData = encodedData + item.position.y + ";";
            encodedData = encodedData + item.rotation + ";";
            encodedData = encodedData + item.primaryWepaon + ";";
            encodedData = encodedData + item.secondaryWepaon + ";";
            encodedData = encodedData + item.health + ";";
            encodedData = encodedData + item.primaryWeaponIndex + ";";
            encodedData = encodedData + item.secondaryWeaponIndex + ";";
        }
        // Bot data count
        encodedData = encodedData + snapshot.botData.Count + ";";
        // The bot_data field should be an array, each entry containing the following
        // fields: bot_id, position, rotation
        foreach (ClientData item in snapshot.botData.Values)
        {
            encodedData = encodedData + item.id + ";";
            encodedData = encodedData + item.position.x + ";";
            encodedData = encodedData + item.position.y + ";";
            encodedData = encodedData + item.rotation + ";";
            encodedData = encodedData + item.primaryWepaon + ";";
            encodedData = encodedData + item.secondaryWepaon + ";";
            encodedData = encodedData + item.health + ";";
            encodedData = encodedData + item.primaryWeaponIndex + ";";
            encodedData = encodedData + item.secondaryWeaponIndex + ";";
        }

        // First add the snapshot signature (timestamp)
        RpcUnreliable(nameof(clientGetSnapshot), encodedData);
    }

    [Remote]
    private void clientGetSnapshot(String encodedData)
    {
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
            if (!HasNode("client_" + item.id))
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
                if (!HasNode(item.id))
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
        Tank playerNode = (Tank)GetNode("client_" + id);

        if (playerNode == null)
        {
            GD.Print("Cannoot remove invalid node from tree");
            return;
        }

        // Mark the node for deletion
        playerNode.explode();
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

        foreach (KeyValuePair<int, NetworkPlayer> networkPlayer in network.networkPlayers)
        {
            // Locate the player's node. Even if there is no input/update, it's state will be dumped
            // into the snapshot anyway
            Player playerNode = (Player)GetNode("client_" + networkPlayer.Value.net_id);

            if (playerNode == null)
            {
                // Ideally should give a warning that a player node wasn't found
                continue;
            }

            Vector2 pPosition = playerNode.Position;
            float pRotation = playerNode.Rotation;


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
                    primaryWeapon = input.Value.primaryWepaon;
                    secondaryWeapon = input.Value.secondaryWepaon;
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

        // Clean the input
        gameStates.playerInputs.Clear();

        Godot.Collections.Array<String> removeSpawnBots = new Godot.Collections.Array<String>();

        foreach (SpawnBot spawnBot in spawnBots.Values)
        {
            // Locate the bot node
            Enemy enemyNode = (Enemy)GetNode(spawnBot.name);

            if (enemyNode == null)
            {
                // Ideally should give a warning that a bot node wasn't found
                continue;
            }

            enemyNode._shoot(enemyNode.isPrimaryWeapon, enemyNode.isSecondaryWeapon);

            if (enemyNode.getHealth() > 0)
            {
                // Build bot_data entry
                ClientData clientData = new ClientData();
                clientData.id = spawnBot.name;
                clientData.position = enemyNode.GlobalPosition;
                clientData.rotation = enemyNode.GlobalRotation;
                clientData.health = enemyNode.getHealth();
                clientData.primaryWepaon = enemyNode.isPrimaryWeapon;
                clientData.secondaryWepaon = enemyNode.isSecondaryWeapon;
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
            Rpc(nameof(removeClient), id);
        }

        if (spawnBots.ContainsKey(id))
        {
            removeBot(id);
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
                }
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

    private void _onPlayerDead()
    {
        ((GameStates)GetNode("/root/GAMESTATES")).restart();
    }
}
