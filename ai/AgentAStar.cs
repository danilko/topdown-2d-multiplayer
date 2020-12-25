using Godot;
using System;

public class AgentAStar
{
    private Godot.Collections.Array cells;
    private Godot.Collections.Dictionary celltoWorld;
    private Vector2 cellSize;
    TileMap tileMap;

    private AStar2D aStar;

    public AgentAStar(TileMap tileMap)
    {
        aStar = new AStar2D();
        cells = new Godot.Collections.Array();
        celltoWorld = new Godot.Collections.Dictionary();
        this.tileMap = tileMap;

        cellSize = tileMap.CellSize;

    }

    public void addCell(Vector2 cellPos)
    {
        int id = getPointID((int)cellPos.x, (int)cellPos.y);
        aStar.AddPoint(id, cellPos, 1);
        cells.Add(cellPos);
        celltoWorld.Add(id, tileMap.MapToWorld(cellPos) + (cellSize / 2));
    }

    // Pair function to generate unique id from two numbers
    private int getPointID(int xIndex, int yIndex)
    {
        return (((xIndex + yIndex) * (xIndex + yIndex + 1)) / 2) + yIndex;

    }

    public Godot.Collections.Array getPath(Vector2 source, Vector2 target)
    {
        Vector2 cellSource = tileMap.WorldToMap(source);
        Vector2 cellTarget = tileMap.WorldToMap(target);

        int sourceId = getPointID((int)cellSource.x, (int)cellSource.y);
        int targetId = getPointID((int)cellTarget.x, (int)cellTarget.y);

        Vector2[] cellPath = aStar.GetPointPath(sourceId, targetId);

        Godot.Collections.Array worldPath = new Godot.Collections.Array();

        // Reverse adding the points, and ignore 0, as 0 is the source, which is already on
        for (int index = 1; index < cellPath.Length; index++)
        {
            Vector2 pos = tileMap.MapToWorld(cellPath[index]) + (cellSize / 2);
            worldPath.Add(pos);
        }

        return worldPath;
    }

    public void connectPoints()
    {
        // Connect neighbors
        Godot.Collections.Array neighbors = new Godot.Collections.Array();

        neighbors.Add(new Vector2(1, 0));
        neighbors.Add(new Vector2(-1, 0));
        neighbors.Add(new Vector2(0, 1));
        neighbors.Add(new Vector2(0, -1));

        foreach (Vector2 cell in cells)
        {
            foreach (Vector2 neighbor in neighbors)
            {
                Vector2 nextcell = cell + neighbor;
                int toId = getPointID((int)nextcell.x, (int)nextcell.y);
                if (aStar.HasPoint(getPointID((int)nextcell.x, (int)nextcell.y)))
                {
                    int fromId = getPointID((int)cell.x, (int)cell.y);
                    aStar.ConnectPoints(fromId, toId, false);
                }
            }
        }
    }
}
