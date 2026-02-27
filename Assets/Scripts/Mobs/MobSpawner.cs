using Mirror;
using UnityEngine;

/// <summary>
/// Спавнит мобов в комнатах после генерации подземелья.
/// Вызывается только на сервере из GridWalkDungeonGenerator.
/// </summary>
public class MobSpawner : MonoBehaviour
{
    [Header("Мобы")]
    [SerializeField] private GameObject skeletonPrefab;

    [Header("Настройки спавна")]
    [SerializeField] private int skeletonsPerNormalRoom = 1;

    private System.Random rng;

    /// <summary>
    /// Спавнит мобов во всех нормальных комнатах подземелья.
    /// Должен вызываться после BakeNavMesh().
    /// </summary>
    public void SpawnMobs(GridWalkGenerator generator, int seed)
    {
        if (!NetworkServer.active)
        {
            Debug.LogWarning("[MobSpawner] SpawnMobs вызван не на сервере, игнорируем");
            return;
        }

        if (skeletonPrefab == null)
        {
            Debug.LogError("[MobSpawner] skeletonPrefab не назначен!");
            return;
        }

        rng = new System.Random(seed + 42);

        foreach (var cell in generator.Graph.cells)
        {
            if (cell.roomType == RoomType.Normal)
                SpawnSkeletonsInRoom(cell);
        }
    }

    private void SpawnSkeletonsInRoom(CellData cell)
    {
        for (int i = 0; i < skeletonsPerNormalRoom; i++)
        {
            Vector2 pos = GetRandomFloorPosition(cell);
            SpawnSkeleton(pos, cell.RoomCenter);
        }
    }

    private void SpawnSkeleton(Vector2 pos, Vector2 roomCenter)
    {
        GameObject mob = Instantiate(skeletonPrefab, pos, Quaternion.identity);

        var ai = mob.GetComponent<SkeletonAI>();
        if (ai != null)
            ai.Init(roomCenter);

        NetworkServer.Spawn(mob);
    }

    /// <summary>
    /// Возвращает случайную позицию пола внутри комнаты с отступом от стен.
    /// </summary>
    private Vector2 GetRandomFloorPosition(CellData cell)
    {
        // Пробуем найти случайный тайл пола с отступом от стен
        int margin = 1;
        int attempts = 20;

        for (int i = 0; i < attempts; i++)
        {
            int x = rng.Next(cell.roomOrigin.x + margin, cell.roomOrigin.x + cell.roomSize.x - margin);
            int y = rng.Next(cell.roomOrigin.y + margin, cell.roomOrigin.y + cell.roomSize.y - margin);
            var tile = new UnityEngine.Vector2Int(x, y);

            if (cell.floorTiles.Contains(tile))
                return new Vector2(x + 0.5f, y + 0.5f);
        }

        // Фолбэк — центр комнаты
        return cell.RoomCenter;
    }
}
