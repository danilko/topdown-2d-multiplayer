using Godot;
using System;
using System.Collections.Generic;

public class GameConditionManager : Node
{
    private TeamMapAIManager _teamMapAIManager;
    private GameTimerManager _gameTimerManager;

    private List<String> _randomFirstNames;

    private List<String> _randomLastNames;

    private AgentSpawnManager _agentSpawnManager;

    private GameWorld _gameWorld;

    private Team _winningTeam;
    private EndGameCondition _endGameCondition;
    private CapturableBaseManager _capturableBaseManager;
    private GameStates _gameStates;
    public enum EndGameCondition
    {
        TIME_UP,
        ALL_UNITS_DEFEATED
    }

    public class GameResultMessage
    {
        public EndGameCondition EndGameCondition { get; set; }
        public Team.TeamCode WinningTeamCode { get; set; }
        public String ElapsedTime { get; set; }
    }

    private Godot.RandomNumberGenerator _random;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _randomFirstNames = new List<string>(){"John", "Bob", "Joe", "Jose", "Alice", "Ethan", "Vicent", "Allen", "Elisa", "Peter", "Harry", "Arthur", "Carlos"};
        _randomLastNames = new List<string>(){"Wood", "Mars", "Park", "Venus", "Sun", "Dong", "Stone", "Pipe", "Garden", "Booker", "Keeper", "Firework"};
    
