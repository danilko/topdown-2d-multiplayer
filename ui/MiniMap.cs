using Godot;
using System;
using System.Collections.Generic;

public class MiniMap : MarginContainer
{
    public enum MapMode
    {
        MINIMAP,
        BIGMAP
    }

    private float _zoom = 1.5f;

    private Agent _player;
    private Dictionary<String, Agent> _agents;

    private Dictionary<String, Sprite> _agentMarkers;
    private Dictionary<String, Sprite> _baseMarkers;

    private Sprite _agentMarker;
    private Sprite _baseMarker;
    private TextureRect _grid;
    private Vector2 _gridScale = Vector2.Zero;

    private CapturableBaseManager _capturableBaseManager;

    private MapMode _mapMode;

    private Vector2 _mapCorner;

    private float _mapScale;

    private TextureRect _map;
    private ColorRect _background;
    private Vector2 _mapLength;

    public override void _Ready()
    {
        _grid = (TextureRect)GetNode("MarginContainer/Grid");

        _agentMarker = (Sprite)_grid.GetNode("AgentMarker");
        _baseMarker = (Sprite)_grid.GetNode("BaseMarker");

        _map = (TextureRect)GetNode("MarginContainer/Map");
        _background = (ColorRect)GetNode("MarginContainer/Background");

        _agents = new Dictionary<String, Agent>();
        _agentMarkers = new Dictionary<String, Sprite>();
        _baseMarkers = new Dictionary<String, Sprite>();

        _player = null;
        _capturableBaseManager = null;
        _gridScale = _grid.RectSize / (GetViewportRect().Size * _zoom);

        SetMapMode(MapMode.MINIMAP);

        _mapLength = Vector2.Zero;
    }

    public void Iniitialize(GameWorld gameWorld)
    {
        _capturableBaseManager = gameWorld.GetCapturableBaseManager();

        foreach (CapturableBase capturableBase in _capturableBaseManager.GetCapturableBases())
        {
            Sprite baseMarker = (Sprite)_baseMarker.Duplicate();
            baseMarker.Name = capturableBase.Name + "_marker";
            baseMarker.SelfModulate = Team.TeamColor[(int)capturableBase.GetCaptureBaseTeam()];
            baseMarker.Show();

            _grid.AddChild(baseMarker);

            // Add marker to dictionary
            _baseMarkers.Add(capturableBase.Name, baseMarker);
        }

        TileMap tileMap = (TileMap)gameWorld.GetNode("Obstacles");
        Rect2 tileMapCellBounds = tileMap.GetUsedRect();

        // Get the corner in global position
        // Need to use cell bounds (tilemap index) * tileMapSize to scale to real world position
        _mapCorner = new Vector2(tileMapCellBounds.Position.x * tileMap.CellSize.x, tileMapCellBounds.Position.y * tileMap.CellSize.y);

        _mapLength = new Vector2(tileMapCellBounds.Size.x * tileMap.CellSize.x, tileMapCellBounds.Size.y * tileMap.CellSize.y);
    }

    public void SetMapMode(MapMode mapMode)
    {
        _mapMode = mapMode;

        if (_mapMode == MapMode.BIGMAP)
        {
            _map.Show();
            _background.Hide();
        }
        else
        {
            _map.Hide();
            _background.Show();
        }
    }

    public MapMode GetMapMode(MapMode mapMode)
    {
        return _mapMode;
    }

    public void SetPlayer(Agent agent)
    {
        _player = agent;
        _agentMarker.Show();
    }

    public void RemovePlayer()
    {
        _player = null;
        _agentMarker.Hide();
    }

    public void AddAgent(Agent agent)
    {
        if (!_agents.ContainsKey(agent.GetUnitID()))
        {
            Sprite agentMarker = (Sprite)_agentMarker.Duplicate();
            agentMarker.Name = agent.GetUnitID() + "_marker";

            agentMarker.Modulate = Team.TeamColor[(int)agent.GetTeam()];

            _grid.AddChild(agentMarker);

            // Add agent to dictonary
            _agents.Add(agent.GetUnitID(), agent);

            // Add marker to dictionary
            _agentMarkers.Add(agent.GetUnitID(), agentMarker);
        }
    }

