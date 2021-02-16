using Godot;
using System;

public class ScreenIndicator : Node2D
{

    private float _zoom = 1.5f;

    private Agent _player;
    private Godot.Collections.Dictionary<String, Agent> _agents;

    private Godot.Collections.Dictionary<String, Node2D> _agentMarkers;

    private Node2D _agentMarker;

    private Godot.Collections.Dictionary<String, Team.TeamCode> _targetAgents;
    private Agent _agent = null;

    public override void _Ready()
    {
        _agentMarker = (Node2D)GetNode("AgentMarker");
    }

    public void Initialize(Agent agent)
    {
        _agent = agent;
        _agents = new Godot.Collections.Dictionary<String, Agent>();
        _agentMarkers = new Godot.Collections.Dictionary<String, Node2D>();
        _targetAgents = new Godot.Collections.Dictionary<String, Team.TeamCode>();
    }

    public void SetPlayer(Agent agent)
    {
        _player = agent;
    }

    public void RemovePlayer()
    {
        _player = null;
    }

    public void AddAgent(Agent agent)
    {
        if (!_agents.ContainsKey(agent.GetUnitName()))
        {
            Node2D agentMarker = (Node2D)_agentMarker.Duplicate();
            agentMarker.Name = agent.GetUnitName() + "_marker";

            agentMarker.Modulate = Team.TeamColor[(int)agent.GetCurrentTeam()];

            AddChild(agentMarker);

            agentMarker.Show();

            // Add agent to dictonary
            _agents.Add(agent.GetUnitName(), agent);

            // Add marker to dictionary
            _agentMarkers.Add(agent.GetUnitName(), agentMarker);
        }
    }

    public void RemoveAgent(Agent agent)
    {
        if (_agents.ContainsKey(agent.GetUnitName()))
        {
            // Add agent to dictonary
            _agents.Remove(agent.GetUnitName());

            Node2D agentMarker = _agentMarkers[agent.GetUnitName()];

            // Add marker to dictionary
            _agentMarkers.Remove(agent.GetUnitName());

            agentMarker.QueueFree();
        }
    }

    public override void _Process(float delta)
    {
        if(_agent == null || ! IsInstanceValid(_agent))
        {
            return;
        }
        
        foreach (Agent agent in _agents.Values)
        {
            if (agent != null && IsInstanceValid(agent))
            {
                Node2D agentMarker = _agentMarkers[agent.GetUnitName()];
                agentMarker.GlobalRotation = agent.GlobalRotation + Mathf.Pi / 2.0f;

                // Update marker
                agentMarker.LookAt(agent.GlobalPosition);
                float distance = agentMarker.GlobalPosition.DistanceTo(agent.GlobalPosition);
                ((Label)agentMarker.GetNode("Text")).Text = agent.GetUnitName() + " " + distance;
            }
        }
    }


    private void _onDetectionZoneBodyEntered(Node body)
    {

        if (body.HasMethod(nameof(Agent.GetCurrentTeam)) && body != _agent)
        {
            AddAgent((Agent)body);
        }
    }

    private void _onDetectionZoneBodyExited(Node body)
    {
        if (body.HasMethod(nameof(Agent.GetCurrentTeam)) && body != _agent)
        {
            RemoveAgent((Agent)body);
        }
    }


}
