using Godot;
using System;
using System.Collections.Generic;

public class PathFinding : Node2D
{
    public class PathRequest
    {
        public PathRequest(Agent agent, Vector2 source, Vector2 target)
        {
            Agent = agent;
            Source = source;
            Target = target;
        }

        public Agent Agent { get; set; }
        public Vector2 Source { get; set; }
        public Vector2 Target { get; set; }
    }

    private Dictionary<int, Vector2> _tilestoWorld;
    private Vector2 _halfCellSize;

    private TileMap _tileMap;

    private AStar2D _aStar;

    private ObstacleManager _obstacleManager;

    private Node2D _grid;
    private Color _enableColor = new Color("c9dffb");
    private Color _disableColor = new Color("fbc9c9");

    private Dictionary<int, ColorRect> _gridRects;

    private Timer _updateTraversableTilesTimer;
    private Timer _updatePathRequestTimer;

    [Export]
    private float UpdateTraversableTilesTime = 10.0f;

    [Export]
    private float UpdatePathRequestTime = 0.01f;

    private List<PathRequest> _pathRequestList;

    // Test count, 3 seem to be more stable with 40 AI
    private int _processedPathRequestsPerCheck = 3;

    // Debug flag
    private bool debug = false;

    private GameWorld _gameWorld;

    public override void _Ready()
    {
        _aStar = new AStar2D();
        _grid = (Node2D)GetNode("Grid");

        if (!debug)
        {
            _grid.Visible = false;
        }

        _tilestoWorld = new Dictionary<int, Vector2>();
        _gridRects = new Dictionary<int, ColorRect>();

        _updateTraversableTilesTimer = (Timer)GetNode("UpdateTraversableTilesTimer");
        _updateTraversableTilesTimer.WaitTime = UpdateTraversableTilesTime;

        _updatePathRequestTimer = (Timer)GetNode("UpdatePathRequestTimer");
        _updatePathRequestTimer.WaitTime = UpdatePathRequestTime;

        _pathRequestList = new List<PathRequest>();
    }

