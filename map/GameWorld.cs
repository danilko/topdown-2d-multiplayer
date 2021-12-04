using Godot;
using System;
using System.Collections.Generic;

public class GameWorld : Node2D
{
    protected GameStates GameStates;

    private CapturableBaseManager _capturableBaseManager;

    private GameCamera _gameCamera;

    private HUD _hud;

    private InventoryManager _inventoryManager;
    private ObstacleManager _obstacleManager;

    private NetworkSnapshotManager _networkSnapshotManager;
    private GameConditionManager _gameConditionManager;
    private AgentSpawnManager _agentSpawnManager;
    private GameTimerManager _gameTimerManager;
    private GameStateManager _gameStateManager;
    private TeamMapAIManager _teamMapAIManager;
    private PathManager _pathManager;

    private ProjectileManager _projectileManager;

    public override void _Ready()
    {
        _initializeInventoryManager();

        _initializeNetworkSnapshotManager();

        _initializeGameCamera();
        _initailizeGameTimerManager();
        _initailizeGameStateManager();
        _initializeObsctacleManager();
        _initializeCapaturableBaseManager();
        _initializePathManager();
        _initializeTeamMapAIManager();
 
        _initailizeAgentSpawnManager();
        _initailizeGameConditionManager();

        _initailizeProjectileManager();

        _initializeHUD();

        // Complete all initialize, start game timer
        _gameTimerManager.StartGameTimer();

        _gameConditionManager.InitAgents();
    }

    private void _initializeGameCamera()
    {
        _gameCamera = (GameCamera)GetNode("GameCamera");
        _gameCamera.Current = true;
    }

    public GameCamera GetGameCamera()
    {
        return _gameCamera;
    }

    protected void _initializeHUD()
    {
        _hud = (HUD)GetNode("HUD");
        _hud.Initailize(this);
    }

    public HUD GetHUD()
    {
        return _hud;
    }

    private void _initailizeProjectileManager()
    {
        _projectileManager = (ProjectileManager)GetNode("ProjectileManager");
        _projectileManager.Initailize(this);
    }

    public ProjectileManager GetProjectileManager()
    {
        return _projectileManager;
    }

    private void _initializeNetworkSnapshotManager()
    {
        _networkSnapshotManager = (NetworkSnapshotManager)GetNode("NetworkSnapshotManager");
        _networkSnapshotManager.Initialize(this);
    }

    public NetworkSnapshotManager GetNetworkSnasphotManager()
    {
        return _networkSnapshotManager;
    }

    private void _initailizeGameTimerManager()
    {
        _gameTimerManager = (GameTimerManager)GetNode("GameTimerManager");
        _gameTimerManager.Initialize(this);
    }

    public GameTimerManager GetGameTimerManager()
    {
        return _gameTimerManager;
    }

    private void _initailizeGameStateManager()
    {
        _gameStateManager = (GameStateManager)GetNode("GameStateManager");
        _gameStateManager.Initialize(this);
    }

    public GameStateManager GetGameStateManager()
    {
        return _gameStateManager;
    }

    private void _initializePathManager()
    {
        _pathManager = (PathManager)GetNode("PathManager");
        _pathManager.Initialize(this);
    }

    public PathManager GetPathManager()
    {
        return _pathManager;
    }

    private void _initailizeGameConditionManager()
    {
        _gameConditionManager =  (GameConditionManager)GetNode("GameConditionManager");
        _gameConditionManager.Initialize(this);
    }

    public GameConditionManager GetGameConditionManager()
    {
        return _gameConditionManager;
    }

    private void _initailizeAgentSpawnManager()
    {
        _agentSpawnManager =  (AgentSpawnManager)GetNode("AgentSpawnManager");
        _agentSpawnManager.Initialize(this);
    }

    public AgentSpawnManager GetAgentSpawnManager()
    {
        return _agentSpawnManager;
    }

    public InventoryManager getInventoryManager()
    {
        return _inventoryManager;
    }

    private void _initializeObsctacleManager()
    {
  
        TileMap tileMap = (TileMap)GetNode("Ground");
        _obstacleManager = (ObstacleManager)GetNode("ObstacleManager");

        _obstacleManager.Initialize(this);
    }

    public ObstacleManager GetObstacleManager()
    {
        return _obstacleManager;
    }

    private void _initializeCapaturableBaseManager()
    {
        _capturableBaseManager = (CapturableBaseManager)GetNode("CapaturableBaseManager");
        _capturableBaseManager.Initailize(this);
    }

    public CapturableBaseManager GetCapturableBaseManager()
    {
        return _capturableBaseManager;
    }

    private void _initializeTeamMapAIManager()
    {
        _teamMapAIManager  = (TeamMapAIManager)GetNode("TeamMapAIManager");
        _teamMapAIManager.Initialize(this);
    }

    public TeamMapAIManager GetTeamMapAIManager()
    {
        return _teamMapAIManager;
    }

    protected void _initializeInventoryManager()
    {
        _inventoryManager = (InventoryManager)GetNode("InventoryManager");
        _inventoryManager.Initialize(this);
    }

    public override void _PhysicsProcess(float delta)
    {
        // And update the game state
        _gameStateManager.UpdateState(delta);

    }
}
