using Godot;
using System;
using System.Collections.Generic;

using System.IO;
using System.IO.Compression;
using System.Text;

public class SimulateGameWorld : GameWorld
{

    [Signal]
    private delegate void SnapshotReceivedSignal();

    [Signal]
    private delegate void NeworkRateUpdateSignal();

    [Signal]
    private delegate void PlayerDefeatedSignal();

    [Signal]
    private delegate void PlayerCreateSignal();

    [Signal]
    private delegate void WaitingPeriodSignal();

    private String _agentPrefix = "agent_";

    private String _agentPlayerPrefix = "agent_player_";

    private String _agentObserverPrefix = "agent_observer_";

    private GameStates gameStates;
    private class SpwanInfo
    {
        public long spawn_index { get; set; }
        public NetworkPlayer networkPlayer { get; set; }
    }


    Dictionary<String, ClientState> clientStates = new Dictionary<String, ClientState>();



    int spawned_bots = 0;

    Dictionary<String, Agent> spawnBots = new Dictionary<String, Agent>();
    Dictionary<String, int> spawnBotTargetBase = new Dictionary<String, int>();


    Network network;

    long _agentBotCounter = 0;
    long _agentPlayerCounter = 0;

    long observerCounter = 0;

    float currentTime;

    // The "signature" (timestamp) added into each generated state snapshot
    // int max is 2,147,483,647 
    // 2,147,483,647 / 60 (snapshots) / 60 (seconds => 1 min) /60 (mins = > 1 hr) /24 (24 hrs => 1 day) = 414 days (before the snapshot expire)
    // So as long as this round of games end in 1 year, the snapshot signature will be unique
    int snapshotSignature = 1;

    // The signature of the last snapshot received
    int lastSnapshotSignature = 0;

    private PathFinding _pathFinding;

    private Godot.RandomNumberGenerator random;

    // Called when the node enters the scene tree for the first time.

    private float currentNetworkBytes = 0;
    private float currentNetworkSnapshots = 0;
    private float currentAppliedNetworkSnapshots = 0;

    private bool _waitingPeriod = false;


    // Use as tick to track countdown time
    private int internalTimer;

    private CapaturableBaseManager _capaturableBaseManager;

    private Godot.Collections.Array<TeamMapAI> _teamMapAIs;

    private Timer _timer;

    private ObstacleManager _obstacleManager;

    private TileMap _tileMap;

    private GameCamera _camera2D;
    private InventoryManager _inventoryManager;

    public override void _Ready()
    {
        random = new RandomNumberGenerator();
        gameStates = (GameStates)GetNode("/root/GAMESTATES");

        _initializeInventoryManager();
        _initializeCamera();
        _initializeTileMap();
        _initializeCapaturableBaseManager();
        _initializeTeamMapAI();
        _syncBots();

    }

    private void _initializeCamera()
    {
        _camera2D = (GameCamera)GetNode("GameCamera");
        _camera2D.Current = true;
    }

    private void _initializeTileMap()
    {
        _tileMap = (TileMap)GetNode("Ground");
    }

    private void _initializeCapaturableBaseManager()
    {
        _capaturableBaseManager = (CapaturableBaseManager)GetNode("CapaturableBaseManager");
        _capaturableBaseManager.Initailize(this);
    }

    private void _initializeTeamMapAI()
    {
        _teamMapAIs = new Godot.Collections.Array<TeamMapAI>();

        // Start with neutral and above
        for (int index = 0; index < (int)(Team.TeamCode.NEUTRAL); index++)
        {
            TeamMapAI ai = (TeamMapAI)((PackedScene)GD.Load("res://ai/TeamMapAI.tscn")).Instance();
            ai.Name = nameof(TeamMapAI) + "_" + (Team.TeamCode)index;
            AddChild(ai);

            ai.Initialize(this, _inventoryManager, _capaturableBaseManager.GetBases(), (Team.TeamCode)index, _pathFinding);

            _teamMapAIs.Add(ai);

            foreach (CapturableBase capturable in _capaturableBaseManager.GetBases())
            {
                capturable.Connect(nameof(CapturableBase.BaseTeamChangeSignal), ai, nameof(TeamMapAI.HandleCapturableBaseCaptured));
            }
        }
    }

