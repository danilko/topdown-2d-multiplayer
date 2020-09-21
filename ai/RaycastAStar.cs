using Godot;
using System;

// Reference from https://www.thebotanistgame.com/blog/2020/05/01/raycasting-astar-in-godot.html
public class RaycastAStar
{

    private const float GRID_SIZE = 128;

    private Godot.Collections.Dictionary openSet = null;
    private Godot.Collections.Dictionary cameFrom = null;
    private Godot.Collections.Dictionary gScore = null;
    private Godot.Collections.Dictionary fScore = null;
    private Godot.Collections.Array excludes;
    private World2D space;
    private Godot.Collections.Dictionary neighborsMap = new Godot.Collections.Dictionary();
    private int max_iterations = 1000;

    private GameWorld gameWorld;

    // Simple vector seralization
    private String vector2ToId(Vector2 vector)
    {
        return vector.x + "," + vector.y;
    }

    // Finds the (serialized) point in the openSet with the smallest fScore
    public String findSmallestFScore()
    {
        float smallestFScore = Int64.MaxValue;
        String smallestPoint = null;

        foreach (Vector2 vector in openSet.Values)
        {
            String vectorId = vector2ToId(vector);
            if (fScore.Contains(vectorId) && (float)fScore[vectorId] < smallestFScore)
            {
                smallestFScore = (float)fScore[vectorId];
                smallestPoint = vectorId;
            }
        }

        return "" + smallestPoint;
    }

    // Heuristic distance between two points. In this case, actual distance.
    // This is straight-line distance though, and ignores any obstacles.
    public float hDistance(Vector2 start, Vector2 end)
    {
        return (end - start).Length();
    }

    // Uses raycasting to find the traversable neighbors of the given position
    // Cache results
    public Godot.Collections.Array getNeighbors(Vector2 vector)
    {
        String vectorId = vector2ToId(vector);

        if (neighborsMap.Contains(vectorId))
        {
            return (Godot.Collections.Array)neighborsMap[vectorId];
        }

        Godot.Collections.Array targets = new Godot.Collections.Array();
        targets.Add(vector + new Vector2(GRID_SIZE, 0));
        targets.Add(vector + new Vector2(-GRID_SIZE, 0));
        targets.Add(vector + new Vector2(0, GRID_SIZE));
        targets.Add(vector + new Vector2(0, -GRID_SIZE));

        targets.Add(vector + new Vector2(GRID_SIZE, -GRID_SIZE));
        targets.Add(vector + new Vector2(GRID_SIZE, GRID_SIZE));
        targets.Add(vector + new Vector2(-GRID_SIZE, GRID_SIZE));
        targets.Add(vector + new Vector2(-GRID_SIZE, -GRID_SIZE));

        Godot.Collections.Array valid = new Godot.Collections.Array();

        foreach (Vector2 target in targets)
        {

            Physics2DDirectSpaceState ray = space.DirectSpaceState;
            Godot.Collections.Dictionary result = ray.IntersectRay(vector, target, excludes);

            // There's nothing there, so we can visit the neighbor
            if (result.Count == 0)
            {
                debugDraw(target);
                valid.Add(target);
            }
        }


        neighborsMap.Add(vectorId, valid);

        return valid;
    }

    public void debugDraw(Vector2 target)
    {

        Node2D path = null;

        if (! gameWorld.HasNode("path_raycast"))
        {
            path = (Node2D)gameWorld.GetNode("pathchart").Duplicate();
            path.Name = "path_raycast";
            gameWorld.AddChild(path);
        }
        else
        {
            path = (Node2D)gameWorld.GetNode("path_raycast");
        }

        Node2D pointNode = (Node2D)gameWorld.GetNode("dot").Duplicate();
        pointNode.Name = "path_raycast_" + target.x + "_" + target.y;
        pointNode.Position = target;
        path.AddChild(pointNode);
    }

    public void debugDrawClear()
    {
        Node2D path = (Node2D)gameWorld.GetNode("path_raycast");

        if (path != null)
        {
            foreach (Node2D node in path.GetChildren())
            {
                node.QueueFree();
            }
        }
    }

