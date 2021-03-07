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
    private delegate void PlayerCreateSignal();

    [Signal]
    private delegate void WaitingPeriodSignal();

    protected String AgentPrefix = "agent_";

    private String _agentPlayerPrefix = "agent_player_";

    private String _agentObserverPrefix = "agent_observer_";

    protected GameStates GameStates;

    private class SpwanInfo
    {
        public long spawn_index { get; set; }
        public NetworkPlayer networkPlayer { get; set; }
    }

    public class ClientData : Godot.Object
    {
        public String Id;
        public Vector2 Position;
        public float Rotation;
        public int RightWeapon;
        public int LeftWeapon;
        public int Health;
        public int RightWeaponIndex;
        public int LeftWeaponIndex;
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
        public int LeftWeapon;
        public int Health;
        public float Time;
        public Node2D Node;
    }

    Dictionary<String, ClientState> clientStates = new Dictionary<String, ClientState>();



    int spawned_bots = 0;

    protected Dictionary<String, Agent> SpawnBots = new Dictionary<String, Agent>();
   protected Dictionary<String, Agent> spawnPlayers = new Dictionary<String, Agent>();


    Network network;

    protected long AgentBotCounter = 0;
    long _agentPlayerCounter = 0;

    long observerCounter = 0;

    protected float CurrentTime;

    // The "signature" (timestamp) added into each generated state snapshot
    // int max is 2,147,483,647 
    // 2,147,483,647 / 60 (snapshots) / 60 (seconds => 1 min) /60 (mins = > 1 hr) /24 (24 hrs => 1 day) = 414 days (before the snapshot expire)
    // So as long as this round of games end in 1 year, the snapshot signature will be unique
    int snapshotSignature = 1;

    // The signature of the last snapshot received
    int lastSnapshotSignature = 0;

    private PathFinding _pathFinding;

    protected Godot.RandomNumberGenerator Random;

    // Called when the node enters the scene tree for the first time.

    private float currentNetworkBytes = 0;
    private float currentNetworkSnapshots = 0;
    private float currentAppliedNetworkSnapshots = 0;

    private bool _waitingPeriod = true;

    [Export]
    private int MaxWaitingTime = 5;

    [Export]
    private int MaxGameTime = 3600;

    // Use as tick to track countdown time
    private int internalTimer;

    protected CapaturableBaseManager CapaturableBaseManager;

    protected Godot.Collections.Array<TeamMapAI> TeamMapAIs;

    private Timer _timer;

    private ObstacleManager _obstacleManager;

    private TileMap _tileMap;

    private GameCamera _camera2D;

    private HUD _hud;
    private PopUpMessage _popUpMessage;
    private MiniMap _miniMap;
    private InventoryManager _inventoryManager;

    public override void _Ready()
    {
        GameStates = (GameStates)GetNode("/root/GAMESTATES");

        InitializeInventoryManager();

        InitializeCamera();
        InitializeTileMap();
        InitializeObsctacleManager();
        InitializeCapaturableBaseManager();
        InitializeTeamMapAI();

        InitializeHUD();

        Input.SetCustomMouseCursor(GD.Load("res://assets/ui/blue_cross.png"), Input.CursorShape.Arrow, new Vector2(16, 16));

        _initializeNetwork();
        _initailizeGameTimer();
    }

    public Godot.Collections.Array<TeamMapAI> GetTeamMapAIs()
    {
        return TeamMapAIs;
    }

    protected void InitializeCamera()
    {
        _camera2D = (GameCamera)GetNode("GameCamera");
        _camera2D.Current = true;
    }

    protected void InitializeHUD()
    {
        _hud = (HUD)GetNode("HUD");
        _miniMap = (MiniMap)_hud.GetNode("controlGame/MiniMap");
        _popUpMessage = (PopUpMessage)_hud.GetNode("PopUpMessage");

        _miniMap.Iniitialize(CapaturableBaseManager);
    }

    private void _initializeNetwork()
    {
        // After receiving and fully decoding a new snapshot, apply it to the game world
        this.Connect(nameof(SnapshotReceivedSignal), this, nameof(applySnapshot));

        network = (Network)GetNode("/root/NETWORK");
        network.Connect(nameof(Network.DisconnectedSignal), this, nameof(onDisconnected));

        if (GetTree().IsNetworkServer())
        {
            // Connect player remove logic
            network.Connect(nameof(Network.PlayerRemovedSignal), this, nameof(onPlayerRemoved));
        
            // Spawn the players
            _initializeNewPlayer(network.gamestateNetworkPlayer.ToString());
            _syncBots();
        }
        else
        {
            RpcId(1, nameof(_initializeNewPlayer), network.gamestateNetworkPlayer.ToString());
        }


        // Update network flow
        this.Connect(nameof(NeworkRateUpdateSignal), _hud, "_onNetworkRateUpdate");
        this.Connect(nameof(PlayerDefeatedSignal), _hud, nameof(HUD.OnPlayerDefeated));
        this.Connect(nameof(Network.PlayerListChangedSignal), _hud, "onPlayerListChanged");
        this.Connect(nameof(WaitingPeriodSignal), _hud, "_onUpdateTimer");
        this.Connect(nameof(PlayerCreateSignal), _hud, nameof(HUD.OnPlayerCreated));

        // Update playerlist
        EmitSignal(nameof(Network.PlayerListChangedSignal));
    }

    private void _initailizeGameTimer()
    {
        _waitingPeriod = true;

        // Set the timer on server to do waiting period count down
        if (GetTree().IsNetworkServer())
        {
            _timer = (Timer)GetNode("Timer");
            _timer.WaitTime = 1;
            internalTimer = MaxWaitingTime;
            _timer.Connect("timeout", this, nameof(waitingPeriodTimerTimeout));
            _timer.Start();
        }
    }

    protected void InitializeTileMap()
    {
        _tileMap = (TileMap)GetNode("Ground");
    }

    protected void InitializeObsctacleManager()
    {
        _obstacleManager = (ObstacleManager)GetNode("ObstacleManager");
        _obstacleManager.Initialize(_tileMap);

        if (GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
        {
            _pathFinding = (PathFinding)((PackedScene)GD.Load("res://ai/PathFinding.tscn")).Instance(); ;
            this.AddChild(_pathFinding);
            _pathFinding.Initialize(_tileMap, _obstacleManager);
        }
    }

    protected void InitializeCapaturableBaseManager()
    {
        CapaturableBaseManager = (CapaturableBaseManager)GetNode("CapaturableBaseManager");
        CapaturableBaseManager.Initailize(this);
    }

    protected void InitializeTeamMapAI()
    {
        TeamMapAIs = new Godot.Collections.Array<TeamMapAI>();

        // Start with neutral and above
        for (int index = 0; index < (int)(Team.TeamCode.NEUTRAL); index++)
        {
            TeamMapAI ai = (TeamMapAI)((PackedScene)GD.Load("res://ai/TeamMapAI.tscn")).Instance();
            ai.Name = nameof(TeamMapAI) + "_" + (Team.TeamCode)index;
            AddChild(ai);

            ai.Initialize(this, _inventoryManager,  CapaturableBaseManager.GetBases(), (Team.TeamCode)index, _pathFinding);

            TeamMapAIs.Add(ai);

            foreach (CapturableBase capturable in CapaturableBaseManager.GetBases())
            {
                capturable.Connect(nameof(CapturableBase.BaseTeamChangeSignal), ai, nameof(TeamMapAI.HandleCapturableBaseCaptured));
            }
        }
    }

    protected void InitializeInventoryManager()
    {
        _inventoryManager = (InventoryManager)GetNode("InventoryManager");
        _inventoryManager.Initialize(this);
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
                    encodedData = encodedData + encodeClientData(clientValues[index]);
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
                    encodedData = encodedData + encodeClientData(botValues[index]);
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
        clientData.RightWeapon = int.Parse(encodedData.Split(";")[parseIndex]);
        parseIndex++;
        clientData.LeftWeapon = int.Parse(encodedData.Split(";")[parseIndex]);
        parseIndex++;
        clientData.Health = int.Parse(encodedData.Split(";")[parseIndex]);
        parseIndex++;
        clientData.RightWeaponIndex = int.Parse(encodedData.Split(";")[parseIndex]);
        parseIndex++;
        clientData.LeftWeaponIndex = int.Parse(encodedData.Split(";")[parseIndex]);
        parseIndex++;

        return parseIndex;
    }

    private String encodeClientData(ClientData clientData)
    {
        String encodedData = "" + clientData.Id + ";";
        encodedData = encodedData + clientData.Position.x + ";";
        encodedData = encodedData + clientData.Position.y + ";";
        encodedData = encodedData + clientData.Rotation + ";";
        encodedData = encodedData + clientData.RightWeapon + ";";
        encodedData = encodedData + clientData.LeftWeapon + ";";
        encodedData = encodedData + clientData.Health + ";";
        encodedData = encodedData + clientData.RightWeaponIndex + ";";
        encodedData = encodedData + clientData.LeftWeaponIndex + ";";

        return encodedData;
    }

    private void applySnapshot(Snapshot snapshot)
    {
        // In here we assume the obtained snapshot is newer than the last one
        // Iterate through player data 
        foreach (ClientData item in snapshot.playerData.Values)
        {
            _updateAgentStateFromSnapshot(_agentPlayerPrefix + item.Id, item);
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

        Agent agent = null;
        if (spawnPlayers.ContainsKey(agentNodeName))
        {
            agent = spawnPlayers[agentNodeName];
        }

        if (SpawnBots.ContainsKey(agentNodeName))
        {
            agent = SpawnBots[agentNodeName];
        }


        if (agent == null || !IsInstanceValid(agent))
        {
            return;
        }

        agent.changeWeapon(item.RightWeaponIndex, Weapon.WeaponOrder.Right);
        agent.changeWeapon(item.LeftWeaponIndex, Weapon.WeaponOrder.Left);

        agent.Sync(item.Position, item.Rotation, item.RightWeapon, item.LeftWeapon);
        agent.setHealth(item.Health);
    }

    private void onPlayerRemoved(int id)
    {
        removeDisconnectedPlayer(id);
        _syncBots();
    }

    private void onDisconnected()
    {
        GameStates.restart();
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
                RpcId(item.Key, nameof(removeDisconnectedPlayer), id);
            }
        }

        _removeUnit(_agentPlayerPrefix + id, true);
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
            currentSpawnCache = SpawnBots;
        }

        // Need to check if keys are there, it may not be a bug
        if (currentSpawnCache.ContainsKey(id) && TeamMapAIs[(int)currentSpawnCache[id].GetCurrentTeam()].GetUnit(currentSpawnCache[id].Name) != null)
        {
            existingPosition = currentSpawnCache[id].GlobalPosition;


            // Clean up the agent marker on minimap if not the agent of the player of this machine
            if (!(player && id == _agentPlayerPrefix + network.gamestateNetworkPlayer.net_id))
            {
                _miniMap.RemoveAgent(currentSpawnCache[id]);
            }
            else
            {
                // Remove the player agent marker
                _miniMap.RemovePlayer();
            }


            _popUpMessage.NotifyMessage("NOTIFICATION", currentSpawnCache[id].GetUnitName() + " (" + currentSpawnCache[id].GetCurrentTeam().ToString() + ") IS ELIMINATED");

            TeamMapAIs[(int)currentSpawnCache[id].GetCurrentTeam()].RemoveUnit(currentSpawnCache[id].Name);
        }

        if (currentSpawnCache.ContainsKey(id))
        {
            currentSpawnCache.Remove(id);
        }

        // If this is the player attach to current client, respawn the client with observer
        if (player && id == _agentPlayerPrefix + network.gamestateNetworkPlayer.net_id)
        {

            createObserver(existingPosition);
            EmitSignal(nameof(PlayerDefeatedSignal));
        }

        // Shake camera
        if (Mathf.Abs(existingPosition.x - _camera2D.GlobalPosition.y) < GetViewport().Size.x &&
         Mathf.Abs(existingPosition.y - _camera2D.GlobalPosition.y) < GetViewport().Size.y)
        {
            _camera2D.StartScreenShake();
        }


        if (GetTree().IsNetworkServer())
        {
            _checkGameWinningCondition();
        }
    }

    private void createObserver(Vector2 position)
    {
        // Load the scene and create an instance
        Observer client;

        client = (Observer)((PackedScene)GD.Load("res://agents/Observer.tscn")).Instance();

        client.Position = position;
        client.Name = _agentObserverPrefix + observerCounter;

        AddChild(client);

        client.SetCameraRemotePath(_camera2D);
    }

    [Remote]
    private void _syncCapturableBase(String info)
    {
        int baseIndex = int.Parse(info.Split(";")[0]);
        int team = int.Parse(info.Split(";")[1]);

        ((CapturableBase)CapaturableBaseManager.GetBases()[baseIndex]).SetCaptureBaseTeam((Team.TeamCode)team);
    }

    // Update and generate a game state snapshot
    private void _updateState()
    {
        // If not on the server, bail
        if (!GetTree().IsNetworkServer())
        {
            return;
        }

        // Initialize the "high level" snapshot
        Snapshot snapshot = new Snapshot();
        snapshot.signature = snapshotSignature;

        Godot.Collections.Array<String> removeSpawnPlayers = new Godot.Collections.Array<String>();

        foreach (KeyValuePair<int, NetworkPlayer> networkPlayer in network.networkPlayers)
        {
            String playerId = _agentPlayerPrefix + networkPlayer.Value.net_id;
            // Node may not being created yet
            if (!spawnPlayers.ContainsKey(playerId))
            {
                // Ideally should give a warning that a player node wasn't found
                continue;
            }

            Agent playerNode = spawnPlayers[playerId];

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
                if (GameStates.playerInputs.ContainsKey(networkPlayer.Key) && GameStates.playerInputs[networkPlayer.Key].Count > 0)
                {

                    int rightWeapon = 0;
                    int leftWeapon = 0;

                    // Calculate the delta
                    float delta = GameStates.updateDelta / (float)(GameStates.playerInputs[networkPlayer.Key].Count);

                    foreach (KeyValuePair<int, GameStates.PlayerInput> input in GameStates.playerInputs[networkPlayer.Key])
                    {
                        Vector2 moveDir = new Vector2();
                        moveDir.y = -1 * input.Value.Up;
                        moveDir.y += 1 * input.Value.Down;
                        moveDir.x = -1 * input.Value.Left;
                        moveDir.x += 1 * input.Value.Right;

                        playerNode.changeWeapon(input.Value.RightWeaponIndex, Weapon.WeaponOrder.Right);
                        playerNode.changeWeapon(input.Value.LeftWeaponIndex, Weapon.WeaponOrder.Left);

                        if (!_waitingPeriod)
                        {
                            rightWeapon = input.Value.RightWeaponAction;
                            leftWeapon = input.Value.LeftWeaponAction;
                        }
                        playerNode.Fire(Weapon.WeaponOrder.Right, rightWeapon);
                        playerNode.Fire(Weapon.WeaponOrder.Left, leftWeapon);

                        playerNode.MoveToward(moveDir, delta);
                        playerNode.RotateToward(input.Value.MousePosition, delta);
                    }

                    // Cleanup the input vector
                    GameStates.playerInputs[networkPlayer.Key].Clear();

                    GameStates.playerInputs.Remove(networkPlayer.Key);

                    ClientData clientData = new ClientData();
                    clientData.Id = networkPlayer.Key + "";
                    clientData.Position = playerNode.Position;
                    clientData.Rotation = playerNode.Rotation;
                    clientData.RightWeapon = rightWeapon;
                    clientData.LeftWeapon = leftWeapon;
                    clientData.RightWeaponIndex = playerNode.GetCurrentWeaponIndex(Weapon.WeaponOrder.Right);
                    clientData.LeftWeaponIndex = playerNode.GetCurrentWeaponIndex(Weapon.WeaponOrder.Left);
                    clientData.Health = playerNode.getHealth();

                    snapshot.playerData.Add(networkPlayer.Key, clientData);
                }
            }
            else
            {
                removeSpawnPlayers.Insert(0, playerId);
            }
        }

        // Clean the input
        GameStates.playerInputs.Clear();

        foreach (String spawnPlayerId in removeSpawnPlayers)
        {
            // Respawn dead player if that team still allow new unit
            Team.TeamCode teamCode = spawnPlayers[spawnPlayerId].GetCurrentTeam();
            String displayName = spawnPlayers[spawnPlayerId].GetDisplayName();
            _removeUnitOnNetwork(spawnPlayerId);

            // Respawn if that team still allow new unit
            if (TeamMapAIs[(int)teamCode].isNewUnitAllow())
            {
                _spawnPlayer(spawnPlayerId.Replace(_agentPlayerPrefix, "") + ";" + (int)teamCode + ";" + _agentPlayerPrefix + _agentPlayerCounter + ";" + displayName);
                _agentPlayerCounter++;
            }
        }

        Godot.Collections.Array<String> removeSpawnBots = new Godot.Collections.Array<String>();

        foreach (Agent agent in SpawnBots.Values)
        {
            // Locate the bot node
            Agent enemyNode = (Agent)TeamMapAIs[(int)agent.GetCurrentTeam()].GetUnit(agent.Name);

            if (enemyNode == null || !IsInstanceValid(enemyNode))
            {
                // Ideally should give a warning that a bot node wasn't found
                continue;
            }


            int rightWeapon = (int)GameStates.PlayerInput.InputAction.NOT_TRIGGER;
            int leftWeapon = (int)GameStates.PlayerInput.InputAction.NOT_TRIGGER;

            if (!_waitingPeriod)
            {
                rightWeapon = enemyNode.RightWeaponAction;
                leftWeapon = enemyNode.LeftWeaponAction;
            }

            enemyNode.Fire(Weapon.WeaponOrder.Right, rightWeapon);
            enemyNode.Fire(Weapon.WeaponOrder.Left, leftWeapon);

            if (enemyNode.getHealth() > 0)
            {
                // Build bot_data entry
                ClientData clientData = new ClientData();
                clientData.Id = enemyNode.Name;
                clientData.Position = enemyNode.GlobalPosition;
                clientData.Rotation = enemyNode.GlobalRotation;
                clientData.Health = enemyNode.getHealth();
                clientData.RightWeapon = rightWeapon;
                clientData.LeftWeapon = leftWeapon;
                clientData.RightWeaponIndex = enemyNode.GetCurrentWeaponIndex(Weapon.WeaponOrder.Right);
                clientData.LeftWeaponIndex = enemyNode.GetCurrentWeaponIndex(Weapon.WeaponOrder.Left);

                // Append into the snapshot
                snapshot.botData.Add(enemyNode.Name, clientData);

                // This logic is necessary to notify the AI that reload is pick up, so can continue with next state
                if (rightWeapon == (int)GameStates.PlayerInput.InputAction.RELOAD)
                {
                    enemyNode.RightWeaponAction = (int)GameStates.PlayerInput.InputAction.NOT_TRIGGER;
                }
                if (leftWeapon == (int)GameStates.PlayerInput.InputAction.RELOAD)
                {
                    enemyNode.LeftWeaponAction = (int)GameStates.PlayerInput.InputAction.NOT_TRIGGER;
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
            _timer.Stop();
        }

        Rpc(nameof(_getWaitingPeriodStatus), message);
        _getWaitingPeriodStatus(message);

        if (GetTree().IsNetworkServer())
        {
            // Start to count against game time
            if (internalTimer == 0)
            {
                internalTimer = MaxGameTime;

                if (_timer.IsConnected("timeout", this, nameof(waitingPeriodTimerTimeout)))
                {
                    _timer.Disconnect("timeout", this, nameof(waitingPeriodTimerTimeout));
                }

                _timer.Connect("timeout", this, nameof(gamePeriodTimeout));
                _timer.Start();
            }
        }
    }

    [Remote]
    private void _getWaitingPeriodStatus(String message)
    {
        EmitSignal(nameof(WaitingPeriodSignal), message + " WAITING PERIOD");
        if (message == "00:00:00")
        {
            _waitingPeriod = false;
            _popUpMessage.NotifyMessage("OBJECTIVE", "CAPTURE ALL BASES AND ELIMIATED ALL ENEMIES", 10);

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
        if (!_waitingPeriod)
        {
            bool endGame = false;

            if (internalTimer == 0)
            {
                _timer.Stop();
                endGame = true;
            }

            if (endGame == false)
            {
                int currentFieldTeam = 0;
                foreach (TeamMapAI currentAI in TeamMapAIs)
                {
                    if (currentAI.isNewUnitAllow() || currentAI.GetUnitsContainer().GetChildren().Count != 0)
                    {

                        currentFieldTeam++;
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
            foreach (TeamMapAI currentAI in TeamMapAIs)
            {
                if (currentAI.isNewUnitAllow() || currentAI.GetUnitsContainer().GetChildren().Count != 0)
                {
                    winTeam = currentAI.GetCurrentTeam();
                    teamCounter = 1;
                }
            }

        }
        else
        {
            int largestUnitCount = -1;

            foreach (TeamMapAI currentAI in TeamMapAIs)
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
        GameStates.setMessagesForNextScene(message);
        GameStates.endGameScreen();
    }

    public bool IsWaitingPeriod()
    {
        return _waitingPeriod;
    }

    [Remote]
    private void _initializeNewPlayer(String info)
    {
        NetworkPlayer pininfo = new NetworkPlayer(info);

        if (GetTree().IsNetworkServer())
        {
            _spawnPlayer(pininfo.net_id + ";" + pininfo.team + ";" + _agentPlayerPrefix + _agentPlayerCounter + ";" + pininfo.name);
            _agentPlayerCounter++;
        }

        // Propagate info of obstacles to other
        if (GetTree().IsNetworkServer() && pininfo.net_id != 1)
        {
            // Add current bot info to new player
            foreach (String playerIDs in spawnPlayers.Keys)
            {
                RpcId(pininfo.net_id, nameof(_spawnPlayer), playerIDs.Replace(_agentPlayerPrefix, "") + ";" + (int)spawnPlayers[playerIDs].GetCurrentTeam() + ";" + spawnPlayers[playerIDs].Name + ";" + spawnPlayers[playerIDs].GetDisplayName());
                // Sync inventory
                _inventoryManager.SyncInventory(pininfo.net_id, spawnPlayers[playerIDs]);
            }

            // Add current bot info to new player
            foreach (Agent spawnAgent in SpawnBots.Values)
            {
                RpcId(pininfo.net_id, nameof(_addBotOnNetwork), (int)spawnAgent.GetCurrentTeam() + ";" + spawnAgent.Name);
                // Sync inventory
                _inventoryManager.SyncInventory(pininfo.net_id, spawnAgent);
            }

            int index = 0;
            foreach (CapturableBase capturableBase in CapaturableBaseManager.GetBases())
            {
                RpcId(pininfo.net_id, nameof(_syncCapturableBase), index + ";" + (int)capturableBase.GetCaptureBaseTeam());
                index++;
            }

            // Sync the destoryed obstacles
            _obstacleManager.syncObstacles(pininfo.net_id);

            // Sync the waiting period if it is already end
            if (!_waitingPeriod)
            {
                RpcId(pininfo.net_id, nameof(_getWaitingPeriodStatus), "00:00");
            }
        }
    }

    [Remote]
    private void _spawnPlayer(String unitInfo)
    {
        int netId = int.Parse(unitInfo.Split(";")[0]);
        int team = int.Parse(unitInfo.Split(";")[1]);
        String unitName = unitInfo.Split(";")[2];
        String displayName = unitInfo.Split(";")[3];

        if (GetTree().IsNetworkServer())
        {
            foreach (KeyValuePair<int, NetworkPlayer> item in network.networkPlayers)
            {
                // Spawn the new player within the currently iterated player as long it's not the server
                // Because the server's list already contains the new player, that one will also get itself!
                if (item.Key != 1)
                {
                    RpcId(item.Key, nameof(_spawnPlayer), unitInfo);
                }
            }
        }

        _cratePlayer(netId, (Team.TeamCode)team, unitName, displayName);
    }

    private void _cratePlayer(int netId, Team.TeamCode team, String unitName, String displayName)
    {
        String playerId = _agentPlayerPrefix + netId;

        // Already generated
        if (spawnPlayers.ContainsKey(playerId))
        {
            return;
        }

        // If observer is setup, clean it up
        if (HasNode(_agentObserverPrefix + observerCounter))
        {
            GetNode(_agentObserverPrefix + observerCounter).QueueFree();
            observerCounter++;
        }

        // Load the scene and create an instance
        Player agent = (Player)(TeamMapAIs[(int)team].CreateUnit(unitName, displayName, false));

        // If this actor does not belong to the server, change the network master accordingly
        if (netId != 1)
        {
            agent.SetNetworkMaster(netId);
        }



        // If this actor is the current client controlled, add camera and attach HUD
        if (netId == network.gamestateNetworkPlayer.net_id)
        {
            // Attach camera
            agent.SetHUD(_hud, _inventoryManager);
            agent.SetCameraRemotePath(_camera2D);

            // Set player marker
            _miniMap.SetPlayer(agent);
        }
        else
        {
            // Add as normal agent marker
            _miniMap.AddAgent(agent);
        }

        spawnPlayers.Add(playerId, agent);
        EmitSignal(nameof(PlayerCreateSignal));
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

        if (SpawnBots.ContainsKey(id))
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

            if (SpawnBots.Count > bot_count)
            {
                while (SpawnBots.Count > bot_count)
                {
                    foreach (Agent spawnBot in SpawnBots.Values)
                    {
                        _removeUnitOnNetwork(spawnBot.Name);
                        break;
                    }
                }
            }
            else if (SpawnBots.Count < bot_count)
            {
                // If bot_count
                while (SpawnBots.Count < bot_count)
                {
                    TeamMapAI targetAI = null;
                    // Set the initial to max bot count
                    int smallestUnitCount = bot_count;

                    foreach (TeamMapAI currentAI in TeamMapAIs)
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
                        String botId = (int)targetAI.GetCurrentTeam() + ";" + AgentPrefix + AgentBotCounter;
                        AgentBotCounter++;

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

        if (!SpawnBots.ContainsKey(unitName))
        {
            bool enableAI = false;

            if (GetTree().IsNetworkServer())
            {
                enableAI = true;
            }

            Agent agent = TeamMapAIs[(int)team].CreateUnit(unitName, unitName, enableAI);
            SpawnBots.Add(unitName, agent);

            // Add agent marker to minimap
            _miniMap.AddAgent(agent);
        }
    }

    public override void _PhysicsProcess(float delta)
    {
        // Update the timeout counter
        CurrentTime += delta;
        if (CurrentTime < GameStates.updateDelta)
        {
            return;
        }

        // "Reset" the time counting
        CurrentTime -= GameStates.updateDelta;

        // And update the game state
        _updateState();
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








}
