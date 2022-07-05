using Godot;
using System;
using System.Collections.Generic;

public class ScreenIndicator : Node2D
{

    private float _zoom = 1.5f;

    private Agent _player;
    private Dictionary<String, Agent> _agents;

    private Dictionary<String, Node2D> _agentMarkers;

    private Node2D _agentMarker;

    private Agent _agent = null;

    private TextureProgress _healthBar;

    private Node2D _healthPanel;


    private Node2D _weaponPanel;

    private Node2D _leftWeaponNote;

    private Node2D _rightWeaponNote;

    private Tween _tween;


    private Agent _currentTargetAgnet;

    private TextureRect _lockonIndicator;


    public void SetActivate(Boolean activate)
    {
        this.Visible = activate;
        _lockonIndicator.Visible = false;
    }


    public override void _Ready()
    {
        _agentMarker = (Node2D)GetNode("AgentMarker");
        _healthPanel = (Node2D)GetNode("HealthPanel");
        _healthBar = (TextureProgress)_healthPanel.GetNode("HealthBar");

        _weaponPanel = (Node2D)GetNode("WeaponPanel");
        _leftWeaponNote = (Node2D)_weaponPanel.GetNode("LeftWeaponNote");
        _rightWeaponNote = (Node2D)_weaponPanel.GetNode("RightWeaponNote");

        _tween = (Tween)GetNode("Tween");

        _currentTargetAgnet = null;
    }

    public void Initialize(GameWorld gameWorld, Agent agent)
    {
        _agent = agent;
        _agents = new Dictionary<String, Agent>();
        _agentMarkers = new Dictionary<String, Node2D>();

        _lockonIndicator = (TextureRect)gameWorld.GetHUD().GetNode("GameControl/LockOnIndicator");
    }

    public void setCurrentTargetAgent(Agent agent)
    {
        _currentTargetAgnet = agent;
    }

    public void AddAgent(Agent agent)
    {
        if (!_agents.ContainsKey(agent.GetUnitID()))
        {
            Node2D agentMarker = (Node2D)_agentMarker.Duplicate();
            agentMarker.Name = agent.GetUnitID() + "_marker";

            agentMarker.Modulate = Team.TeamColor[(int)agent.GetTeam()];

            AddChild(agentMarker);

            agentMarker.Show();

            // Add agent to dictonary
            _agents.Add(agent.GetUnitID(), agent);

            // Add marker to dictionary
            _agentMarkers.Add(agent.GetUnitID(), agentMarker);
        }
    }

    public void RemoveAgent(Agent agent)
    {
        _removeAgent(agent.GetUnitID());
    }

    private void _removeAgent(String agentUnitID)
    {
        if (_agents.ContainsKey(agentUnitID))
        {
            // Add agent to dictonary
            _agents.Remove(agentUnitID);

            Node2D agentMarker = _agentMarkers[agentUnitID];

            // Add marker to dictionary
            _agentMarkers.Remove(agentUnitID);

            agentMarker.QueueFree();
        }
    }

    public override void _Process(float delta)
    {
        if (_agent == null || !IsInstanceValid(_agent))
        {
            return;
        }

        if (_currentTargetAgnet != null && IsInstanceValid(_currentTargetAgnet))
        {
            _lockonIndicator.Visible = true;
            // set the label position to the position of agent
            _lockonIndicator.RectPosition = new Vector2(_currentTargetAgnet.GetGlobalTransformWithCanvas().origin.x - (_lockonIndicator.RectSize.x / 2), _currentTargetAgnet.GetGlobalTransformWithCanvas().origin.y - (_lockonIndicator.RectSize.y/ 2));

        }
        else
        {
            // Hide indicator if there is no target
            _lockonIndicator.Visible = false;
        }

        Godot.Collections.Array<String> removeTargetList = new Godot.Collections.Array<String>();


        foreach (String agentUnitID in _agents.Keys)
        {
            Agent agent = _agents[agentUnitID];
            if (agent != null && IsInstanceValid(agent))
            {
                Node2D agentMarker = _agentMarkers[agent.GetUnitID()];
                agentMarker.GlobalRotation = agent.GlobalRotation + Mathf.Pi / 2.0f;

                // Update marker
                agentMarker.LookAt(agent.GlobalPosition);
                float distance = agentMarker.GlobalPosition.DistanceTo(agent.GlobalPosition);
                ((Label)agentMarker.GetNode("Text")).Text = agent.GetUnitID() + " " + distance;
            }
            else
            {
                removeTargetList.Add(agentUnitID);
            }
        }

        // Clean up the list
        foreach (String targetAgentUnitID in removeTargetList)
        {
            _removeAgent(targetAgentUnitID);
        }

        _healthPanel.GlobalRotation = 0;
        _leftWeaponNote.GlobalRotation = 0;
        _rightWeaponNote.GlobalRotation = 0;
    }

    public void UpdateHealth(int value)
    {

        _tween.InterpolateProperty(_healthBar, "value", _healthBar.Value,
        value, 0.2f,
        Tween.TransitionType.Linear, Tween.EaseType.InOut);
        _tween.Start();
        ((Label)_healthPanel.GetNode("HealthText")).Text = value + "%";
    }

    public void UpdateWeaponAmmo(int current, int max, Weapon.WeaponOrder weaponOrder)
    {

        ((Label)_weaponPanel.GetNode(weaponOrder + "WeaponNote/WeaponText")).Text = current + "/" + max;

        TextureProgress textureProgress = (TextureProgress)_weaponPanel.GetNode(weaponOrder + "WeaponBar");
        textureProgress.MaxValue = max;
        textureProgress.Value = current;
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
