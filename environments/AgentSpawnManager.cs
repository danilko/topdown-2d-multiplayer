using Godot;
using System;
using System.Collections.Generic;


public class AgentSpawnInfo : Godot.Object
{
    public Team.TeamCode Team;
    public String UnitName;
    public String UnitId;
    public String DisplayName;
    public int Delay;
}

public class AgentSpawnManager : Node
{

    [Signal]
    public delegate void PlayerDefeatedSignal();

    [Signal]
    public delegate void PlayerCreatedSignal();

    [Signal]
    public delegate void AgentDefeatedSignal();

    [Signal]
    public delegate void AgentCreatedSignal();

    [Signal]
    public delegate void AgentStartToCreateSignal();

    private List<AgentSpawnInfo> _spawnList;

    private GameWorld _gameWorld;
    private GameStates _gameStates;

    private Dictionary<String, Agent> _spawnBots;
    private Dictionary<String, Agent> _spawnPlayers;

    public static String AgentPlayerPrefix = "agent_player_";

    public static String AgentBotPrefix = "agent_bot_";

    public static String AgentObserverPrefix = "agent_observer_";

    private Network _network;
    private Observer _observer;
    private Timer _counterTimer;

    public static int INIT_DELAY = 1;
    public static int DEFAULT_DELAY = 20;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _spawnList = new List<AgentSpawnInfo>();

        _spawnBots = new Dictionary<String, Agent>();
        _spawnPlayers = new Dictionary<String, Agent>();

        _counterTimer = (Timer)GetNode("CounterTimer");

