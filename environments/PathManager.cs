using Godot;
using System;

public class PathManager : Node
{
    private GameWorld _gameWorld;
    private PathFinding _pathFinding;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        
    }

    public void Initialize(GameWorld gameWorld)
    {
        _gameWorld = gameWorld;
        TileMap _tileMap = (TileMap)_gameWorld.GetNode("Ground");

        if (GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
        {
            _pathFinding = (PathFinding)((PackedScene)GD.Load("res://ai/PathFinding.tscn")).Instance(); ;
            this.AddChild(_pathFinding);
            _pathFinding.Initialize(gameWorld, _tileMap, _gameWorld.GetObstacleManager());
        }
    }

    public PathFinding GetPathFinding()
    {
        return _pathFinding;
    }

//  // Called every frame. 'delta' is the elapsed time since the previous frame.
//  public override void _Process(float delta)
//  {
//      
//  }
}
