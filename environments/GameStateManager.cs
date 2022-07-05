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

        if (GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
        {
            // Connect player remove logic, which remove
            _network.Connect(nameof(Network.PlayerRemovedSignal), this, nameof(_onPlayerRemoved));
        }

        // Connect logic if server disconnect logic, which will perform end game
        _network.Connect(nameof(Network.DisconnectedSignal), this, nameof(_onGameExit));
    }

    public GameStates GetGameStates()
    {
        return _gamestates;
    }

    private void _onGameExit()
    {
        _gamestates.EnterTitleScreen();
    }

    private void _onPlayerRemoved(int playerId)
    {
        // push the player remove to all clients (include server)
        _gameWorld.GetAgentSpawnManager().PlaceRemoveUnit(AgentSpawnManager.AgentPlayerPrefix + playerId);
    }

    // Update and generate a game state snapshot
    // This is only perform on server
    public void UpdateState(float phyisDelta)
    {
        // If not on the server, bail
        if (GetTree().NetworkPeer != null && !GetTree().IsNetworkServer())
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
            if (playerNode.GetHealth() > 0)
            {
                // Check if there is any input for this player. In that case, update the state
                if (_networkSnapshotManager.playerInputs.ContainsKey(networkPlayer.Key) && _networkSnapshotManager.playerInputs[networkPlayer.Key].Count > 0)
                {


                    int rightWeaponAction = (int)NetworkSnapshotManager.PlayerInput.InputAction.NOT_TRIGGER;
                    int leftWeaponAction = (int)NetworkSnapshotManager.PlayerInput.InputAction.NOT_TRIGGER;
                    int remoteWeaponAction = (int)NetworkSnapshotManager.PlayerInput.InputAction.NOT_TRIGGER;

                    // Calculate the delta
                    float delta = _gamestates.updateDelta / (float)(_networkSnapshotManager.playerInputs[networkPlayer.Key].Count);

                    foreach (KeyValuePair<int, NetworkSnapshotManager.PlayerInput> input in _networkSnapshotManager.playerInputs[networkPlayer.Key])
                    {
                        Vector2 moveDir = Vector2.Zero;
                        moveDir.y -= input.Value.Up;
                        moveDir.y += input.Value.Down;
                        moveDir.x -= input.Value.Left;
                        moveDir.x += input.Value.Right;

                        playerNode.ChangeWeapon(input.Value.RightWeaponIndex, Weapon.WeaponOrder.Right);
                        playerNode.ChangeWeapon(input.Value.LeftWeaponIndex, Weapon.WeaponOrder.Left);

                        if (isGamingPeriod)
                        {
                            rightWeaponAction = input.Value.RightWeaponAction;
                            leftWeaponAction = input.Value.LeftWeaponAction;
                            remoteWeaponAction = input.Value.RemoteWeaponAction;
                        }

                        playerNode.RotateToward(input.Value.MousePosition, delta);
                        playerNode.MoveToward(moveDir, delta);

                        playerNode.Fire(Weapon.WeaponOrder.Right, rightWeaponAction);
                        playerNode.Fire(Weapon.WeaponOrder.Left, leftWeaponAction);
                        playerNode.FireRemoteWeapon(remoteWeaponAction);

                        if(input.Value.TargetSelectionAction == (int)NetworkSnapshotManager.PlayerInput.TargetAction.TRIGGER)
                        {
                            playerNode.TriggerTargetAgentSelection();
                        }
                        else if(input.Value.TargetSelectionAction == (int)NetworkSnapshotManager.PlayerInput.TargetAction.PREVIOUS)
                        {
                            playerNode.GetPreviousTargeAgent();
                        }
                        else if(input.Value.TargetSelectionAction == (int)NetworkSnapshotManager.PlayerInput.TargetAction.PREVIOUS)
                        {
                            playerNode.GetNextTargeAgent();
                        }
                    }

                    // Cleanup the input vector
                    _networkSnapshotManager.playerInputs[networkPlayer.Key].Clear();

                    _networkSnapshotManager.playerInputs.Remove(networkPlayer.Key);

                    NetworkSnapshotManager.ClientData clientData = new NetworkSnapshotManager.ClientData();
                    clientData.Id = networkPlayer.Key + "";
                    clientData.Position = playerNode.GlobalPosition;
                    clientData.Rotation = playerNode.GlobalRotation;
                    clientData.Health = playerNode.GetHealth();
                    clientData.Energy = playerNode.GetEnergy();
                    clientData.RightWeaponAction = rightWeaponAction;
                    clientData.LeftWeaponAction = leftWeaponAction;
                    clientData.RemoteWeaponAction = remoteWeaponAction;
                    clientData.RightWeaponIndex = playerNode.GetCurrentWeaponIndex(Weapon.WeaponOrder.Right);
                    clientData.LeftWeaponIndex = playerNode.GetCurrentWeaponIndex(Weapon.WeaponOrder.Left);

                    clientData.TargetAgentUnitID = playerNode.GetTargetAgent() != null ? playerNode.GetTargetAgent().GetUnitID() : "";

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
            Agent agentNode = (Agent)_gameWorld.GetTeamMapAIManager().GetTeamMapAIs()[(int)agent.GetTeam()].GetUnit(agent.Name);

            if (agentNode == null || !IsInstanceValid(agentNode))
            {
                // Ideally should give a warning that a bot node wasn't found
                continue;
            }


            int rightWeaponAction = (int)NetworkSnapshotManager.PlayerInput.InputAction.NOT_TRIGGER;
            int leftWeaponAction = (int)NetworkSnapshotManager.PlayerInput.InputAction.NOT_TRIGGER;
            int remoteWeaponAction = (int)NetworkSnapshotManager.PlayerInput.InputAction.NOT_TRIGGER;

            if (isGamingPeriod)
            {
                rightWeaponAction = agentNode.RightWeaponAction;
                leftWeaponAction = agentNode.LeftWeaponAction;
                remoteWeaponAction = agentNode.RemoteWeaponAction;
            }

            agentNode.Fire(Weapon.WeaponOrder.Right, rightWeaponAction);
            agentNode.Fire(Weapon.WeaponOrder.Left, leftWeaponAction);
            agentNode.FireRemoteWeapon(remoteWeaponAction);

            if (agentNode.GetHealth() > 0)
            {
                // Build bot_data entry
                NetworkSnapshotManager.ClientData clientData = new NetworkSnapshotManager.ClientData();
                clientData.Id = agentNode.Name;
                clientData.Position = agentNode.GlobalPosition;
                clientData.Rotation = agentNode.GlobalRotation;
                clientData.Health = agentNode.GetHealth();
                clientData.Energy = agentNode.GetEnergy();
                clientData.RightWeaponAction = rightWeaponAction;
                clientData.RightWeaponAction = leftWeaponAction;
                clientData.RemoteWeaponAction = remoteWeaponAction;
                clientData.RightWeaponIndex = agentNode.GetCurrentWeaponIndex(Weapon.WeaponOrder.Right);
                clientData.LeftWeaponIndex = agentNode.GetCurrentWeaponIndex(Weapon.WeaponOrder.Left);
                clientData.TargetAgentUnitID = agentNode.GetTargetAgent() != null ? agentNode.GetTargetAgent().GetUnitID() : "";

                // Append into the snapshot
                snapshot.botData.Add(agentNode.Name, clientData);

                // This logic is necessary to notify the AI that reload is pick up, so can continue with next state
                if (rightWeaponAction == (int)NetworkSnapshotManager.PlayerInput.InputAction.RELOAD)
                {
                    agentNode.RightWeaponAction = (int)NetworkSnapshotManager.PlayerInput.InputAction.NOT_TRIGGER;
                }
                if (leftWeaponAction == (int)NetworkSnapshotManager.PlayerInput.InputAction.RELOAD)
                {
                    agentNode.LeftWeaponAction = (int)NetworkSnapshotManager.PlayerInput.InputAction.NOT_TRIGGER;
                }
                if (remoteWeaponAction == (int)NetworkSnapshotManager.PlayerInput.InputAction.RELOAD)
                {
                    agentNode.RemoteWeaponAction = (int)NetworkSnapshotManager.PlayerInput.InputAction.NOT_TRIGGER;
                }
            }
            else
            {
                // Simulation will not remove bot, but rather just set its max health
                if (_gameWorld.GetGameStateManager().GetGameStates().GetGameType() == GameStates.GameType.SIMULATION)
                {
                    agentNode.SetHealth(agentNode.MaxHealth);
                }
                else
                {

                    removeSpawnBots.Insert(0, agentNode.Name);
                }

            }
        }

        foreach (String spawnBotId in removeSpawnBots)
        {
            _gameWorld.GetAgentSpawnManager().PlaceRemoveUnit(spawnBotId);
        }

        // Sync Remote Weapon Manager before sync the snapshot
        _gameWorld.GetRemoteWeaponManager().SyncState();

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

        agent.Sync(item);
    }
}