        if (GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
        {
            _counterTimer.Connect("timeout", this, nameof(_checkAgentSpawnQueue));
            Connect(nameof(AgentStartToCreateSignal), this, nameof(_spawnAgentServer));
            Connect(nameof(AgentDefeatedSignal), this, nameof(_placeDefeatedUnitIntoCreationQueue));

            _counterTimer.Start();
        }
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

    public void PlaceNewUnit(String unitId, Team.TeamCode team, String unitName, String displayName, int delay)
    {
        AgentSpawnInfo agentSpawnInfo = new AgentSpawnInfo();
        agentSpawnInfo.UnitId = unitId;
        agentSpawnInfo.Team = team;
        agentSpawnInfo.UnitName = unitName;
        agentSpawnInfo.Delay = delay;

        _spawnList.Add(agentSpawnInfo);
    }

    private void _placeDefeatedUnitIntoCreationQueue(String unitId, Team.TeamCode team, String unitName, String displayName)
    {
        // Check if this player need to be respawn
        if (!_gameWorld.GetGameConditionManager().CheckIfPlayerNeedToBeRespawn(unitId))
        {
            return;
        }

        PlaceNewUnit(unitId, team, unitName, displayName, DEFAULT_DELAY);
    }

    public void PlaceRemoveUnit(String id)
    {
        if (GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
        {
            // Call locally for server
            _placeRemoveUnit(id);

            if (GetTree().NetworkPeer != null)
            {
                Rpc(nameof(_placeRemoveUnit), id);
            }
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

            EmitSignal(nameof(AgentDefeatedSignal), id, currentSpawnCache[id].GetTeam(), currentSpawnCache[id].GetUnitName(), currentSpawnCache[id].GetDisplayName());

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

    private void _checkAgentSpawnQueue()
    {
        foreach (AgentSpawnInfo agentSpawnInfo in _spawnList)
        {
            agentSpawnInfo.Delay--;
        }

        while (_spawnList.Count > 0)
        {
            // If no need to wait, create it, and clean from queue
            if (_spawnList[0].Delay <= 0)
            {
                EmitSignal(nameof(AgentStartToCreateSignal), _spawnList[0]);
                _spawnList.RemoveAt(0);
            }
            else
            {
                // No longer have new agent to spawn at this moment
                break;
            }
        }

        // Init next trigger
        _counterTimer.Start();
    }

    private void _spawnAgentServer(AgentSpawnInfo unitSpawnInfo)
    {
        // If condition cannot spawn, re - added back to queue and 
        // TODO: Need to better control whatever it is base/amount, if amount, will block the team if from adding to queue
        if (!_gameWorld.GetGameConditionManager().CheckIfCanSpawn(unitSpawnInfo.Team))
        {
            unitSpawnInfo.Delay = DEFAULT_DELAY;
            PlaceNewUnit(unitSpawnInfo.UnitId, unitSpawnInfo.Team, unitSpawnInfo.UnitName, unitSpawnInfo.DisplayName, DEFAULT_DELAY);
            return;
        }

        String agentInfo = unitSpawnInfo.UnitId + ";" + (int)unitSpawnInfo.Team + ";" + unitSpawnInfo.UnitName + ";" + unitSpawnInfo.DisplayName;

        if (GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
        {

            // Call locally for server
            _spawnAgentClient(agentInfo);

            _gameWorld.GetTeamMapAIManager().GetTeamMapAIs()[(int)unitSpawnInfo.Team].ChargeUnitAmount();

            if (GetTree().NetworkPeer != null)
            {
                Rpc(nameof(_spawnAgentClient), agentInfo);
            }


            // Equip weapons, on server side only and sync to client, note
            if (unitSpawnInfo.UnitId.Contains(AgentPlayerPrefix))
            {
                _gameWorld.GetTeamMapAIManager().GetTeamMapAIs()[(int)unitSpawnInfo.Team].AssignDefaultWeapon(_spawnPlayers[unitSpawnInfo.UnitId]);
            }
            else
            {
                _gameWorld.GetTeamMapAIManager().GetTeamMapAIs()[(int)unitSpawnInfo.Team].AssignDefaultAIRandomCombine(_spawnBots[unitSpawnInfo.UnitId]);
            }
        }
    }

    [Remote]
    private void _spawnAgentClient(String agentInfo)
    {
        String unitId = agentInfo.Split(";")[0];
        Team.TeamCode unitTeam = (Team.TeamCode)(int.Parse(agentInfo.Split(";")[1]));
        String unitName = agentInfo.Split(";")[2];
        String unitDisplayName = agentInfo.Split(";")[3];

        if (unitId.Contains(AgentPlayerPrefix))
        {
            _spawnPlayer(unitId, unitTeam, unitName, unitDisplayName);
        }
        else
        {
            _spawnBot(unitId, unitTeam, unitName, unitDisplayName);
        }
    }

    private void _spawnPlayer(String playerId, Team.TeamCode team, String unitName, String displayName)
    {
        // Already generated
        if (_spawnPlayers.ContainsKey(playerId))
        {
            return;
        }

        int netId = int.Parse(playerId.Replace(AgentPlayerPrefix, ""));

        // Load the scene and create an instance
        Player agent = (Player)(_gameWorld.GetTeamMapAIManager().GetTeamMapAIs()[(int)team].CreateUnit(unitName, displayName, false));

        // If this actor is the current client controlled, add camera and attach HUD
        if (netId == _network.gamestateNetworkPlayer.net_id)
        {
            // Attach camera
            agent.SetHUD(_gameWorld.GetHUD(), _gameWorld.getInventoryManager());
            agent.SetCameraRemotePath(_gameWorld.GetGameCamera());
        }

        // Set the authority to the net player who owns it
        // This one is important so a player cannot control unit does not belong to player
        agent.SetNetworkMaster(netId);

        _spawnPlayers.Add(playerId, agent);

        EmitSignal(nameof(PlayerCreatedSignal));
        EmitSignal(nameof(AgentCreatedSignal), unitName, team);
    }

    private void _spawnBot(String botId, Team.TeamCode team, String unitName, String displayName)
    {
        // Already generated
        if (_spawnBots.ContainsKey(unitName))
        {
            return;
        }

        bool enableAI = false;

        if (GetTree().IsNetworkServer() || GetTree().NetworkPeer == null)
        {
            enableAI = true;
        }

        Agent agent = _gameWorld.GetTeamMapAIManager().GetTeamMapAIs()[(int)team].CreateUnit(unitName, unitName, enableAI);
        _spawnBots.Add(unitName, agent);

        EmitSignal(nameof(AgentCreatedSignal), unitName, team);

    }

}
