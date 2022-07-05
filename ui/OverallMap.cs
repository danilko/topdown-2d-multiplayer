using Godot;
using System;

public class OverallMap : MarginContainer
{
    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        
    }

    public void Initialize(GameWorld _gameWorld)
    {
        TileMap tileMap = (TileMap)_gameWorld.GetNode("Ground");

        

    }
}
