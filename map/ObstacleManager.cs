using Godot;
using System;

public class ObstacleManager : Node2D
{
    AgentAStar _aStar;
    TileMap _tileMap;

    private Godot.Collections.Dictionary<String, Vector2> _obstacles = new Godot.Collections.Dictionary<String, Vector2>();
    private Godot.Collections.Array _obstaclesDestroyed = new Godot.Collections.Array();

    public override void _Ready()
    {

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
    public void Initialize(AgentAStar aStar, TileMap tileMap)
    {
        _aStar = aStar;
        _tileMap = tileMap;

        // buildObstacles();

    }

    private void buildObstacles()
    {

        Vector2 cellSize = _tileMap.CellSize;
        Rect2 usedRect = _tileMap.GetUsedRect();

        int startPointX = (int)usedRect.Position.x;
        int startPointY = (int)usedRect.Position.y;

        int maxLengthX = (int)usedRect.Size.x;
        int maxLengthY = (int)usedRect.Size.y;

        Godot.Collections.Dictionary prebuildObstacles = new Godot.Collections.Dictionary();

        if (GetTree().IsNetworkServer())
        {
            // Add pre - added obstacles
            foreach (Node2D obstacle in GetNode("Obstacles").GetChildren())
            {
                Vector2 pos = _tileMap.WorldToMap(obstacle.Position);
                prebuildObstacles.Add(pos.x + "+" + pos.y, pos);

                float x = pos.x - 2;
                float y = pos.y - 2;
                prebuildObstacles.Add(x + "+" + y, new Vector2(x, y));

                x = pos.x;
                y = pos.y - 2;
                prebuildObstacles.Add(x + "+" + y, new Vector2(x, y));

                x = pos.x - 2;
                y = pos.y;
                prebuildObstacles.Add(x + "+" + y, new Vector2(x, y));

                x = pos.x + 2;
                y = pos.y + 2;
                prebuildObstacles.Add(x + "+" + y, new Vector2(x, y));

                x = pos.x;
                y = pos.y + 2;
                prebuildObstacles.Add(x + "+" + y, new Vector2(x, y));

                x = pos.x + 2;
                y = pos.y;
                prebuildObstacles.Add(x + "+" + y, new Vector2(x, y));
            }
        }

        // As the grid use in this game is 2 x 2 of a normal godot grid, so need to increment by 2
        for (int xIndex = startPointX; xIndex < maxLengthX; xIndex = xIndex + 2)
        {
            for (int yIndex = startPointY; yIndex < maxLengthY; yIndex = yIndex + 2)
            {

                // if there is already obstacle on it, then ignore this tile, this is also not workable tile, so skip entire logic to next tile
                if (prebuildObstacles.Contains(xIndex + "+" + yIndex))
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

                Label mapLabel = (Label)GetNode("MapCoordinate").Duplicate();
                mapLabel.Text = ("(" + xIndex + "," + yIndex + ")");
                mapLabel.Name = "maplabel_" + xIndex + "_" + yIndex;

                position = _tileMap.MapToWorld(new Vector2(xIndex, yIndex));

                if (item != Obstacle.Items.remain)
                {

                    Obstacle obstacle = (Obstacle)((PackedScene)GD.Load("res://environments/Obstacle.tscn")).Instance();
                    obstacle.type = item;

                    obstacle.Name = "obstacle_" + xIndex + "_" + yIndex;

                    GetNode("Obstacles").AddChild(obstacle);

                    obstacle.GlobalPosition = position + cellSize;

                    mapLabel.Set("custom_colors/font_color", new Color("#ff0000"));
                }
                else
                {
                    if (GetTree().IsNetworkServer())
                    {
                        // As there is no obstacle, this cell is a workable path
                        _aStar.addCell(new Vector2(xIndex, yIndex));
                    }

                    mapLabel.Set("custom_colors/font_color", new Color("#0016ff"));
                }

                mapLabel.SetGlobalPosition(position + cellSize);

                this.AddChild(mapLabel);
            }
        }

        if (GetTree().IsNetworkServer())
        {
            _aStar.connectPoints();
            _buildObstaclesCache();
        }
    }

    private void _buildObstaclesCache()
    {
        TileMap tilemap = (TileMap)GetNode("Ground");
        foreach (Node node in GetNode("Obstacles").GetChildren())
        {
            Obstacle obstacle = (Obstacle)node;

            Vector2 tileMapPosition = tilemap.WorldToMap(obstacle.Position);

            float xIndex = tileMapPosition.x;
            float yIndex = tileMapPosition.y;

            // Align to the cell deployment (2 x 2 index size)
            // Need to do (xIndex / 2) to get how many 2 x 2 does it cross
            // Then add up
            xIndex = (xIndex / 2) * 2;
            yIndex = (yIndex / 2) * 2;

            if (!obstacle.IsConnected("ObstacleDestroy", this, nameof(_onObstacleDestroy)))
            {
                obstacle.Connect("ObstacleDestroy", this, nameof(_onObstacleDestroy));
            }
            _obstacles.Add("obstacle_" + xIndex + "_" + yIndex, obstacle.Position);
        }
    }
    private void _onObstacleDestroy(String obstacleName)
    {
        if (GetTree().IsNetworkServer())
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

        // Align to the cell deployment (2 x 2 index size)
        // Need to do (xIndex / 2) to get how many 2 x 2 does it cross
        // Then add up

        xIndex = (xIndex / 2) * 2;
        yIndex = (yIndex / 2) * 2;

        return _obstacles.ContainsKey("obstacle_" + xIndex + "_" + yIndex);
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

                // Restablish aStar
                //if (GetTree().IsNetworkServer())
                //{
                //    _aStar.addCell(new Vector2(float.Parse(obstacleName.Split("_")[1]), float.Parse(obstacleName.Split("_")[2])));
                //    _aStar.connectPoints();
                //}
            }
        }
    }

}