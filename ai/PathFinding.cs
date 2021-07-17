using Godot;
using System;

public class PathFinding : Node2D
{
    private Godot.Collections.Dictionary _tilestoWorld;
    private Vector2 _halfCellSize;

    private TileMap _tileMap;

    private Rect2 used_rect;

    private AStar2D _aStar;

    private ObstacleManager _obstacleManager;

    private Node2D _grid;
    private Color _enableColor = new Color("c9dffb");
    private Color _disableColor = new Color("fbc9c9");

    private Godot.Collections.Dictionary<int, ColorRect> _gridRects;

    private Timer _updateTraversableTilesTimer;

    [Export]
    private int UpdateTRaversableTilesTime = 10;

    // Debug flag
    private bool debug = false;

    public override void _Ready()
    {
        _aStar = new AStar2D();
        _grid = (Node2D)GetNode("Grid");

        if (!debug)
        {
            _grid.Visible = false;
        }

        _tilestoWorld = new Godot.Collections.Dictionary();
        _gridRects = new Godot.Collections.Dictionary<int, ColorRect>();

        _updateTraversableTilesTimer = (Timer)GetNode("UpdateTraversableTilesTimer");
        _updateTraversableTilesTimer.WaitTime = UpdateTRaversableTilesTime;
    }

    public void Initialize(TileMap tileMap, ObstacleManager obstacleManager)
    {
        _tileMap = tileMap;
        _halfCellSize = tileMap.CellSize / 2;

        _obstacleManager = obstacleManager;

        UpdateTraversableTiles();
    }

    public void UpdateNavigationMap()
    {
        foreach (int pointID in _aStar.GetPoints())
        {
            _aStar.SetPointDisabled(pointID, false);
            _gridRects[pointID].Color = _enableColor;
        }

        Godot.Collections.Array obstacles = GetTree().GetNodesInGroup("Obstacles");

        foreach (Node2D obstacle in obstacles)
        {
            if (obstacle is TileMap)
            {
                TileMap tileMap = (TileMap)obstacle;
                foreach (Vector2 tile in tileMap.GetUsedCells())
                {
                    int tileID = getPointID((int)tile.x, (int)tile.y);
                    if (_aStar.HasPoint(tileID))
                    {
                        _aStar.SetPointDisabled(tileID, true);
                        _gridRects[tileID].Color = _disableColor;
                    }
                }
            }
            else if (obstacle is Agent)
            {
                Vector2 tile = _tileMap.WorldToMap(obstacle.GlobalPosition);
                int tileID = getPointID((int)tile.x, (int)tile.y);
                if (_aStar.HasPoint(tileID))
                {
                    _aStar.SetPointDisabled(tileID, true);
                    _gridRects[tileID].Color = _disableColor;
                }
            }
        }
    }

    public void UpdateTraversableTiles()
    {
        _updateTraversableTiles(_obstacleManager.GetTraversableTiles());

        _updateTraversableTilesTimer.Start();
    }

    private void _updateTraversableTiles(Godot.Collections.Array tiles)
    {
        // No need to update as tiles not change
        if (_tilestoWorld.Count == tiles.Count)
        {
            return;
        }

        _addTraversableTiles(tiles);
        _connectTraversableTiles(tiles);
    }

    public void _addTraversableTiles(Godot.Collections.Array tiles)
    {
        foreach (Vector2 tile in tiles)
        {
            int id = getPointID((int)tile.x, (int)tile.y);

            if (!_aStar.HasPoint(id))
            {
                _aStar.AddPoint(id, tile, 1);
                _tilestoWorld.Add(id, _tileMap.MapToWorld(tile) + _halfCellSize);

                ColorRect colorRect = new ColorRect();
                _grid.AddChild(colorRect);

                colorRect.Color = _enableColor;
                colorRect.Modulate = new Color(1, 1, 1, 0.5f);

                _gridRects.Add(id, colorRect);

                colorRect.MouseFilter = Control.MouseFilterEnum.Ignore;

                colorRect.SetSize(_tileMap.CellSize);
                // Color Rect's x calculation is lightly different, so need to add 1.5f to position correctly
                colorRect.RectPosition = new Vector2(_tileMap.MapToWorld(tile).x + (_tileMap.CellSize.x * 1.5f), _tileMap.MapToWorld(tile).y);

            }
        }

    }

