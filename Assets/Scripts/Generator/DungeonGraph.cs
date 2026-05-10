using System.Collections.Generic;
using UnityEngine;

public class DungeonGraph
{
    public List<CellData> cells = new List<CellData>();
    public List<(CellData, CellData)> edges = new List<(CellData, CellData)>();

    private HashSet<(Vector2Int, Vector2Int)> edgeSet = new HashSet<(Vector2Int, Vector2Int)>();

    public bool HasEdge(CellData a, CellData b)
    {
        var key = MakeEdgeKey(a.gridPos, b.gridPos);
        return edgeSet.Contains(key);
    }

    public void AddEdge(CellData a, CellData b)
    {
        var key = MakeEdgeKey(a.gridPos, b.gridPos);
        if (edgeSet.Contains(key)) return;

        edgeSet.Add(key);
        edges.Add((a, b));
        if (!a.neighbors.Contains(b)) a.neighbors.Add(b);
        if (!b.neighbors.Contains(a)) b.neighbors.Add(a);
    }

    private (Vector2Int, Vector2Int) MakeEdgeKey(Vector2Int a, Vector2Int b)
    {
        // Упорядочиваем чтобы (A,B) == (B,A)
        if (a.x < b.x || (a.x == b.x && a.y < b.y))
            return (a, b);
        return (b, a);
    }

    // BFS: находит самую дальнюю ячейку от start
    public CellData FindFarthestCell(CellData start)
    {
        var visited = new HashSet<CellData>();
        var queue = new Queue<(CellData cell, int dist)>();
        queue.Enqueue((start, 0));
        visited.Add(start);

        CellData farthest = start;
        int maxDist = 0;

        while (queue.Count > 0)
        {
            var (cell, dist) = queue.Dequeue();
            if (dist > maxDist)
            {
                maxDist = dist;
                farthest = cell;
            }

            foreach (var neighbor in cell.neighbors)
            {
                if (!visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    queue.Enqueue((neighbor, dist + 1));
                }
            }
        }

        return farthest;
    }

    // BFS-расстояния от start до всех остальных ячеек.
    public Dictionary<CellData, int> BfsDistances(CellData start)
    {
        var dist = new Dictionary<CellData, int> { [start] = 0 };
        var queue = new Queue<CellData>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            int d = dist[cur];
            foreach (var n in cur.neighbors)
            {
                if (dist.ContainsKey(n)) continue;
                dist[n] = d + 1;
                queue.Enqueue(n);
            }
        }
        return dist;
    }

    // Находит все листья графа (ячейки с 1 ребром)
    public List<CellData> FindLeaves(params CellData[] exclude)
    {
        var excludeSet = new HashSet<CellData>(exclude);
        var leaves = new List<CellData>();

        foreach (var cell in cells)
        {
            if (cell.neighbors.Count == 1 && !excludeSet.Contains(cell))
                leaves.Add(cell);
        }

        return leaves;
    }
}
