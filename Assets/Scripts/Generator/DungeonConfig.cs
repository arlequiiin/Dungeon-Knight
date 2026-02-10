using UnityEngine;

// Конфигурация параметров генерации подземелья
[System.Serializable]
public class DungeonConfig
{
    [Header("Размеры карты")]
    public int mapWidth = 100;
    public int mapHeight = 100;

    [Header("Комнаты")]
    public int normalRoomsCount = 15;
    public int specialRoomsCount = 3;
    public Vector2Int minRoomSize = new Vector2Int(8, 8);
    public Vector2Int maxRoomSize = new Vector2Int(20, 20);
    [Range(2, 8)]
    public int bspDepth = 4; // Глубина BSP разбиения (больше = больше комнат)

    [Header("Коридоры")]
    public int corridorWidth = 3;

    [Header("Граф (маршруты)")]
    public int minPathToBossl = 5;
    public int maxPathToBoss = 10;
    [Range(0f, 1f)]
    public float branchProbability = 0.3f; // вероятность создания боковых ветвей (петли)

    [Header("Другое")]
    public int seed = 0;
    public bool useRandomSeed = true;

    public void ValidateAndFix()
    {
        mapWidth = Mathf.Max(50, mapWidth);
        mapHeight = Mathf.Max(50, mapHeight);
        normalRoomsCount = Mathf.Max(1, normalRoomsCount);
        specialRoomsCount = Mathf.Max(1, specialRoomsCount);
        minRoomSize = new Vector2Int(
            Mathf.Max(3, minRoomSize.x),
            Mathf.Max(3, minRoomSize.y)
        );
        maxRoomSize = new Vector2Int(
            Mathf.Max(minRoomSize.x + 1, maxRoomSize.x),
            Mathf.Max(minRoomSize.y + 1, maxRoomSize.y)
        );
        minPathToBossl = Mathf.Max(1, minPathToBossl);
        maxPathToBoss = Mathf.Max(minPathToBossl + 1, maxPathToBoss);
        corridorWidth = Mathf.Max(1, corridorWidth);
    }
}