    // Works backward, looking up ideal steps in cameFrom, to reproduce the full path
    public Godot.Collections.Array reconstructPath(Vector2 currentVector)
    {
        String currentVectorId = vector2ToId(currentVector);
        Godot.Collections.Array totalPath = new Godot.Collections.Array();
        totalPath.Insert(0, currentVector);

        while (cameFrom.Contains(currentVectorId))
        {
            currentVector = (Vector2)cameFrom[currentVectorId];
            currentVectorId = vector2ToId(currentVector);
            totalPath.Insert(0, currentVector);
        }

        return totalPath;
    }

    // Normalizes a point onto our grid (centering on cells)
    public Vector2 normalizePoint(Vector2 vector)
    {
        return new Vector2(Mathf.Round(vector.x / GRID_SIZE) * GRID_SIZE + (GRID_SIZE / 2.0f), Mathf.Round(vector.y / GRID_SIZE) * GRID_SIZE + (GRID_SIZE / 2.0f));
    }

    // Entrypoint to the pathfinding algorithm. Will return either null or an array of Vector2s
    public Godot.Collections.Array path(Vector2 start, Vector2 end, World2D space_state, Godot.Collections.Array exclude_bodies, GameWorld gameWorld)
    {

        this.gameWorld = gameWorld;

        int iteration = 0;

        // Update class variables
        space = space_state;
        excludes = exclude_bodies;

        start = normalizePoint(start);
        end = normalizePoint(end);
        String startId = vector2ToId(start);
        String endId = vector2ToId(end);

        cameFrom = new Godot.Collections.Dictionary();
        openSet = new Godot.Collections.Dictionary();
        openSet.Add(startId, start);
        gScore = new Godot.Collections.Dictionary();
        fScore = new Godot.Collections.Dictionary();

        gScore.Add(startId, 0.0f);
        fScore.Add(startId, hDistance(end, start));

        // As long as we have points to visit, let's visit them
        // But not more than max_iterations times.

        while (openSet.Count > 0 && iteration < max_iterations)
        {
            // We're going to grab the current best tile, then look at its neighbors
            String currentId = findSmallestFScore();

            Vector2 current = (Vector2)openSet[currentId];

            // We reached the goal, so stop here and return the path.
            if (currentId == endId)
            {
                return reconstructPath(current);
            }

            openSet.Remove(currentId);

            Godot.Collections.Array neighbors = getNeighbors(current);

            foreach (Vector2 neighbor in neighbors)
            {
                String neighborId = vector2ToId(neighbor);
                float neighborGScore = Int32.MaxValue;

                // We've seen this neighbor before, likely when passing through from a different path.
                if (gScore.Contains(neighborId))
                {
                    neighborGScore = (float)gScore[neighborId];
                }

                // This is the "new" gScore as taken through _this_ path, not the previous path
                float tentativeGscore = ((float)(gScore[currentId])) + GRID_SIZE;


                // If this path is better than the previous path through this neighbor, record it
                if (tentativeGscore < neighborGScore)
                {

                    // This lets us work backwards through best-points later
                    if (!cameFrom.Contains(neighborId)) { cameFrom.Add(neighborId, current); }

                    // gScore is the actual distance it took to get here from the start
                    if (!gScore.Contains(neighborId)) { gScore.Add(neighborId, tentativeGscore); }

                    // fScore is the actual distance from the start plus the estimated distance to the end
                    // Whoever has the best fScore in the openSet gets our attention next
                    // Therefore we are always inspecting the current best-guess-path
                    if (!fScore.Contains(neighborId)) { fScore.Add(neighborId, tentativeGscore + hDistance(end, neighbor));}

                    // This would allow revisiting if the heuristic were not consistent
                    // But in our use case we should not end up revisiting nodes
                    if (!openSet.Contains(neighborId))
                    {
                        openSet.Add(neighborId, neighbor);
                    }

                }
            }

            iteration++;
        }

        // No path found
        return null;
    }
}
