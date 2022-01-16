using Godot;
using System;
using System.Collections.Generic;


public class AgentSpawnInfo : Godot.Object
{
    public Team.TeamCode Team { get; set; }
    public String UnitId { get; set; }
    public String DisplayName { get; set; }
    public int Delay { get; set; }
    public int CaptureBaseIndex { get; set; }
    public int WeaponSetupIndex { get; set; }

    public AgentSpawnInfo()
    {
        CaptureBaseIndex = -1;
        WeaponSetupIndex = 0;
    }
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

    [Signal]
    public delegate void AgentConfigSignal();

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

    public static int UNIT_CONFIG_TIME = 12;

    public static int ALLOW_UNIT_CONFIG_TIME = 10;

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

    public void PlaceNewUnit(String unitId, Team.TeamCode team, String displayName, int delay)
    {
        AgentSpawnInfo agentSpawnInfo = new AgentSpawnInfo();
        agentSpawnInfo.UnitId = unitId;
        agentSpawnInfo.DisplayName = displayName;
        agentSpawnInfo.Team = team;
        agentSpawnInfo.Delay = delay;

        _spawnList.Add(agentSpawnInfo);
    }

    private void _placeDefeatedUnitIntoCreationQueue(String unitId, Team.TeamCode team, String displayName)
    {
        // Check if this player need to be respawn
        if (!_gameWorld.GetGameConditionManager().CheckIfPlayerNeedToBeRespawn(unitId))
        {
            return;
        }

        PlaceNewUnit(unitId, team, displayName, DEFAULT_DELAY);
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

    public void UpdateNewUnitConfig(String unitId, int captureBase, int weaponSetup)
    {
        if (GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
        {
            _updateNewUnitConfig(unitId, captureBase, weaponSetup);
        }
        else
        {
            RpcId(1, nameof(_updateNewUnitConfigServer), captureBase, weaponSetup);
        }
    }

    [Remote]
    private void _updateNewUnitConfigServer(int captureBaseIndex, int weaponSetupIndex)
    {
        String unitId = AgentPlayerPrefix + GetTree().GetRpcSenderId();

        _updateNewUnitConfig(unitId, captureBaseIndex, weaponSetupIndex);
    }

    private void _updateNewUnitConfig(String unitId, int captureBase, int weaponSetupIndex)
    {
        foreach (AgentSpawnInfo agentSpawnInfo in _spawnList)
        {
            // Confirm the unit is in spawn list 
            if (agentSpawnInfo.UnitId == unitId)
            {
                weaponSetupIndex = weaponSetupIndex % _gameWorld.GetTeamMapAIManager().GetTeamMapAIs()[(int)agentSpawnInfo.Team].GetMaxWeaponSetupCount();

                // Ensure it is positive number
                if (weaponSetupIndex < 0)
                {
                    weaponSetupIndex *= -1;
                }

                agentSpawnInfo.WeaponSetupIndex = weaponSetupIndex;

                // Ensure it is one of selectable base
                captureBase = captureBase % _gameWorld.GetCapturableBaseManager().GetCapturableBases().Count;

                // Ensure it is positive number
                if (captureBase < 0)
                {
                    captureBase *= -1;
                }

                // Confirm if
                // It is still in allowance time to change
                // The target team is same team as the base
                // Reason to not allow 0 or 1 count down setting to have conflict during creation and map setting
                if (agentSpawnInfo.Delay >= (UNIT_CONFIG_TIME - ALLOW_UNIT_CONFIG_TIME) &&
                _gameWorld.GetCapturableBaseManager().GetCapturableBases()[captureBase].GetCaptureBaseTeam() == agentSpawnInfo.Team)
                {
                    // Update the index
                    agentSpawnInfo.CaptureBaseIndex = captureBase;
                }

                break;
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

            EmitSignal(nameof(AgentDefeatedSignal), currentSpawnCache[id].GetUnitID(), currentSpawnCache[id].GetTeam(), currentSpawnCache[id].GetDisplayName());

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

    private void _notifyAgentConfigServer(String unitID, Team.TeamCode team)
    {
        // No need to check for server as this logic will only run on server

        String message = unitID + ";" + (int)team;

        // Only need to send RPC if:
        // There are more peers to connect to
        // and
        // The unit is player
        if (GetTree().NetworkPeer != null && unitID.Contains(AgentPlayerPrefix))
        {
            // Notify the target client to show the UI
            int client = int.Parse(unitID.Replace(AgentPlayerPrefix, ""));

            // If it is not server, notify that client to notify user to select base
            if (client != 1)
            {
                RpcId(client, nameof(_notifyAgentConfigClient), message);
                return;
            }
        }

        _notifyAgentConfigClient(message);
    }

    [Remote]
    private void _notifyAgentConfigClient(String message)
    {
        int index = 0;
        String unitID = message.Split(";")[index];
        index++;
        Team.TeamCode team = (Team.TeamCode)(int.Parse(message.Split(";")[index]));

        EmitSignal(nameof(AgentConfigSignal), unitID, team);
    }

    private void _checkAgentSpawnQueue()
    {
        foreach (AgentSpawnInfo agentSpawnInfo in _spawnList)
        {
            // If this is the time allow user config, notify unit's owner
            if (agentSpawnInfo.Delay == UNIT_CONFIG_TIME)
            {
                _notifyAgentConfigServer(agentSpawnInfo.UnitId, agentSpawnInfo.Team);
            }

            // Count down the time
            agentSpawnInfo.Delay--;
        }

        // Sort by delay
        _spawnList.Sort((a, b) => a.Delay.CompareTo(b.Delay));

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
            PlaceNewUnit(unitSpawnInfo.UnitId, unitSpawnInfo.Team, unitSpawnInfo.DisplayName, DEFAULT_DELAY);
            return;
        }

        String agentInfo = unitSpawnInfo.UnitId + ";" + (int)unitSpawnInfo.Team + ";" + unitSpawnInfo.DisplayName + ";" + unitSpawnInfo.CaptureBaseIndex + ";";

        if (GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
        {

            // Call locally for server
            _spawnAgentClient(agentInfo);

            // Charge unit amount
            _gameWorld.GetTeamMapAIManager().GetTeamMapAIs()[(int)unitSpawnInfo.Team].ChargeUnitAmount();

            if (GetTree().NetworkPeer != null)
            {
                Rpc(nameof(_spawnAgentClient), agentInfo);
            }


            // Equip weapons, on server side only and sync to client, note
            if (unitSpawnInfo.UnitId.Contains(AgentPlayerPrefix))
            {
                _gameWorld.GetTeamMapAIManager().GetTeamMapAIs()[(int)unitSpawnInfo.Team].AssignDefaultWeaponSetup(_spawnPlayers[unitSpawnInfo.UnitId], unitSpawnInfo.WeaponSetupIndex);
            }
            else
            {
                _gameWorld.GetTeamMapAIManager().GetTeamMapAIs()[(int)unitSpawnInfo.Team].AssignDefaultWeaponSetup(_spawnBots[unitSpawnInfo.UnitId], unitSpawnInfo.WeaponSetupIndex);
            }
        }
    }

    [Remote]
    private void _spawnAgentClient(String agentInfo)
    {
        int parseIndex = 0;

        String unitId = agentInfo.Split(";")[parseIndex];
        parseIndex++;

        Team.TeamCode unitTeam = (Team.TeamCode)(int.Parse(agentInfo.Split(";")[parseIndex]));
        parseIndex++;

        String unitDisplayName = agentInfo.Split(";")[parseIndex];
        parseIndex++;

        int captureBase = int.Parse(agentInfo.Split(";")[parseIndex]);
        parseIndex++;

        if (unitId.Contains(AgentPlayerPrefix))
        {
            _spawnPlayer(unitId, unitTeam, unitDisplayName, captureBase);
        }
        else
        {
            _spawnBot(unitId, unitTeam, unitDisplayName, captureBase);
        }
    }

    private void _spawnPlayer(String unitID, Team.TeamCode team, String displayName, int captureBase)
    {
        GD.Print("_spawnPlayer unit DISPLAY NAME SET: " + displayName + " FOR UNIT: " + unitID);

        // Already generated
        if (_spawnPlayers.ContainsKey(unitID))
        {
            return;
        }

        int netId = int.Parse(unitID.Replace(AgentPlayerPrefix, ""));

        // Load the scene and create an instance
        Player agent = (Player)(_gameWorld.GetTeamMapAIManager().GetTeamMapAIs()[(int)team].CreateUnit(unitID, displayName, captureBase, false));

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

        _spawnPlayers.Add(unitID, agent);

        EmitSignal(nameof(PlayerCreatedSignal));
        EmitSignal(nameof(AgentCreatedSignal), unitID, team);
    }

    private void _spawnBot(String unitId, Team.TeamCode team, String displayName, int captureBase)
    {
        // Already generated
        if (_spawnBots.ContainsKey(unitId))
        {
            return;
        }

        bool enableAI = false;

        if (GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
        {
            enableAI = true;
        }

        Agent agent = _gameWorld.GetTeamMapAIManager().GetTeamMapAIs()[(int)team].CreateUnit(unitId, displayName, captureBase, enableAI);
        _spawnBots.Add(unitId, agent);

        EmitSignal(nameof(AgentCreatedSignal), unitId, team);

    }

}
