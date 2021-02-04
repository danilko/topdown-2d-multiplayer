using Godot;
using System;

public class MiniMap : MarginContainer
{
    private float _zoom = 1.5f;

    private Agent _player;
    private Godot.Collections.Dictionary<String, Agent> _agents;

    private Godot.Collections.Dictionary<String, Sprite> _agentMarkers;
    private Godot.Collections.Dictionary<String, Sprite> _baseMarkers;

    private Sprite _agentMarker;
    private Sprite _baseMarker;
    private TextureRect _grid;
    private Vector2 _gridScale = Vector2.Zero;

    private CapaturableBaseManager _capaturableBaseManager;

    public override void _Ready()
    {
        _grid = (TextureRect)GetNode("MarginContainer/Grid");

        _agentMarker = (Sprite)_grid.GetNode("AgentMarker");
        _baseMarker = (Sprite)_grid.GetNode("BaseMarker");
        _agents = new Godot.Collections.Dictionary<String, Agent>();
        _agentMarkers = new Godot.Collections.Dictionary<String, Sprite>();
        _baseMarkers = new Godot.Collections.Dictionary<String, Sprite>();

        _player = null;
        _capaturableBaseManager = null;
        _gridScale = _grid.RectSize / (GetViewportRect().Size * _zoom);
    }

    public void Iniitialize(CapaturableBaseManager capaturableBaseManager)
    {
        _capaturableBaseManager = capaturableBaseManager;

        foreach (CapturableBase capturableBase in capaturableBaseManager.GetBases())
        {
            Sprite baseMarker = (Sprite)_baseMarker.Duplicate();
            baseMarker.Name = capturableBase.Name + "_marker";
            baseMarker.SelfModulate = Team.TeamColor[(int)capturableBase.GetCaptureBaseTeam()];
            baseMarker.Show();

            _grid.AddChild(baseMarker);

            // Add marker to dictionary
            _baseMarkers.Add(capturableBase.Name, baseMarker);
        }
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
            Sprite agentMarker = (Sprite)_agentMarker.Duplicate();
            agentMarker.Name = agent.GetUnitName() + "_marker";

            agentMarker.Modulate = Team.TeamColor[(int)agent.GetCurrentTeam()];

            _grid.AddChild(agentMarker);

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

            Sprite agentMarker = _agentMarkers[agent.GetUnitName()];

            // Add marker to dictionary
            _agentMarkers.Remove(agent.GetUnitName());

            agentMarker.QueueFree();
        }
    }


    public override void _Process(float delta)
    {
        if (_player == null || !IsInstanceValid(_player))
        {
            return;
        }

        _agentMarker.GlobalRotation = _player.GlobalRotation + Mathf.Pi / 2.0f;
        _agentMarker.Position = _grid.RectSize / 2.0f;

        foreach (Agent agent in _agents.Values)
        {
            if (IsInstanceValid(_player))
            {

                Sprite agentMarker = _agentMarkers[agent.GetUnitName()];
                agentMarker.GlobalRotation = agent.GlobalRotation + Mathf.Pi / 2.0f;

                Vector2 markerPosition = ((agent.GlobalPosition - _player.GlobalPosition) * _gridScale) + _grid.RectSize / 2.0f;
                markerPosition.x = Mathf.Clamp(markerPosition.x, 0, _grid.RectSize.x);
                markerPosition.y = Mathf.Clamp(markerPosition.y, 0, _grid.RectSize.y);

                // Update marker
                _agentMarkers[agent.GetUnitName()].Position = markerPosition;
            }
        }


        foreach (CapturableBase capturableBase in _capaturableBaseManager.GetBases())
        {
            Vector2 markerPosition = ((capturableBase.GlobalPosition - _player.GlobalPosition) * _gridScale) + _grid.RectSize / 2.0f;
            markerPosition.x = Mathf.Clamp(markerPosition.x, 0, _grid.RectSize.x);
            markerPosition.y = Mathf.Clamp(markerPosition.y, 0, _grid.RectSize.y);

            // Update marker
            _baseMarkers[capturableBase.Name].Position = markerPosition;
            _baseMarkers[capturableBase.Name].SelfModulate = Team.TeamColor[(int)capturableBase.GetCaptureBaseTeam()];
        }
    }
}
