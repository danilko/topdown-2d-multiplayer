using Godot;
using System;
using System.Collections.Generic;

public class GameStateManager : Node
{
    private GameWorld _gameWorld;
    private Network _network;
    private NetworkSnapshotManager _networkSnapshotManager;

    private GameStates _gamestates;
    private GameTimerManager _gameTimerManager;

    private float _currentTime;


    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _currentTime = 0;
    }

    public void Initialize(GameWorld gameWorld)
    {
        _gamestates = (GameStates)GetNode("/root/GAMESTATES");

        _gameWorld = gameWorld;
        _gameTimerManager = _gameWorld.GetGameTimerManager();
        _networkSnapshotManager = _gameWorld.GetNetworkSnasphotManager();
        _network = _networkSnapshotManager.GetNetwork();

        if (GetTree().NetworkPeer != null && GetTree().IsNetworkServer())
        {
            // Connect logic if server disconnect logic, which will perform end game
            _network.Connect(nameof(Network.DisconnectedSignal), this, nameof(_onGameExit));

            // Connect player remove logic, which remove
            _network.Connect(nameof(Network.PlayerRemovedSignal), this, nameof(_onPlayerRemoved));
        }
    }

    public GameStates GetGameStates()
    {
        return _gamestates;
    }

    private void _onGameExit()
    {
        _gamestates.restart();
    }

    private void _onPlayerRemoved(int playerId)
    {
        foreach (KeyValuePair<int, NetworkPlayer> item in _network.networkPlayers)
        {
            // Skip disconnecte player and server from replication code
            if (item.Key == playerId || item.Key == 1)
            {
                continue;
            }

            // push the player remove to all clients (include server)
            _gameWorld.GetAgentSpawnManager().PlaceRemoveUnit(AgentSpawnManager.AgentPrefix + playerId);
        }
    }

    // Update and generate a game state snapshot
    // This is only perform on server
    public void UpdateState(float phyisDelta)
    {
        // If not on the server, bail
        if (!GetTree().IsNetworkServer())
        {
            return;
        }

        if (_gameTimerManager.GetGameTimerState() == GameTimerManager.GameTimerState.INIT)
        {
            return;
        }

        // Update the timeout counter
        _currentTime += phyisDelta;
        if (_currentTime < _gamestates.updateDelta)
        {
            return;
        }

        // "Reset" the time counting
        _currentTime -= _gamestates.updateDelta;

        // Initialize the "high level" snapshot
        NetworkSnapshotManager.Snapshot snapshot = new NetworkSnapshotManager.Snapshot();
        snapshot.signature = _networkSnapshotManager.GetSnapshotSignature();

        List<String> removeSpawnPlayers = new List<String>();

        Boolean isGamingPeriod = _gameTimerManager.GetGameTimerState() == GameTimerManager.GameTimerState.GAMING;

        foreach (KeyValuePair<int, NetworkPlayer> networkPlayer in _network.networkPlayers)
        {
            String playerId = AgentSpawnManager.AgentPlayerPrefix + networkPlayer.Value.net_id;
            // Node may not being created yet
            if (!_gameWorld.GetAgentSpawnManager().GetSpawnPlayers().ContainsKey(playerId))
            {
                // Ideally should give a warning that a player node wasn't found
                continue;
            }

            Agent playerNode = _gameWorld.GetAgentSpawnManager().GetSpawnPlayers()[playerId];

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
                if (_networkSnapshotManager.playerInputs.ContainsKey(networkPlayer.Key) && _networkSnapshotManager.playerInputs[networkPlayer.Key].Count > 0)
                {

                    int rightWeapon = 0;
                    int leftWeapon = 0;

                    // Calculate the delta
                    float delta = _gamestates.updateDelta / (float)(_networkSnapshotManager.playerInputs[networkPlayer.Key].Count);

                    foreach (KeyValuePair<int, NetworkSnapshotManager.PlayerInput> input in _networkSnapshotManager.playerInputs[networkPlayer.Key])
                    {
                        Vector2 moveDir = Vector2.Zero;
                        moveDir.y -= input.Value.Up;
                        moveDir.y += input.Value.Down;
                        moveDir.x -= input.Value.Left;
                        moveDir.x += input.Value.Right;

                        playerNode.changeWeapon(input.Value.RightWeaponIndex, Weapon.WeaponOrder.Right);
                        playerNode.changeWeapon(input.Value.LeftWeaponIndex, Weapon.WeaponOrder.Left);

                        if (isGamingPeriod)
                        {
                            rightWeapon = input.Value.RightWeaponAction;
                            leftWeapon = input.Value.LeftWeaponAction;
                        }
                        playerNode.Fire(Weapon.WeaponOrder.Right, rightWeapon);
                        playerNode.Fire(Weapon.WeaponOrder.Left, leftWeapon);

                        playerNode.RotateToward(input.Value.MousePosition, delta);
                        playerNode.MoveToward(moveDir, delta);
                    }

                    // Cleanup the input vector
                    _networkSnapshotManager.playerInputs[networkPlayer.Key].Clear();

                    _networkSnapshotManager.playerInputs.Remove(networkPlayer.Key);

                    NetworkSnapshotManager.ClientData clientData = new NetworkSnapshotManager.ClientData();
                    clientData.Id = networkPlayer.Key + "";
                    clientData.Position = playerNode.Position;
                    clientData.Rotation = playerNode.Rotation;
                    clientData.RightWeapon = rightWeapon;
                    clientData.LeftWeapon = leftWeapon;
                    clientData.RightWeaponIndex = playerNode.GetCurrentWeaponIndex(Weapon.WeaponOrder.Right);
                    clientData.LeftWeaponIndex = playerNode.GetCurrentWeaponIndex(Weapon.WeaponOrder.Left);
                    clientData.Health = playerNode.getHealth();

                    snapshot.playerData.Add(networkPlayer.Key + "", clientData);
                }
            }
            else
            {
                removeSpawnPlayers.Insert(0, playerId);
            }
        }

        // Clean the input
        _networkSnapshotManager.playerInputs.Clear();

        foreach (String spawnPlayerId in removeSpawnPlayers)
        {
            // Respawn dead player if that team still allow new unit
            Team.TeamCode teamCode = _gameWorld.GetAgentSpawnManager().GetSpawnPlayers()[spawnPlayerId].GetTeam();
            String displayName = _gameWorld.GetAgentSpawnManager().GetSpawnPlayers()[spawnPlayerId].GetDisplayName();
            _gameWorld.GetAgentSpawnManager().PlaceRemoveUnit(spawnPlayerId);
        }

        List<String> removeSpawnBots = new List<String>();

        foreach (Agent agent in _gameWorld.GetAgentSpawnManager().GetSpawnBots().Values)
        {
            // Locate the bot node
            Agent enemyNode = (Agent)_gameWorld.GetTeamMapAIManager().GetTeamMapAIs()[(int)agent.GetTeam()].GetUnit(agent.Name);

            if (enemyNode == null || !IsInstanceValid(enemyNode))
            {
                // Ideally should give a warning that a bot node wasn't found
                continue;
            }


            int rightWeapon = (int)NetworkSnapshotManager.PlayerInput.InputAction.NOT_TRIGGER;
            int leftWeapon = (int)NetworkSnapshotManager.PlayerInput.InputAction.NOT_TRIGGER;

            if (isGamingPeriod)
            {
                rightWeapon = enemyNode.RightWeaponAction;
                leftWeapon = enemyNode.LeftWeaponAction;
            }

            enemyNode.Fire(Weapon.WeaponOrder.Right, rightWeapon);
            enemyNode.Fire(Weapon.WeaponOrder.Left, leftWeapon);

            if (enemyNode.getHealth() > 0)
            {
                // Build bot_data entry
                NetworkSnapshotManager.ClientData clientData = new NetworkSnapshotManager.ClientData();
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
                if (rightWeapon == (int)NetworkSnapshotManager.PlayerInput.InputAction.RELOAD)
                {
                    enemyNode.RightWeaponAction = (int)NetworkSnapshotManager.PlayerInput.InputAction.NOT_TRIGGER;
                }
                if (leftWeapon == (int)NetworkSnapshotManager.PlayerInput.InputAction.RELOAD)
                {
                    enemyNode.LeftWeaponAction = (int)NetworkSnapshotManager.PlayerInput.InputAction.NOT_TRIGGER;
                }
            }
            else
            {
                removeSpawnBots.Insert(0, enemyNode.Name);
            }
        }

        foreach (String spawnBotId in removeSpawnBots)
        {
            _gameWorld.GetAgentSpawnManager().PlaceRemoveUnit(spawnBotId);
        }

        // Encode and broadcast the snapshot - if there is at least one connected client
        if (_network.networkPlayers.Count > 1)
        {
            _networkSnapshotManager.EncodeSnapshot(snapshot);
        }
    }

    public void UpdateAgentStateFromSnapshot(String agentNodeName, NetworkSnapshotManager.ClientData item)
    {
        // Depending on the synchronization mechanism, this may not be an error!
        // For now assume the entities are spawned and kept in sync so just continue
        // the loop

        Agent agent = null;
        if (_gameWorld.GetAgentSpawnManager().GetSpawnPlayers().ContainsKey(agentNodeName))
        {
            agent = _gameWorld.GetAgentSpawnManager().GetSpawnPlayers()[agentNodeName];
        }

        if (_gameWorld.GetAgentSpawnManager().GetSpawnBots().ContainsKey(agentNodeName))
        {
            agent = _gameWorld.GetAgentSpawnManager().GetSpawnBots()[agentNodeName];
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
}