    private void _initializeInventoryManager()
    {
        _inventoryManager = (InventoryManager)GetNode("InventoryManager");
    }

    private void _syncBots()
    {
        // Calculate the target amount of spawned bots
        String botId = (int)Team.TeamCode.TEAMALPHA + ";" + _agentPrefix + _agentBotCounter;
        spawnBotTargetBase.Add(_agentPrefix + _agentBotCounter, (int)_agentBotCounter);
        _agentBotCounter++;
        _addBotOnNetwork(botId);


        botId = (int)Team.TeamCode.TEAMBETA + ";" + _agentPrefix + _agentBotCounter;
        spawnBotTargetBase.Add(_agentPrefix + _agentBotCounter, (int)_agentBotCounter);
        _agentBotCounter++;
        _addBotOnNetwork(botId);


    }

    private void _addBotOnNetwork(String botId)
    {
        Team.TeamCode team = (Team.TeamCode)int.Parse(botId.Split(";")[0]);
        String unitName = botId.Split(";")[1];

        if (!spawnBots.ContainsKey(unitName))
        {

            Agent agent = _teamMapAIs[(int)team].CreateUnit(unitName, unitName, false);
            spawnBots.Add(unitName, agent);
        }
    }

    public void changeAgentBehavior()
    {
        foreach (Agent agent in spawnBots.Values)
        {
            // Locate the bot node
            Agent enemyNode = (Agent)_teamMapAIs[(int)agent.GetCurrentTeam()].GetUnit(agent.Name);

            if (enemyNode == null || !IsInstanceValid(enemyNode))
            {
                // Ideally should give a warning that a bot node wasn't found
                continue;
            }

            enemyNode.Heal(100);
            spawnBotTargetBase[enemyNode.GetUnitName()] = (spawnBotTargetBase[enemyNode.GetUnitName()] + 1) % _capaturableBaseManager.GetBases().Count;
        }
    }

    public override void _PhysicsProcess(float delta)
    {
        // Update the timeout counter
        currentTime += delta;
        if (currentTime < gameStates.updateDelta)
        {
            return;
        }

        // "Reset" the time counting
        currentTime -= gameStates.updateDelta;

        int index = 0;
        // And update the game state
        foreach (Agent agent in spawnBots.Values)
        {
            // Locate the bot node
            Agent enemyNode = (Agent)_teamMapAIs[(int)agent.GetCurrentTeam()].GetUnit(agent.Name);

            if (enemyNode == null || !IsInstanceValid(enemyNode))
            {
                // Ideally should give a warning that a bot node wasn't found
                continue;
            }


            CapturableBase capturableBase = (CapturableBase)(_capaturableBaseManager.GetBases()[spawnBotTargetBase[agent.GetUnitName()]]);

            Vector2 randomPosition = capturableBase.GetRandomPositionWithinCaptureRadius();

            enemyNode.MoveToward(randomPosition - enemyNode.GlobalPosition, delta);

            enemyNode.LookAt(spawnBots[_agentPrefix + ((index + 1) % spawnBots.Count)].GlobalPosition);

            // Always fire
            enemyNode.Fire(Weapon.WeaponOrder.Right, 1);
            enemyNode.Fire(Weapon.WeaponOrder.Left, 1);
            index++;
        }
    }

    private void _onProjectileShoot(PackedScene projectile, Vector2 _position, Vector2 _direction, Node2D source, Team sourceTeam, Node2D target)
    {
        Projectile newProjectile = (Projectile)projectile.Instance();
        AddChild(newProjectile);
        newProjectile.Initialize(_position, _direction, source, sourceTeam, target);
    }

    private void _onDamageCalculation(int damage, Vector2 hitDir, Godot.Object source, Team sourceTeam, Godot.Object target)
    {
        if (target != null && IsInstanceValid(target))
        {
            if (target.HasMethod(nameof(Agent.TakeDamage)))
            {
                Agent targetAgent = (Agent)(target);
                Agent sourceAgent = (Agent)(source);
                targetAgent.TakeDamage(damage, hitDir, sourceAgent, sourceTeam);
            }
            else if (target.HasMethod(nameof(Obstacle.TakeEnvironmentDamage)))
            {
                ((Obstacle)(target)).TakeEnvironmentDamage(damage);
            }
        }
    }








}
