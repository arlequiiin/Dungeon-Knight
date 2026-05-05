using UnityEngine;

[CreateAssetMenu(fileName = "DungeonConfig", menuName = "Dungeon Knight/Dungeon Config")]
public class GridWalkConfig : ScriptableObject
{
    [Header("Количество комнат")]
    [Range(6, 15)]
    public int roomCount = 10;

    [Header("Сетка")]
    public Vector2Int gridSize = new Vector2Int(4, 3);

    [Header("Размер ячейки (в тайлах)")]
    public Vector2Int cellSize = new Vector2Int(10, 8);

    [Header("Расстояние между ячейками (тайлы)")]
    public int cellGap = 3;

    [Header("Размер комнат (в тайлах)")]
    public Vector2Int roomMinSize = new Vector2Int(6, 5);
    public Vector2Int roomMaxSize = new Vector2Int(9, 7);

    [Header("Коридоры")]
    public int corridorWidth = 2;
    [Range(0, 3)]
    public int extraEdges = 2;

    [Header("Модификация формы")]
    [Range(0f, 1f)]
    public float shapeModifyChance = 0.35f;

    [Header("Seed")]
    [Tooltip("Используется только если useRandomSeed = false. SO не мутируется в рантайме — реальный сид забега хранится отдельно в DungeonKnightNetworkManager.")]
    public int seed = 0;
    public bool useRandomSeed = true;

    // Полный размер мира в тайлах
    public int WorldWidth => gridSize.x * (cellSize.x + cellGap) + cellGap;
    public int WorldHeight => gridSize.y * (cellSize.y + cellGap) + cellGap;

    public void ValidateAndFix()
    {
        roomCount = Mathf.Clamp(roomCount, 6, gridSize.x * gridSize.y);
        roomMinSize = new Vector2Int(Mathf.Max(4, roomMinSize.x), Mathf.Max(4, roomMinSize.y));
        roomMaxSize = new Vector2Int(
            Mathf.Clamp(roomMaxSize.x, roomMinSize.x + 1, cellSize.x),
            Mathf.Clamp(roomMaxSize.y, roomMinSize.y + 1, cellSize.y)
        );
        corridorWidth = Mathf.Max(1, corridorWidth);
        cellGap = Mathf.Max(corridorWidth, cellGap);
        extraEdges = Mathf.Clamp(extraEdges, 0, 3);
    }
}