    public void RemoveAgent(String unitName)
    {
        if (_agents.ContainsKey(unitName))
        {
            // Remove agent to dictonary
            _agents.Remove(unitName);

            Sprite agentMarker = _agentMarkers[unitName];

            // Remove marker to dictionary
            _agentMarkers.Remove(unitName);

            agentMarker.QueueFree();
        }
    }

    private void _updateBigMap()
    {
        if (_mapLength == Vector2.Zero)
        {
            return;
        }

        // Calculate the scale
        float mapScaleX = _map.GetRect().Size.x / _mapLength.x;
        float mapScaleY = _map.GetRect().Size.y / _mapLength.y;

        if (!(_player == null || !IsInstanceValid(_player)))
        {
            Vector2 markerPosition = new Vector2(((_player.GlobalPosition.x - _mapCorner.x) * mapScaleX),
            ((_player.GlobalPosition.y - _mapCorner.y) * mapScaleY));

            _agentMarker.Position = markerPosition;
            _agentMarker.Rotation = _player.GlobalRotation + Mathf.Pi / 2.0f;

        }

        foreach (Agent agent in _agents.Values)
        {
            if (agent == null || !IsInstanceValid(agent))
            {
                return;
            }

            Sprite agentMarker = _agentMarkers[agent.GetUnitID()];

            if (agentMarker != null && IsInstanceValid(agentMarker))
            {
                agentMarker.GlobalRotation = agent.GlobalRotation + Mathf.Pi / 2.0f;

                Vector2 markerPosition = new Vector2(((agent.GlobalPosition.x - _mapCorner.x) * mapScaleX),
                ((agent.GlobalPosition.y - _mapCorner.y) * mapScaleY));

                // Update marker
                _agentMarkers[agent.GetUnitID()].Position = markerPosition;
            }
        }

        foreach (CapturableBase capturableBase in _capturableBaseManager.GetCapturableBases())
        {
            Vector2 markerPosition = new Vector2(((capturableBase.GlobalPosition.x - _mapCorner.x) * mapScaleX),
            ((capturableBase.GlobalPosition.y - _mapCorner.y) * mapScaleY));

            // Update marker
            _baseMarkers[capturableBase.Name].Position = markerPosition;
            _baseMarkers[capturableBase.Name].SelfModulate = Team.TeamColor[(int)capturableBase.GetCaptureBaseTeam()];
        }
    }

    private void _updateMiniMap()
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

                if (agent == null || !IsInstanceValid(agent))
                {
                    return;
                }

                Sprite agentMarker = _agentMarkers[agent.GetUnitID()];
                if (agentMarker != null && IsInstanceValid(agentMarker))
                {
                    agentMarker.GlobalRotation = agent.GlobalRotation + Mathf.Pi / 2.0f;

                    Vector2 markerPosition = ((agent.GlobalPosition - _player.GlobalPosition) * _gridScale) + _grid.RectSize / 2.0f;
                    markerPosition.x = Mathf.Clamp(markerPosition.x, 0, _grid.RectSize.x);
                    markerPosition.y = Mathf.Clamp(markerPosition.y, 0, _grid.RectSize.y);

                    // Update marker
                    _agentMarkers[agent.GetUnitID()].Position = markerPosition;
                }
            }
        }


        foreach (CapturableBase capturableBase in _capturableBaseManager.GetCapturableBases())
        {
            Vector2 markerPosition = ((capturableBase.GlobalPosition - _player.GlobalPosition) * _gridScale) + _grid.RectSize / 2.0f;
            markerPosition.x = Mathf.Clamp(markerPosition.x, 0, _grid.RectSize.x);
            markerPosition.y = Mathf.Clamp(markerPosition.y, 0, _grid.RectSize.y);

            // Update marker
            _baseMarkers[capturableBase.Name].Position = markerPosition;
            _baseMarkers[capturableBase.Name].SelfModulate = Team.TeamColor[(int)capturableBase.GetCaptureBaseTeam()];
        }
    }

    public override void _Process(float delta)
    {
        if (_mapMode == MapMode.MINIMAP)
        {
            _updateMiniMap();
        }

        if (_mapMode == MapMode.BIGMAP)
        {
            _updateBigMap();
        }
    }
}
