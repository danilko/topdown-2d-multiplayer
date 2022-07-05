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
    public delegate void AgentEnteredSignal();

    [Signal]
    public delegate void AgentExitedSignal();

    [Signal]
    public delegate void AutoTargetAgentSelectionChangeSignal();

    private Agent _agent;

    private Agent _targetAgent = null;

    private List<String> _targetAgents;
    private Dictionary<String, Agent> _detectedAgents;
    private GameWorld _gameWorld;

    private Boolean _autoTargetSelect = true;

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

    public Agent GetTargetAgent()
    {
        return _targetAgent;
    }

    public Agent GetTargetAgents()
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
                && !_detectedAgents.ContainsKey(((Agent)body).GetUnitID()))
        {

            Agent agent = (Agent)body;

            EmitSignal(nameof(AgentEnteredSignal), agent);


            _detectedAgents.Add(agent.GetUnitID(), agent);

            // If not same team identifier, identify as target
            if (agent.GetTeam() != _agent.GetTeam())
            {

                _targetAgents.Add(agent.GetUnitID());

                if (_autoTargetSelect && _targetAgent == null)
                {
                    GetNextTargetAgent();
                }
            }
        }

        EmitSignal(nameof(DetectedAgentsChangeSignal));
    }

    private void _onDetectionZoneBodyExited(Node body)
    {
        if (body.HasMethod(nameof(Agent.GetTeam))
            && body != _agent
            && _detectedAgents.ContainsKey(((Agent)body).GetUnitID()))
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


            _targetAgents.Remove(agent.GetUnitID());
            _detectedAgents.Remove(agent.GetUnitID());


            // If target is not vaild, then will try to check for next target
            if (_targetAgent == null || IsInstanceValid(_targetAgent))
            {
                // Set the agent to be null as the instance is invalid if it is not null
                _targetAgent = null;

                if(_autoTargetSelect)
                {
                    // Compute target
                    GetNextTargetAgent();
                }
            }

            EmitSignal(nameof(DetectedAgentsChangeSignal));
        }


    }

    public void TriggerTargetAgentSelection()
    {
        if(_autoTargetSelect)
        {
            _autoTargetSelect = false;
            _targetAgent = null;
            EmitSignal(nameof(TargetAgentChangeSignal));
        }
        else
        {
            _autoTargetSelect = true;
            GetNextTargetAgent();
        }

        EmitSignal(nameof(AutoTargetAgentSelectionChangeSignal), _autoTargetSelect);
    }

    public void SetTargetAgent(String targetAgentUnitID)
    {
        if (String.IsNullOrWhiteSpace(targetAgentUnitID))
        {
            _targetAgent = null;
            EmitSignal(nameof(TargetAgentChangeSignal));
            return;
        }

        // Only update if the target agent did detect (should eventually catch up)
        if(_detectedAgents.ContainsKey(targetAgentUnitID))
        {
            Agent targetAgent = _detectedAgents[targetAgentUnitID];

            if (targetAgent != null && targetAgent != _targetAgent && IsInstanceValid(targetAgent))
            {
                _targetAgent = targetAgent;
                EmitSignal(nameof(TargetAgentChangeSignal));
            }
        }
    }

    public void GetPreviousTargetAgent()
    {
        // If current there is no target to lookup, just get next agent
        if (_targetAgent == null || !IsInstanceValid(_targetAgent))
        {
            if (_targetAgents.Count > 0)
            {
                GetNextTargetAgent();
            }
            return;
        }
        else if (_targetAgent != null && _targetAgents.Count == 1)
        {
            // No other agent can be selected now, directly return
            return;
        }

        Godot.Collections.Array<String> removeTargetList = new Godot.Collections.Array<String>();

        int currentIndex = _targetAgents.IndexOf(_targetAgent.GetUnitID());

        // If existing agent is no longer exist, just look next agent
        if(currentIndex < 0)
        {
            GetNextTargetAgent();
            return;
        }

        int previousIndex = currentIndex;

        Agent targetAgent = null;

        // Look up previous agent until a vaild one exist
        do
        {
            previousIndex--;

            // Circular back the index to last item in case it is move behind 0 in left direction
            if(previousIndex == -1)
            {
                previousIndex = _targetAgents.Count - 1;
            }

            targetAgent = _detectedAgents[_targetAgents[previousIndex]];

            if (targetAgent != null && IsInstanceValid(targetAgent))
            {
                // Identify the agent is different from current agent
                if (_targetAgent != null && targetAgent.GetUnitID() != _targetAgent.GetUnitID())
                {
                    break;
                }
            }
            else
            {
                // Remove this target from list as it is no longer valid
                removeTargetList.Add(_targetAgents[previousIndex]);
            }

        }while(previousIndex != currentIndex);

        // Clean up the list
        foreach (String targetAgentUnitID in removeTargetList)
        {
            _targetAgents.Remove(targetAgentUnitID);
            _detectedAgents.Remove(targetAgentUnitID);
        }

        // Identify a new target, so assign it
        if (targetAgent != null)
        {
            _targetAgent = targetAgent;
            EmitSignal(nameof(TargetAgentChangeSignal));
        }
    }

    public void GetNextTargetAgent()
    {
        Agent targetAgent = null;

        Godot.Collections.Array<String> removeTargetList = new Godot.Collections.Array<String>();

        foreach (String targetAgentUnitID in _targetAgents)
        {
            targetAgent = _detectedAgents[targetAgentUnitID];

            if (targetAgent != null && IsInstanceValid(targetAgent))
            {
                // Identify the agent is different from current agent
                if (_targetAgent != null && targetAgent.GetUnitID() != _targetAgent.GetUnitID())
                {
                    break;
                }
            }
            else
            {
                targetAgent = null;
                // Remove this target from list as it is no longer valid
                removeTargetList.Add(targetAgentUnitID);
            }
        }

        // Clean up the list
        foreach (String targetAgentUnitID in removeTargetList)
        {
            _targetAgents.Remove(targetAgentUnitID);
            _detectedAgents.Remove(targetAgentUnitID);
        }

        // Identify a new target, so assign it
        if (targetAgent != null)
        {
            _targetAgent = targetAgent;
            EmitSignal(nameof(TargetAgentChangeSignal));
        }
    }


}
