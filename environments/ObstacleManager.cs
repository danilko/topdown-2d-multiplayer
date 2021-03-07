using Godot;
using System;

public class ObstacleManager : Node2D
{
    TileMap _tileMap;

    private Godot.Collections.Dictionary<String, Vector2> _obstacles = new Godot.Collections.Dictionary<String, Vector2>();
    private Godot.Collections.Array _obstaclesDestroyed = new Godot.Collections.Array();

    private Vector2 _halfCellSize;

    private Godot.Collections.Array _traverableTiles;

    private String _obstaclePrefix = "obstacle_";
    private String _obstacleIndexSeparator = "_";

    public override void _Ready()
    {
        _traverableTiles = new Godot.Collections.Array();
    }

    public Godot.Collections.Array GetObstaclesDestroyed()
    {
        return _obstaclesDestroyed;
    }

    public Godot.Collections.Dictionary<String, Vector2> GetObstacles()
    {
        return _obstacles;
    }

    // Build obstacles base on tile map
    // Will not build obstacles on the road automatically
    public void Initialize(TileMap tileMap)
    {
        _tileMap = tileMap;
        _halfCellSize = _tileMap.CellSize / 2;
        buildObstacles();

    }

    public Godot.Collections.Array GetTraversableTiles()
    {
        return _traverableTiles;
    }

    private void buildObstacles()
    {
        Rect2 usedRect = _tileMap.GetUsedRect();

        int startPointX = (int)usedRect.Position.x;
        int startPointY = (int)usedRect.Position.y;

        int maxLengthX = (int)usedRect.Size.x;
        int maxLengthY = (int)usedRect.Size.y;

        Godot.Collections.Dictionary prebuildObstacles = new Godot.Collections.Dictionary();

        if (GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
        {
            // Add pre - added obstacles
            foreach (Node2D obstacle in GetChildren())
            {
                Vector2 pos = _tileMap.WorldToMap(obstacle.Position);
                prebuildObstacles.Add(pos.x + _obstacleIndexSeparator + pos.y, pos);

                float x = pos.x;
                float y = pos.y;
                prebuildObstacles.Add(x + _obstacleIndexSeparator + y, new Vector2(x, y));
            }
        }

        for (int xIndex = startPointX; xIndex < maxLengthX; xIndex++)
        {
            for (int yIndex = startPointY; yIndex < maxLengthY; yIndex++)
            {

                // if there is already obstacle on it, then ignore this tile, this is also not workable tile, so skip entire logic to next tile
                if (prebuildObstacles.Contains(xIndex + _obstacleIndexSeparator + yIndex))
                {
                    continue;
                }

                Vector2 position;
                Obstacle.Items item = Obstacle.Items.remain;

                if (_tileMap.GetCell(xIndex, yIndex) == 0)
                {
                    item = Obstacle.Items.crate_wood;
                }
                else if (_tileMap.GetCell(xIndex, yIndex) == 45 || _tileMap.GetCell(xIndex, yIndex) == 46)
                {
                    item = Obstacle.Items.crate_steel;
                }
                else if (_tileMap.GetCell(xIndex, yIndex) == 7)
                {
                    item = Obstacle.Items.roadblock_red;
                }

                Label mapLabel = (Label)GetNode("../MapCoordinate").Duplicate();
                mapLabel.Text = ("(" + xIndex + "," + yIndex + ")");
                mapLabel.Name = "maplabel_" + xIndex + _obstacleIndexSeparator + yIndex;

                position = _tileMap.MapToWorld(new Vector2(xIndex, yIndex));

                if (item != Obstacle.Items.remain)
                {

                    Obstacle obstacle = (Obstacle)((PackedScene)GD.Load("res://environments/Obstacle.tscn")).Instance();
                    obstacle.type = item;

                    obstacle.Name = _obstaclePrefix + xIndex + _obstacleIndexSeparator + yIndex;

                    AddChild(obstacle);

                    obstacle.GlobalPosition = position + _halfCellSize;

                    mapLabel.Set("custom_colors/font_color", new Color("#ff0000"));
                }
                else
                {
                    if (GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
                    {
                        // As there is no obstacle, this cell is a workable path
                        _traverableTiles.Add(new Vector2(xIndex, yIndex));
                    }

                    mapLabel.Set("custom_colors/font_color", new Color("#0016ff"));
                }

                mapLabel.SetGlobalPosition(position + _halfCellSize);

                AddChild(mapLabel);
            }
        }

        if (GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
        {
            _buildObstaclesCache();
        }
    }

    private void _buildObstaclesCache()
    {
        foreach (Node node in GetChildren())
        {
            if(! node.HasMethod(nameof(Obstacle.TakeEnvironmentDamage)))
            {
                continue;
            }

            Obstacle obstacle = (Obstacle)node;

            Vector2 tileMapPosition = _tileMap.WorldToMap(obstacle.Position);

            float xIndex = tileMapPosition.x;
            float yIndex = tileMapPosition.y;

            if (!obstacle.IsConnected(nameof(Obstacle.ObstacleDestroySignal), this, nameof(_onObstacleDestroy)))
            {
                obstacle.Connect(nameof(Obstacle.ObstacleDestroySignal), this, nameof(_onObstacleDestroy));
            }

            _obstacles.Add(_obstaclePrefix + xIndex + _obstacleIndexSeparator + yIndex, obstacle.Position);
        }
    }
    private void _onObstacleDestroy(String obstacleName)
    {
        if (GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
        {
            if (_obstacles.ContainsKey(obstacleName))
            {
                _obstacles.Remove(obstacleName);
            }

            _obstaclesDestroyed.Add(obstacleName);

            destroyObstacle(obstacleName);

            Rpc(nameof(destroyObstacle), obstacleName);
        }

    }

    public bool CheckForObstacles(Vector2 input)
    {
        Vector2 tileMapPosition = _tileMap.WorldToMap(input);

        float xIndex = tileMapPosition.x;
        float yIndex = tileMapPosition.y;

        return _obstacles.ContainsKey(_obstaclePrefix + xIndex + _obstacleIndexSeparator + yIndex);
    }

    public void syncObstacles(int netId)
    {
        // Sync the destoryed obstacles
        foreach (String obstacle in _obstaclesDestroyed)
        {
            RpcId(netId, nameof(destroyObstacle), obstacle);
        }
    }

    [Remote]
    private void destroyObstacle(String obstacleName)
    {
        if (this.HasNode(obstacleName))
        {
            Obstacle obstacle = (Obstacle)this.GetNode(obstacleName);
            if (IsInstanceValid(obstacle))
            {
                obstacle.explode();

                if (GetTree().NetworkPeer == null || GetTree().IsNetworkServer())
                {
                    // As there is no obstacle now, this cell is a workable path
                    _traverableTiles.Add(new Vector2(float.Parse(obstacleName.Split(_obstacleIndexSeparator)[1]), float.Parse(obstacleName.Split(_obstacleIndexSeparator)[2])));
                }
            }
        }
    }

}