    // Pair function to generate unique id from two numbers
    private int getPointID(int xIndex, int yIndex)
    {
        return (((xIndex + yIndex) * (xIndex + yIndex + 1)) / 2) + yIndex;
    }

    public Godot.Collections.Array GetPath(Vector2 source, Vector2 target)
    {
        Vector2 cellSource = _tileMap.WorldToMap(source);
        Vector2 cellTarget = _tileMap.WorldToMap(target);

        int sourceId = getPointID((int)cellSource.x, (int)cellSource.y);
        int targetId = getPointID((int)cellTarget.x, (int)cellTarget.y);

        Godot.Collections.Array worldPath = new Godot.Collections.Array();

        if (!_aStar.HasPoint(sourceId) || !_aStar.HasPoint(targetId))
        {
            return worldPath;
        }

        Vector2[] cellPath = _aStar.GetPointPath(sourceId, targetId);

        if (cellPath == null || cellPath.Length == 0)
        {
            return worldPath;
        }


        // Reverse adding the points
        for (int index = 0; index < cellPath.Length; index++)
        {
            int id = getPointID((int)cellPath[index].x, (int)cellPath[index].y);
            worldPath.Add(_tilestoWorld[id]);
        }

        return worldPath;
    }

    public override void _PhysicsProcess(float delta)
    {
        UpdateNavigationMap();
    }

    private void _connectTraversableTiles(Godot.Collections.Array tiles)
    {

        foreach (Vector2 tile in tiles)
        {
            // Add all neighbors
            for (int indexX = -1; indexX < 2; indexX++)
            {
                for (int indexY = -1; indexY < 2; indexY++)
                {
                    Vector2 targetTile = new Vector2(tile.x + indexX, tile.y + indexY);
                    int toId = getPointID((int)targetTile.x, (int)targetTile.y);
                    int fromId = getPointID((int)tile.x, (int)tile.y);

                    if (tile == targetTile || !_aStar.HasPoint(toId))
                    {
                        // No need to add tile as it is the same
                        continue;
                    }

                    // Caculate if diagonal point can be used 
                    // (this is to avoid to given a diagonal path to agent, but agent cannot pass as vertical/horizontal has obstacles)

                    // Upper left/right diagonal (-1,1), (1,1)
                    // which need (-1,0) (horizontal), (0,1) (veritcal), (1,0) (horizontal)

                    // Bottom left/right diagonal (-1,-1), (1,-1)
                    // which need (-1,0) (horizontal), (0,-1) (veritcal), (1,0) (horizontal)

                    // this is to avoid stuck on corner
                    if (indexX != 0 && indexY != 0)
                    {
                        int horizaontalId = getPointID((int)tile.x + indexX, (int)tile.y);
                        int verticalId = getPointID((int)tile.x, (int)tile.y + indexY);

                        // Upper diaonal connection depend on if there is obstacle for vertical/horizontal neighbors
                        // If there are obstacles (i.e. point not exist, then not connect it)
                        if (!_aStar.HasPoint(horizaontalId) || !_aStar.HasPoint(verticalId))
                        {
                            continue;
                        }
                    }

                    if (!_aStar.ArePointsConnected(fromId, toId))
                    {
                        // Debug code
                        if (debug)
                        {
                            Line2D line2d = new Line2D();
                            Vector2[] points = { (Vector2)_tilestoWorld[fromId], (Vector2)_tilestoWorld[toId] };
                            line2d.Points = points;
                            _tileMap.AddChild(line2d);
                        }
                        _aStar.ConnectPoints(fromId, toId, true);
                    }
                }
            }
        }
    }
}
