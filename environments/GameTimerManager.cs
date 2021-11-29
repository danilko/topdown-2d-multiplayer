using Godot;
using System;

public class GameTimerManager : Node
{

    [Signal]
    public delegate void GameTimerTickSignal();

    [Signal]
    public delegate void GameTimerStateChangeSignal();
    [Signal]
    public delegate void GamingTimeEndSignal();

    public enum GameTimerState
    {
        INIT,
        WAITING,
        GAMING,
        END
    }

    private int _maxWaitingTime = 5;

    private int _maxGameTime = 3600;

    private Timer _timer;

    // Use as tick to track countdown time
    private int internalTimer;

    private GameTimerState _gameTimerState;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _timer = (Timer)GetNode("Timer");
        _timer.WaitTime = 1;
    }

    public void Initialize(GameWorld gameWorld)
    {
        _timer.Connect("timeout", this, nameof(_timerTimeout));

        _gameTimerState = GameTimerState.INIT;
    }

    public void StartGameTimer()
    {
        // Set the timer on server to do waiting period count down
        if (GetTree().IsNetworkServer())
        {
            _serverTimerStart(_maxWaitingTime, (int)GameTimerState.WAITING);
        }
        else if (GetTree().NetworkPeer == null)
        {
            _clientTimerStart(_maxWaitingTime, (int)GameTimerState.WAITING);
        }
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

    public int GetElpasedTime()
    {
        // Record elaspse time
        return _maxGameTime - internalTimer;
    }

    public String ConvertToDateFormat(int time)
    {
        int hour = time / 3600;

        int minutes = (time % 3600) / 60;

        int seconds = (time % 3600) % 60;

        return formatTwoDigits(hour) + ":" + formatTwoDigits(minutes) + ":" + formatTwoDigits(seconds);
    }

    private void timeUpCompute()
    {
        GameTimerState nextGameTimerState = _gameTimerState;

        if (_gameTimerState == GameTimerState.WAITING)
        {
            internalTimer = _maxGameTime;
            nextGameTimerState = GameTimerState.GAMING;
        }
        else if (_gameTimerState == GameTimerState.GAMING)
        {
            internalTimer = -1;
            nextGameTimerState = GameTimerState.END;
        }

        // Update to next state
        if (GetTree().IsNetworkServer())
        {
            _serverTimerStart(internalTimer, (int)nextGameTimerState);
        }
        else if (GetTree().NetworkPeer == null)
        {
            _clientTimerStart(internalTimer, (int)nextGameTimerState);
        }

    }

    private void _timerTimeout()
    {
        // Decremental
        internalTimer -= 1;

        // Stop the timer
        if (internalTimer == 0)
        {
            _timer.Stop();

            timeUpCompute();
        }

        EmitSignal(nameof(GameTimerTickSignal), internalTimer);
    }

    private void _serverTimerStart(int waitingTime, int gameTimerState)
    {
        Rpc(nameof(_clientTimerStart), waitingTime, gameTimerState);
    }

    [Remote]
    private void _clientTimerStart(int waitingTime, int gameTimerState)
    {
        _gameTimerState = (GameTimerState)gameTimerState;

        EmitSignal(nameof(GameTimerStateChangeSignal), _gameTimerState);

        _timer.Stop();

        // No need to restart timer, as the game timer is now in end
        if(_gameTimerState != GameTimerState.END)
        {
            internalTimer = _maxWaitingTime;
            _timer.Start();
        }
    }

    public GameTimerState GetGameTimerState()
    {
        return _gameTimerState;
    }
}
