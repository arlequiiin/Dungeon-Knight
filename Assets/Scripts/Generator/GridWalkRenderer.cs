using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class GridWalkRenderer : MonoBehaviour
{
    [Header("Tilemap слои")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap lavaTilemap;

    [Header("Тайлы")]
    [SerializeField] private TileBase groundTile;
    [SerializeField] private TileBase lavaTile;

    public HashSet<Vector2Int> GroundPositions { get; private set; }

    private void Start()
    {
        if (groundTilemap == null)
            Debug.LogError("[GridWalkRenderer] Ground Tilemap не назначен!");
        if (lavaTilemap == null)
            Debug.LogError("[GridWalkRenderer] Lava Tilemap не назначен!");
        if (groundTile == null)
            Debug.LogWarning("[GridWalkRenderer] Ground Tile не назначен!");
        if (lavaTile == null)
            Debug.LogWarning("[GridWalkRenderer] Lava Tile не назначен!");
    }

    public void RenderDungeon(GridWalkGenerator generator, GridWalkConfig config)
    {
        groundTilemap.ClearAllTiles();
        lavaTilemap.ClearAllTiles();

        // Собираем все позиции пола
        GroundPositions = new HashSet<Vector2Int>();

        // Пол комнат
        foreach (var cell in generator.Graph.cells)
        {
            foreach (var tile in cell.floorTiles)
                GroundPositions.Add(tile);
        }

        // Пол коридоров
        CollectCorridors(generator.Graph, config);

        // Ставим ground-тайлы
        if (groundTile != null)
        {
            foreach (var pos in GroundPositions)
                groundTilemap.SetTile(new Vector3Int(pos.x, pos.y, 0), groundTile);
        }

        // Заливка лавой — только там, где НЕТ пола
        if (lavaTile != null)
        {
            for (int x = 0; x < config.WorldWidth; x++)
            {
                for (int y = 0; y < config.WorldHeight; y++)
                {
                    if (!GroundPositions.Contains(new Vector2Int(x, y)))
                        lavaTilemap.SetTile(new Vector3Int(x, y, 0), lavaTile);
                }
            }
        }

        // Обновление Rule Tiles
        groundTilemap.RefreshAllTiles();
        lavaTilemap.RefreshAllTiles();

        Debug.Log($"[GridWalkRenderer] Отрисовано: ground: {GroundPositions.Count} тайлов, " +
                  $"комнат: {generator.Graph.cells.Count}, рёбер: {generator.Graph.edges.Count}");
    }

    private void CollectCorridors(DungeonGraph graph, GridWalkConfig config)
    {
        int halfWidth = config.corridorWidth / 2;

        foreach (var (cellA, cellB) in graph.edges)
        {
            Vector2Int diff = cellB.gridPos - cellA.gridPos;

            if (diff.x != 0)
            {
                CellData left = diff.x > 0 ? cellA : cellB;
                CellData right = diff.x > 0 ? cellB : cellA;

                int startX = left.roomOrigin.x + left.roomSize.x;
                int endX = right.roomOrigin.x - 1;
                int centerY = (int)((left.RoomCenter.y + right.RoomCenter.y) / 2f);

                for (int x = startX; x <= endX; x++)
                    for (int offset = -halfWidth; offset <= halfWidth; offset++)
                        GroundPositions.Add(new Vector2Int(x, centerY + offset));

                // Стыковка
                AddConnection(left, centerY, halfWidth, true, false);
                AddConnection(right, centerY, halfWidth, false, false);
            }
            else if (diff.y != 0)
            {
                CellData bottom = diff.y > 0 ? cellA : cellB;
                CellData top = diff.y > 0 ? cellB : cellA;

                int startY = bottom.roomOrigin.y + bottom.roomSize.y;
                int endY = top.roomOrigin.y - 1;
                int centerX = (int)((bottom.RoomCenter.x + top.RoomCenter.x) / 2f);

                for (int y = startY; y <= endY; y++)
                    for (int offset = -halfWidth; offset <= halfWidth; offset++)
                        GroundPositions.Add(new Vector2Int(centerX + offset, y));

                // Стыковка
                AddConnection(bottom, centerX, halfWidth, false, true);
                AddConnection(top, centerX, halfWidth, true, true);
            }
        }
    }

    private void AddConnection(CellData cell, int corridorCenter, int halfWidth,
        bool isRightSide, bool vertical)
    {
        if (!vertical)
        {
            int x = isRightSide ? cell.roomOrigin.x + cell.roomSize.x - 1 : cell.roomOrigin.x;
            for (int offset = -halfWidth; offset <= halfWidth; offset++)
                GroundPositions.Add(new Vector2Int(x, corridorCenter + offset));
        }
        else
        {
            int y = isRightSide ? cell.roomOrigin.y : cell.roomOrigin.y + cell.roomSize.y - 1;
            for (int offset = -halfWidth; offset <= halfWidth; offset++)
                GroundPositions.Add(new Vector2Int(corridorCenter + offset, y));
        }
    }
}