        _random = new RandomNumberGenerator();
    }

    public void Initialize(GameWorld gameWorld)
    {
        _winningTeam = new Team();
        _winningTeam.CurrentTeamCode = Team.TeamCode.TEAMUNKOWN;

        _gameWorld = gameWorld;
        _gameTimerManager = _gameWorld.GetGameTimerManager();
        _teamMapAIManager = _gameWorld.GetTeamMapAIManager();
        _capturableBaseManager = _gameWorld.GetCapturableBaseManager();
        _gameStates = _gameWorld.GetGameStateManager().GetGameStates();

        _agentSpawnManager = _gameWorld.GetAgentSpawnManager();

        // Client will rely on server to "notify capture of server"
        // Server and signle player will capture itself
        if (GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
        {
            _gameTimerManager.Connect(nameof(GameTimerManager.GameTimerTickSignal), this, nameof(_checkGameWinningCondition));
        }

    }

    public void InitAgents()
    {

        Dictionary<int, int> teamMemberDictionary = new Dictionary<int, int>();

        foreach (TeamMapAI currentAI in _teamMapAIManager.GetTeamMapAIs())
        {
            teamMemberDictionary.Add((int)currentAI.GetTeam(), 0);
        }


        // Will not generate player under simulation
        if (_gameWorld.GetGameStateManager().GetGameStates().GetGameType() != GameStates.GameType.SIMULATION)
        {
            if (GetTree().NetworkPeer != null && GetTree().IsNetworkServer())
            {
                foreach (KeyValuePair<int, NetworkPlayer> item in _gameWorld.GetNetworkSnasphotManager().GetNetwork().networkPlayers)
                {
                    String unitId = AgentSpawnManager.AgentPlayerPrefix + item.Value.net_id;
                    Team.TeamCode team = (Team.TeamCode)item.Value.team;

                    String displayName = item.Value.name;

                    teamMemberDictionary[(int)item.Value.team] += 1;

                    _agentSpawnManager.PlaceNewUnit(unitId, team, displayName, AgentSpawnManager.UNIT_CONFIG_TIME);

                }
            }
            else if (GetTree().NetworkPeer == null)
            {
                if (_gameWorld.GetGameStateManager().GetGameStates().GetGameType() != GameStates.GameType.SIMULATION)
                {
                    String unitId = AgentSpawnManager.AgentPlayerPrefix + _gameWorld.GetNetworkSnasphotManager().GetNetwork().gamestateNetworkPlayer.net_id;
                    Team.TeamCode team = (Team.TeamCode)0;

                    String displayName = "Player";
                    teamMemberDictionary[0] += 1;

                    _agentSpawnManager.PlaceNewUnit(unitId, team, displayName, AgentSpawnManager.UNIT_CONFIG_TIME);
                }
            }
        }


        int counter = 0;

        foreach (TeamMapAI currentAI in _teamMapAIManager.GetTeamMapAIs())
        {
            if (currentAI.GetAIControl() && CheckIfCanSpawn(currentAI.GetTeam()))
            {

                int teamPlayers = currentAI.GetTeamTotalUnitCount() - teamMemberDictionary[(int)currentAI.GetTeam()];

                // If it is simulation, only did 1
                if (_gameWorld.GetGameStateManager().GetGameStates().GetGameType() == GameStates.GameType.SIMULATION)
                {
                    teamPlayers = 1;
                }

                for (int index = 0; index < teamPlayers; index++)
                {
                    String unitId = AgentSpawnManager.AgentBotPrefix + counter;
                    Team.TeamCode team = currentAI.GetTeam();
                    String unitName = unitId;
                    // Generate random names for AI controlled agent
                    String displayName = _randomFirstNames[(counter + _random.RandiRange(0, 1000))% (_randomFirstNames.Count - 1)] + "_" + _randomLastNames[ (counter + _random.RandiRange(0, 1000)) % (_randomLastNames.Count - 1)];


                    _agentSpawnManager.PlaceNewUnit(unitId, team, displayName, AgentSpawnManager.UNIT_CONFIG_TIME);
                    counter++;
                }
            }

        }
    }

    public Boolean CheckIfCanSpawn(Team.TeamCode teamCode)
    {
        TeamMapAI teamMapAI = _gameWorld.GetTeamMapAIManager().GetTeamMapAIs()[(int)teamCode];
        if (teamMapAI.isUnitUsageAmountAllowed())
        {
            bool availableBase = false;
            foreach (CapturableBase currentBase in _capturableBaseManager.GetCapturableBases())
            {
                if (currentBase.GetCaptureBaseTeam() == teamCode)
                {
                    availableBase = true;
                }
            }

            return availableBase;
        }

        return false;
    }

    private void _checkGameWinningCondition(int time)
    {
        Boolean endGame = false;

        // No need to check condition as still in waiting period
        if (_gameTimerManager.GetGameTimerState() == GameTimerManager.GameTimerState.WAITING)
        {
            return;
        }

        // If time is up, end game with tie
        if (checkIfTimeUp())
        {
            checkLargestCatpureBases();

            endGame = true;
            _endGameCondition = EndGameCondition.TIME_UP;
        }
        else if (checkIfOnlyOneTeamRemain())
        {
            endGame = true;
            _endGameCondition = EndGameCondition.ALL_UNITS_DEFEATED;
        }

        if (endGame)
        {
            _notifyEndGame();
        }
    }

    private bool checkIfTimeUp()
    {
        _winningTeam.CurrentTeamCode = Team.TeamCode.TEAMUNKOWN;
        Boolean timeUp = _gameTimerManager.GetGameTimerState() == GameTimerManager.GameTimerState.END;

        return _gameTimerManager.GetGameTimerState() == GameTimerManager.GameTimerState.END;
    }

    private void checkLargestCatpureBases()
    {
        _winningTeam.CurrentTeamCode = Team.TeamCode.TEAMUNKOWN;

        Dictionary<Team.TeamCode, int> captureBasesMap = new Dictionary<Team.TeamCode, int>();
        Dictionary<int, int> captureBasesCountMap = new Dictionary<int, int>();

        foreach (CapturableBase currentBase in _capturableBaseManager.GetCapturableBases())
        {
            if (!captureBasesMap.ContainsKey(currentBase.GetCaptureBaseTeam()))
            {
                captureBasesMap.Add(currentBase.GetCaptureBaseTeam(), 0);
            }
            captureBasesMap[currentBase.GetCaptureBaseTeam()]++;
        }

        int largetBaseCount = -1;

        foreach (Team.TeamCode currentTeamCode in captureBasesMap.Keys)
        {
            if (!captureBasesCountMap.ContainsKey(captureBasesMap[currentTeamCode]))
            {
                captureBasesCountMap.Add(captureBasesMap[currentTeamCode], 0);
            }
            captureBasesCountMap[captureBasesMap[currentTeamCode]]++;

            if (captureBasesMap[currentTeamCode] > largetBaseCount)
            {
                _winningTeam.CurrentTeamCode = currentTeamCode;
                largetBaseCount = captureBasesMap[currentTeamCode];
            }
        }

        // Check if there is any team with the same number
        if (captureBasesCountMap[largetBaseCount] > largetBaseCount)
        {
            // If so, it is a tie
            _winningTeam.CurrentTeamCode = Team.TeamCode.TEAMUNKOWN;
        }
    }

    public Boolean CheckIfPlayerNeedToBeRespawn(String unitId)
    {
        // Check if player need to be respawn (i.e. if disconnected, no need to respawn)
        if (unitId.Contains(AgentSpawnManager.AgentPlayerPrefix))
        {
            int netId = int.Parse(unitId.Replace(AgentSpawnManager.AgentPlayerPrefix, ""));

            if (!_gameWorld.GetNetworkSnasphotManager().GetNetwork().networkPlayers.ContainsKey(netId))
            {
                // This player is disconnected, so no need to respawn
                return false;
            }
        }

        return true;
    }

    private bool checkIfOnlyOneTeamRemain()
    {
        int currentFieldTeam = 0;
        foreach (TeamMapAI currentAI in _teamMapAIManager.GetTeamMapAIs())
        {
            if (currentAI.isNewUnitAllow() || currentAI.GetUnitsContainer().GetChildren().Count != 0)
            {
                _winningTeam.CurrentTeamCode = currentAI.GetTeam();
                currentFieldTeam++;
            }
        }

        if (currentFieldTeam == 1)
        {
            return true;
        }

        return false;
    }

    private void _notifyEndGame()
    {
        String message = _encodeGameResult();

        if (GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
        {
            // Call locally for server
            _clientNotifyEndGame(message);

            if (GetTree().NetworkPeer != null)
            {
                Rpc(nameof(_clientNotifyEndGame), message);
            }
        }
    }

    private String _encodeGameResult()
    {
        String message = "" + (int)_endGameCondition + ";";
        message = message + (int)_winningTeam.CurrentTeamCode + ";";
        message = message + _gameTimerManager.ConvertToDateFormat(_gameTimerManager.GetElpasedTime());

        return message;
    }

    public static GameResultMessage DecodeGameResult(String encodedData)
    {
        GameResultMessage gameResultMessage = new GameResultMessage();

        int parseIndex = 0;

        gameResultMessage.EndGameCondition = (EndGameCondition)int.Parse(encodedData.Split(";")[parseIndex]);
        parseIndex++;

        gameResultMessage.WinningTeamCode = (Team.TeamCode)int.Parse(encodedData.Split(";")[parseIndex]);
        parseIndex++;

        gameResultMessage.ElapsedTime = encodedData.Split(";")[parseIndex];
        parseIndex++;

        return gameResultMessage;
    }

    [Remote]
    private void _clientNotifyEndGame(String message)
    {
        _gameStates.setMessagesForNextScene(message);
        _gameStates.endGameScreen();
    }

}