    public void Initialize(GameWorld gameWorld, TileMap tileMap, ObstacleManager obstacleManager)
    {
        _gameWorld = gameWorld;

        _tileMap = tileMap;
        _halfCellSize = tileMap.CellSize / 2;

        _obstacleManager = obstacleManager;

        UpdateTraversableTiles();

        // Start the request check timer
        _updatePathRequestTimer.Start();
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
                    int tileID = _getPointID((int)tile.x, (int)tile.y);
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
                int tileID = _getPointID((int)tile.x, (int)tile.y);
                if (_aStar.HasPoint(tileID))
                {
                    //_aStar.SetPointDisabled(tileID, true);
                    // _gridRects[tileID].Color = _disableColor;
                }
            }
        }
    }

    public void UpdateTraversableTiles()
    {
        _updateTraversableTiles(_obstacleManager.GetTraversableTiles());

        _updateTraversableTilesTimer.Start();
    }

    private void _updateTraversableTiles(List<Vector2> tiles)
    {
        // No need to update as tiles not change
        if (_tilestoWorld.Count == tiles.Count)
        {
            return;
        }

        _addTraversableTiles(tiles);
        _connectTraversableTiles(tiles);
    }

    public void _addTraversableTiles(List<Vector2> tiles)
    {
        foreach (Vector2 tile in tiles)
        {

            int id = _getPointID((int)tile.x, (int)tile.y);

            // TEST ONLY BEGIN
            //Vector2 test = _extractVectorFromPointID(id);
            //if (!(test.x == (int)tile.x && test.y == (int)tile.y))
            //{
            //    GD.Print("error id = " + id + " with tileX " + (int)tile.x + " tileY " + (int)tile.y);
            //    GD.Print("error extract id = " + id + " with tileX " + (int)test.x + " tileY " + (int)test.y);
            //}
            // TEST ONLY END

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
    // https://gist.github.com/TheGreatRambler/048f4b38ca561e6566e0e0f6e71b7739 to cover negative number
    private int _getPointID(int xIndex, int yIndex)
    {
        int xx = xIndex >= 0 ? xIndex * 2 : (xIndex * -2) - 1;
        int yy = yIndex >= 0 ? yIndex * 2 : (yIndex * -2) - 1;

        return (xx >= yy) ? (xx * xx + xx + yy) : (yy * yy + xx);
    }

    private Vector2 _extractVectorFromPointID(int id)
    {
        int x = 0;
        int y = 0;
        int sqartz = Mathf.FloorToInt(Mathf.Sqrt(id));
        int sqz = sqartz * sqartz;
        if (((id - sqz)) >= sqartz)
        {
            x = sqartz;
            y = id - sqz - sqartz;
        }
        else
        {
            x = id - sqz;
            y = sqartz;
        }

        int xx = x % 2 == 0 ? x / 2 : (x + 1) / -2;
        int yy = y % 2 == 0 ? y / 2 : (y + 1) / -2;

        return new Vector2(xx, yy);
    }

    public void AddPathRequest(Agent agent, Vector2 source, Vector2 target)
    {
        _cleanUpPathRequestList();
        // Remove any previous request from agent
        _pathRequestList.RemoveAll(pathRequest => pathRequest.Agent.GetUnitID() == agent.GetUnitID());

        PathRequest newPathRequest = new PathRequest(agent, source, target);

        _pathRequestList.Add(newPathRequest);
    }

    public void ClearRequest(String unitID)
    {
        _cleanUpPathRequestList();
        // Remove all agent request
        _pathRequestList.RemoveAll(pathRequest => pathRequest.Agent.GetUnitID() == unitID);
    }

    private void _cleanUpPathRequestList()
    {
        // Clean up invalid agents
        _pathRequestList.RemoveAll(pathRequest => pathRequest.Agent == null || !IsInstanceValid(pathRequest.Agent));
    }

    // Check queue periodically and process fixed request at a time to not slow down whole game
    private void _checkPathRequestQueue()
    {
        _cleanUpPathRequestList();


        // Processed through fixed path request count
        // This allow large amount of AI requests to be processed without "slow" down AI movement
        for (int index = 0; index < _processedPathRequestsPerCheck; index++)
        {
            if (_pathRequestList.Count > 0)
            {
                PathRequest pathRequest = _pathRequestList[0];

                _pathRequestList.RemoveAt(0);

                if (pathRequest.Agent != null && IsInstanceValid(pathRequest.Agent))
                {
                    List<Vector2> pathPoints = _getPath(pathRequest.Source, pathRequest.Target);
                    ((AIAgent)pathRequest.Agent).GetAI().SetPathResult(pathPoints);
                }
            }
            else
            {
                // No need to loop through as there is no more request
                break;
            }
        }

        // Start timer again afterward
        _updatePathRequestTimer.Start();
    }

    private List<Vector2> _getPath(Vector2 source, Vector2 target)
    {
        Vector2 cellSource = _tileMap.WorldToMap(source);
        Vector2 cellTarget = _tileMap.WorldToMap(target);

        int sourceId = _getPointID((int)cellSource.x, (int)cellSource.y);
        int targetId = _getPointID((int)cellTarget.x, (int)cellTarget.y);

        List<Vector2> worldPath = new List<Vector2>();

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
            int id = _getPointID((int)cellPath[index].x, (int)cellPath[index].y);
            worldPath.Add(_tilestoWorld[id]);
        }

        return worldPath;
    }

    public override void _PhysicsProcess(float delta)
    {
        UpdateNavigationMap();
    }

    private void _connectTraversableTiles(List<Vector2> tiles)
    {

        foreach (Vector2 tile in tiles)
        {
            // Add all neighbors
            for (int indexX = -1; indexX < 2; indexX++)
            {
                for (int indexY = -1; indexY < 2; indexY++)
                {
                    // No diagnoal for now to improve performance
                    // https://stackoverflow.com/questions/12417925/a-star-pathfinding-choosing-bad-waypoints
                    if (indexY == indexX || indexY == -1 * indexX)
                    {
                        continue;
                    }
                    Vector2 targetTile = new Vector2(tile.x + indexX, tile.y + indexY);
                    int toId = _getPointID((int)targetTile.x, (int)targetTile.y);
                    int fromId = _getPointID((int)tile.x, (int)tile.y);

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
                        int horizaontalId = _getPointID((int)tile.x + indexX, (int)tile.y);
                        int verticalId = _getPointID((int)tile.x, (int)tile.y + indexY);

                        // Upper diaonal connection depend on if there is obstacle for vertical/horizontal neighbors
                        // If there are obstacles (i.e. point not exist, then not connect it)
                        if (!_aStar.HasPoint(horizaontalId) || !_aStar.HasPoint(verticalId))
                        {
                            continue;
                        }
                    }

                    if (fromId != toId && !_aStar.ArePointsConnected(fromId, toId))
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
