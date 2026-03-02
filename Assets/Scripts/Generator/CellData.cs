using UnityEngine;
using System.Collections.Generic;

public class CellData
{
    public Vector2Int gridPos;          // позиция на сетке (0,0)..(4,3)
    public Vector2Int worldOrigin;      // начало ячейки в мировых тайлах
    public Vector2Int roomOrigin;       // начало комнаты в мировых тайлах
    public Vector2Int roomSize;         // размер комнаты
    public HashSet<Vector2Int> floorTiles = new HashSet<Vector2Int>(); // тайлы пола (мировые координаты)
    public RoomType roomType = RoomType.Normal;
    public List<CellData> neighbors = new List<CellData>();

    public Vector2 RoomCenter => new Vector2(
        roomOrigin.x + roomSize.x / 2f,
        roomOrigin.y + roomSize.y / 2f
    );

    public CellData(Vector2Int gridPos, Vector2Int worldOrigin)
    {
        this.gridPos = gridPos;
        this.worldOrigin = worldOrigin;
    }

    public override string ToString()
    {
        return $"Cell(grid={gridPos}, type={roomType}, roomSize={roomSize})";
    }
}
