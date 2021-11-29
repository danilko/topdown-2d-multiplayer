using Godot;
using System;
using System.Collections.Generic;

public class GameConditionManager : Node
{
    private TeamMapAIManager _teamMapAIManager;
    private GameTimerManager _gameTimerManager;
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

    public class GameResultMessage{
        public EndGameCondition EndGameCondition{get; set;}
        public Team.TeamCode WinningTeamCode{get; set;}
        public String ElapsedTime{get; set;}
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {

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

        if(GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
        {
            _gameTimerManager.Connect(nameof(GameTimerManager.GameTimerTickSignal), this, nameof(_checkGameWinningCondition));
        }
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

        Dictionary <Team.TeamCode, int> captureBasesMap = new Dictionary <Team.TeamCode, int> ();
        Dictionary <int, int> captureBasesCountMap = new Dictionary <int, int> ();

        foreach (CapturableBase currentBase in _capturableBaseManager.GetCapturableBases())
        {
            if (! captureBasesMap.ContainsKey(currentBase.GetCaptureBaseTeam()))
            {
                captureBasesMap.Add(currentBase.GetCaptureBaseTeam(), 0);
            }
            captureBasesMap[currentBase.GetCaptureBaseTeam()]++;
        }

        int largetBaseCount = -1;

        foreach (Team.TeamCode currentTeamCode in captureBasesMap.Keys)
        {
            if (! captureBasesCountMap.ContainsKey(captureBasesMap[currentTeamCode]))
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

        Rpc(nameof(_clientNotifyEndGame), message);
    }

    private String _encodeGameResult()
    {
        String message = "" + (int)_endGameCondition;
        message = message + (int)_winningTeam.CurrentTeamCode;
        message = message + ";" + _gameTimerManager.ConvertToDateFormat(_gameTimerManager.GetElpasedTime());

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
