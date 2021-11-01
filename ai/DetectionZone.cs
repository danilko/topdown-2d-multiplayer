using Godot;
using System;
using System.Collections.Generic;

public class DetectionZone : Area2D
{
    [Signal]
    public delegate void TargetAgentChangeSignal();

    [Signal]
    public delegate void DetectedAgentsChangeSignal();

   [Signal]
    public delegate void  AgentEnteredSignal();

    [Signal]
    public delegate void AgentExitedSignal();

    private Team currentTeam;

    private Agent _agent;

    private Agent _targetAgent = null;

    private List<String> _targetAgents;
    private Dictionary<String, Agent> _detectedAgents;
    private GameWorld _gameWorld;

    public override void _Ready()
    {
        _targetAgents = new List<String>();
        _detectedAgents = new Dictionary<String, Agent>();
    }

    public void Initialize(GameWorld gameWorld, Agent agent, float detectRaidus)
    {

        _gameWorld = gameWorld;
        _agent = agent;

        CollisionShape2D detectRadius = (CollisionShape2D)GetNode("CollisionShape2D");

        CircleShape2D shape = new CircleShape2D();

        shape.Radius = detectRaidus;
        detectRadius.Shape = shape;
    }

    public Agent getTargetAgent()
    {
        return _targetAgent;
    }

    public Agent getTargetAgents()
    {
        return _targetAgent;
    }

    public Dictionary<String, Agent> GetDetectedAgents()
    {
        return _detectedAgents;
    }

    private void _onDetectionZoneBodyEntered(Node body)
    {

        if (body.HasMethod(nameof(Agent.GetTeam))
                && body != _agent
                && !_detectedAgents.ContainsKey(((Agent)body).GetUnitName()))
        {

            Agent agent = (Agent)body;

            EmitSignal(nameof(AgentEnteredSignal), agent);


            _detectedAgents.Add(agent.GetUnitName(), agent);

            // If not same team identifier, identify as target
            if (agent.GetTeam() != _agent.GetTeam())
            {
                if (_targetAgent == null)
                {
                    _targetAgent = agent;
                    EmitSignal(nameof(TargetAgentChangeSignal));
                }

                _targetAgents.Add(agent.GetUnitName());

            }
        }

        EmitSignal(nameof(DetectedAgentsChangeSignal));
    }

    private void _onDetectionZoneBodyExited(Node body)
    {
        if (body.HasMethod(nameof(Agent.GetTeam))
            && body != _agent
            && _detectedAgents.ContainsKey(((Agent)body).GetUnitName()))
        {
            Agent agent = (Agent)body;

            EmitSignal(nameof(AgentExitedSignal), agent);


            // Clean up current target/possible target
            if (body == _targetAgent)
            {
                _targetAgent = null;
                // Notify there is no target currently
                EmitSignal(nameof(TargetAgentChangeSignal));
            }

            
             _targetAgents.Remove(agent.GetUnitName());
            _detectedAgents.Remove(agent.GetUnitName());

           
            // If target is not vaild, then will try to check for next target
            if (_targetAgent == null || IsInstanceValid(_targetAgent))
            {
                // Set the agent to be null as the instance is invalid if it is not null
                _targetAgent = null;

                // Compute target
                GetNextTarget();
            }

            EmitSignal(nameof(DetectedAgentsChangeSignal));
        }


    }

    public void GetNextTarget()
    {
        _targetAgent = null;

        Agent targetAgent = null;

        Godot.Collections.Array<String> removeTargetList = new Godot.Collections.Array<String>();

        foreach (String targetAgentUnitName in _targetAgents)
        {
            targetAgent = _detectedAgents[targetAgentUnitName];

            if (targetAgent != null && IsInstanceValid(targetAgent))
            {
                // Identify the agent is different from current agent
                if (_targetAgent != null && targetAgent.GetUnitName() != _targetAgent.GetUnitName())
                {
                    break;
                }
            }
            else
            {
                targetAgent = null;
                // Remove this target from list as it is no longer valid
                removeTargetList.Add(targetAgentUnitName);
            }
        }

        // Clean up the list
        foreach (String targetAgentUnitName in removeTargetList)
        {
            _targetAgents.Remove(targetAgentUnitName);
            _detectedAgents.Remove(targetAgentUnitName);
        }



        // Identify a new target, so assign it
        if (targetAgent != null)
        {
            _targetAgent = targetAgent;
            EmitSignal(nameof(TargetAgentChangeSignal));
        }
    }


}
