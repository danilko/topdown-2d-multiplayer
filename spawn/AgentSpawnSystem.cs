using Godot;
using System;
using System.Collections.Generic;


public class AgentSpawnInfo
{
    public Team Team;
    public String Name;
    public int Delay;
}

public class AgentSpawnSystem : Node2D
{
    private Queue<AgentSpawnInfo> spawnQueue;
    
    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        spawnQueue = new Queue<AgentSpawnInfo>();
    }

    public void placeAgentOnWaitingQueue(String Agent, Agent agent)
    {

    }

//  // Called every frame. 'delta' is the elapsed time since the previous frame.
//  public override void _Process(float delta)
//  {
//      
//  }
}
