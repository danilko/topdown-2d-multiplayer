using Godot;
using System;

public class ScreenIndicator : Node2D
{

    private float _zoom = 1.5f;

    private Agent _player;
    private Godot.Collections.Dictionary<String, Agent> _agents;

    private Godot.Collections.Dictionary<String, Node2D> _agentMarkers;

    private Node2D _agentMarker;

    private Agent _agent = null;

    private TextureProgress _healthBar;

    private Node2D _healthPanel;

    public override void _Ready()
    {
        _agentMarker = (Node2D)GetNode("AgentMarker");
        _healthPanel = (Node2D)GetNode("HealthPanel");
        _healthBar = (TextureProgress)_healthPanel.GetNode("HealthBar");
    }

    public void Initialize(Agent agent)
    {
        _agent = agent;
        _agents = new Godot.Collections.Dictionary<String, Agent>();
        _agentMarkers = new Godot.Collections.Dictionary<String, Node2D>();
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
        _removeAgent(agent.GetUnitName());
    }

    private void _removeAgent(String agentUnitName)
    {
        if (_agents.ContainsKey(agentUnitName))
        {
            // Add agent to dictonary
            _agents.Remove(agentUnitName);

            Node2D agentMarker = _agentMarkers[agentUnitName];

            // Add marker to dictionary
            _agentMarkers.Remove(agentUnitName);

            agentMarker.QueueFree();
        }
    }

    public override void _Process(float delta)
    {
        if (_agent == null || !IsInstanceValid(_agent))
        {
            return;
        }

        Godot.Collections.Array<String> removeTargetList = new Godot.Collections.Array<String>();


        foreach (String agentUnitName in _agents.Keys)
        {
            Agent agent = _agents[agentUnitName];
            if (agent != null && IsInstanceValid(agent))
            {
                Node2D agentMarker = _agentMarkers[agent.GetUnitName()];
                agentMarker.GlobalRotation = agent.GlobalRotation + Mathf.Pi / 2.0f;

                // Update marker
                agentMarker.LookAt(agent.GlobalPosition);
                float distance = agentMarker.GlobalPosition.DistanceTo(agent.GlobalPosition);
                ((Label)agentMarker.GetNode("Text")).Text = agent.GetUnitName() + " " + distance;
            }
            else
            {
                removeTargetList.Add(agentUnitName);
            }
        }

        // Clean up the list
        foreach (String targetAgentUnitName in removeTargetList)
        {
            _removeAgent(targetAgentUnitName);
        }

_healthPanel.GlobalRotation = 0;
    }

    public void UpdateHealth(int value)
    {
        Tween tween = (Tween)GetNode("Tween");
        tween.InterpolateProperty(_healthBar, "value", _healthBar.Value,
        value, 0.2f,
        Tween.TransitionType.Linear, Tween.EaseType.InOut);
        tween.Start();
        ((Label)_healthPanel.GetNode("HealthText")).Text = value + "%";
    }

    private void _onAgentEntered(Agent agent)
    {
        if (agent != null && IsInstanceValid(agent))
        {
            AddAgent(agent);

        }
    }

    private void _onAgentExited(Agent agent)
    {
        if (agent != null && IsInstanceValid(agent))
        {
            RemoveAgent(agent);
        }
    }

}
