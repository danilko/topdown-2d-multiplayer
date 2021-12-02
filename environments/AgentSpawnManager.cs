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

    [Signal]
    public delegate void AgentCreatedSignal();

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

            EmitSignal(nameof(AgentDefeatedSignal), currentSpawnCache[id].GetUnitName(), currentSpawnCache[id].GetTeam());

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
    private void _spawnPlayerOnServer(String unitInfo)
    {
        int netId = int.Parse(unitInfo.Split(";")[0]);
        int team = int.Parse(unitInfo.Split(";")[1]);
        String unitName = unitInfo.Split(";")[2];
        String displayName = unitInfo.Split(";")[3];

        if (GetTree().IsNetworkServer())
        {
            Rpc(nameof(_spawnPlayer), unitInfo);

        }
    }

    private void _spawnPlayer(int netId, Team.TeamCode team, String unitName, String displayName)
    {
        String playerId = AgentPlayerPrefix + netId;

        // Already generated
        if (_spawnPlayers.ContainsKey(playerId))
        {
            return;
        }

        // Load the scene and create an instance
        Player agent = (Player)(_gameWorld.GetTeamMapAIManager().GetTeamMapAIs()[(int)team].CreateUnit(unitName, displayName, false));

        // If this actor does not belong to the server, change the network master accordingly
        if (netId != 1)
        {
            agent.SetNetworkMaster(netId);
        }

        // If this actor is the current client controlled, add camera and attach HUD
        if (netId == _network.gamestateNetworkPlayer.net_id)
        {
            // Attach camera
            agent.SetHUD(_gameWorld.GetHUD(), _gameWorld.getInventoryManager());
            agent.SetCameraRemotePath(_gameWorld.GetGameCamera());
        }

        _spawnPlayers.Add(playerId, agent);
        EmitSignal(nameof(PlayerCreateSignal));
        EmitSignal(nameof(AgentCreatedSignal), agent.GetUnitName(), agent.GetTeam());
    }


    [Remote]
    private void _addBoT(String botId)
    {
        Team.TeamCode team = (Team.TeamCode)int.Parse(botId.Split(";")[0]);
        String unitName = botId.Split(";")[1];

        if (!_spawnBots.ContainsKey(unitName))
        {
            bool enableAI = false;

            if (GetTree().IsNetworkServer())
            {
                enableAI = true;
            }

            Agent agent = _gameWorld.GetTeamMapAIManager().GetTeamMapAIs()[(int)team].CreateUnit(unitName, unitName, enableAI);
            _spawnBots.Add(unitName, agent);
            EmitSignal(nameof(AgentCreatedSignal), agent.GetUnitName(), agent.GetTeam());
        }
    }

}
