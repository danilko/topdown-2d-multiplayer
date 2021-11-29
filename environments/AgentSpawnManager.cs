using Godot;
using System;
using System.Collections.Generic;


public class AgentSpawnInfo
{
    public Team Team;
    public String Name;
    public int Delay;
}

public class AgentSpawnManager : Node
{

    [Signal]
    public delegate void PlayerDefeatedSignal();

    [Signal]
    public delegate void PlayerCreateSignal();

    [Signal]
    public delegate void AgentDefeatedSignal();

    private Queue<AgentSpawnInfo> spawnQueue;

    private GameWorld _gameWorld;
    private GameStates _gameStates;
    
    private class SpwanInfo
    {
        public long spawn_index { get; set; }
        public NetworkPlayer networkPlayer { get; set; }
    }

    private Dictionary<String, Agent> _spawnBots;
    private Dictionary<String, Agent> _spawnPlayers;

    public static String AgentPrefix = "agent_";

    public static String AgentPlayerPrefix = "agent_player_";

    public static String AgentObserverPrefix = "agent_observer_";
    
    private Network _network;


    private Observer _observer;


    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        spawnQueue = new Queue<AgentSpawnInfo>();

        _spawnBots = new Dictionary<String, Agent>();
        _spawnPlayers = new Dictionary<String, Agent>();
    }

    public Dictionary<String, Agent> GetSpawnBots()
    {
        return _spawnBots;
    }
    
    public Dictionary<String, Agent> GetSpawnPlayers()
    {
        return _spawnPlayers;
    }

    public void Initialize(GameWorld gameWorld)
    {
        _gameWorld = gameWorld;
        _gameStates = _gameWorld.GetGameStateManager().GetGameStates();
        _network = _gameWorld.GetNetworkSnasphotManager().GetNetwork();

        _observer = (Observer)_gameWorld.GetNode("Observer");
    }

    public void placeAgentOnWaitingQueue(String Agent, Agent agent)
    {

    }

    public void PlaceRemoveUnit(String id)
    {
        if (GetTree().NetworkPeer != null && GetTree().IsNetworkServer())
        {
            Rpc(nameof(_placeRemoveUnit), id);
        }
        else
        {
            // If just local, then called the local method directly
            _placeRemoveUnit(id);
        }
    }

    [Remote]
    private void _placeRemoveUnit(String id)
    {
        Vector2 existingPosition = Vector2.Zero;

        Dictionary<String, Agent> currentSpawnCache = null;

        // Assign to different cache base on team
        if (id.Contains(AgentPlayerPrefix))
        {
            currentSpawnCache = _spawnPlayers;
        }
        else
        {
            currentSpawnCache = _spawnBots;
        }

        // Need to check if keys are there, it may not be a bug
        if (currentSpawnCache.ContainsKey(id) && _gameWorld.GetTeamMapAIManager().GetTeamMapAIs()[(int)currentSpawnCache[id].GetTeam()].GetUnit(currentSpawnCache[id].Name) != null)
        {
            existingPosition = currentSpawnCache[id].GlobalPosition;

            EmitSignal(nameof(AgentDefeatedSignal),  currentSpawnCache[id].GetUnitName(), currentSpawnCache[id].GetTeam());

            _gameWorld.GetTeamMapAIManager().GetTeamMapAIs()[(int)currentSpawnCache[id].GetTeam()].RemoveUnit(currentSpawnCache[id].Name);
        }

        if (currentSpawnCache.ContainsKey(id))
        {
            currentSpawnCache.Remove(id);
        }

        // If this is the player attach to current client, respawn the client with observer
        if (currentSpawnCache == _spawnPlayers && id == AgentPlayerPrefix + _network.gamestateNetworkPlayer.net_id)
        {
            updateObserver(existingPosition);
            EmitSignal(nameof(PlayerDefeatedSignal));
        }

        // Shake camera
        if (Mathf.Abs(existingPosition.x - _gameWorld.GetGameCamera().GlobalPosition.y) < GetViewport().Size.x &&
         Mathf.Abs(existingPosition.y - _gameWorld.GetGameCamera().GlobalPosition.y) < GetViewport().Size.y)
        {
            _gameWorld.GetGameCamera().StartScreenShake();
        }
    }

    private void updateObserver(Vector2 position)
    {
        _observer.PauseMode = PauseModeEnum.Inherit;
        _observer.Position = position;
        _observer.SetCameraRemotePath(_gameWorld.GetGameCamera());
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
                        if (currentAI.GetAutoSpawnMember() && currentAI.isNewUnitAllow())
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
                        String botId = (int)targetAI.GetTeam() + ";" + AgentPrefix + AgentBotCounter;
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

}
