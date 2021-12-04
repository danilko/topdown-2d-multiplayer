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
    public delegate void UnitStartToCreateSignal();

    private List<AgentSpawnInfo> _spawnList;

    private GameWorld _gameWorld;
    private GameStates _gameStates;


    private Dictionary<String, Agent> _spawnBots;
    private Dictionary<String, Agent> _spawnPlayers;

    public static String AgentPrefix = "agent_";

    public static String AgentPlayerPrefix = "agent_player_";

    public static String AgentObserverPrefix = "agent_observer_";

    private Network _network;
    private Observer _observer;
    private Timer _counterTimer;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _spawnList = new List<AgentSpawnInfo>();

        _spawnBots = new Dictionary<String, Agent>();
        _spawnPlayers = new Dictionary<String, Agent>();

        _counterTimer = (Timer)GetNode("CounterTimer");

        if (GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
        {
            _counterTimer.Connect("timeout", this, nameof(_checkUnitSpawnQueue));
            Connect(nameof(UnitStartToCreateSignal), this, nameof(_spawnUnitServer));
            _counterTimer.Start();
        }
    }

    public void InitUnits()
    {
        if (GetTree().IsNetworkServer())
        {
            foreach (KeyValuePair<int, NetworkPlayer> item in _network.networkPlayers)
            {
                String unitId = AgentPlayerPrefix + item.Value.net_id;
                Team.TeamCode team = (Team.TeamCode)item.Value.team;
                String unitName = unitId;
                String displayName = item.Value.name;

                PlaceNewUnit(unitId, team, unitName, displayName, 1);
            }
        }
        else if (GetTree().NetworkPeer == null)
        {
            String unitId = AgentPlayerPrefix + 0;
            Team.TeamCode team = (Team.TeamCode)0;
            String unitName = unitId;
            String displayName = "Player";

            PlaceNewUnit(unitId, team, unitName, displayName, 1);
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

    public void PlaceRemoveUnit(String id)
    {
        if (GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
        {
            if (GetTree().NetworkPeer != null)
            {
                Rpc(nameof(_placeRemoveUnit), id);
            }

            // Call locally for server
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

    private void _checkUnitSpawnQueue()
    {
        foreach (AgentSpawnInfo unitSpawnInfo in _spawnList)
        {
            unitSpawnInfo.Delay--;
        }

        while (_spawnList.Count > 0)
        {
            // If no need to wait, create it, and clean from queue
            if (_spawnList[0].Delay <= 0)
            {
                EmitSignal(nameof(UnitStartToCreateSignal), _spawnList[0]);
                _spawnList.RemoveAt(0);
            }
        }

        // Init next trigger
        _counterTimer.Start();
    }

    private void _spawnUnitServer(AgentSpawnInfo unitSpawnInfo)
    {
        String unitInfo = unitSpawnInfo.UnitId + ";" + (int)unitSpawnInfo.Team + ";" + unitSpawnInfo.UnitName + ";" + unitSpawnInfo.DisplayName;

        if (GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
        {
            if (GetTree().NetworkPeer != null)
            {
                Rpc(nameof(_spawnUnitClient), unitInfo);
            }

            // Call locally for server
            _spawnUnitClient(unitInfo);

        }

    }

    [Remote]
    private void _spawnUnitClient(String unitInfo)
    {
        String unitId = unitInfo.Split(";")[0];
        Team.TeamCode unitTeam = (Team.TeamCode)(int.Parse(unitInfo.Split(";")[1]));
        String unitName = unitInfo.Split(";")[2];
        String unitDisplayName = unitInfo.Split(";")[3];

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

        // Load the scene and create an instance
        Player agent = (Player)(_gameWorld.GetTeamMapAIManager().GetTeamMapAIs()[(int)team].CreateUnit(unitName, displayName, false));

        // If this actor is the current client controlled, add camera and attach HUD
        if (int.Parse(playerId.Replace(AgentPlayerPrefix, "")) == _network.gamestateNetworkPlayer.net_id)
        {
            // Attach camera
            agent.SetHUD(_gameWorld.GetHUD(), _gameWorld.getInventoryManager());
            agent.SetCameraRemotePath(_gameWorld.GetGameCamera());
        }

        _spawnPlayers.Add(playerId, agent);

        EmitSignal(nameof(PlayerCreatedSignal));
        EmitSignal(nameof(AgentCreatedSignal), unitName, team);
    }


    [Remote]
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
