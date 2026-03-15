using Mirror;
using UnityEngine;

/// <summary>
/// Спавнит мобов в комнатах после генерации подземелья.
/// Вызывается только на сервере из GridWalkDungeonGenerator.
/// Поддерживает несколько типов мобов (массив префабов).
/// </summary>
public class MobSpawner : MonoBehaviour
{
    [Header("Мобы")]
    [SerializeField] private MobSpawnEntry[] mobPrefabs;

    [Header("Настройки спавна")]
    [SerializeField] private int mobsPerNormalRoom = 1;

    private System.Random rng;
    private int totalWeight;

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

        if (mobPrefabs == null || mobPrefabs.Length == 0)
        {
            Debug.LogError("[MobSpawner] mobPrefabs не назначены!");
            return;
        }

        totalWeight = 0;
        foreach (var entry in mobPrefabs)
            totalWeight += entry.weight;

        rng = new System.Random(seed + 42);

        foreach (var cell in generator.Graph.cells)
        {
            if (cell.roomType == RoomType.Normal)
                SpawnMobsInRoom(cell);
        }
    }

    private void SpawnMobsInRoom(CellData cell)
    {
        // Создаём менеджер группы для комнаты
        var groupObj = new GameObject($"MobGroup_{cell.roomOrigin}");
        groupObj.transform.position = (Vector3)(Vector2)cell.RoomCenter;
        var group = groupObj.AddComponent<MobGroupManager>();

        for (int i = 0; i < mobsPerNormalRoom; i++)
        {
            Vector2 pos = GetRandomFloorPosition(cell);
            SpawnMob(pos, cell.RoomCenter, group);
        }
    }

    private void SpawnMob(Vector2 pos, Vector2 roomCenter, MobGroupManager group)
    {
        GameObject prefab = PickRandomPrefab();
        GameObject mob = Instantiate(prefab, pos, Quaternion.identity);

        var ai = mob.GetComponent<MobAI>();
        if (ai != null)
        {
            ai.Init(roomCenter, group);
            group.Register(ai);
        }

        NetworkServer.Spawn(mob);
    }

    private GameObject PickRandomPrefab()
    {
        int roll = rng.Next(totalWeight);
        int cumulative = 0;

        foreach (var entry in mobPrefabs)
        {
            cumulative += entry.weight;
            if (roll < cumulative)
                return entry.prefab;
        }

        return mobPrefabs[0].prefab;
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

/// <summary>
/// Запись в таблице спавна: префаб + вес (чем больше, тем чаще спавнится).
/// </summary>
[System.Serializable]
public struct MobSpawnEntry
{
    public GameObject prefab;
    [Range(1, 100)] public int weight;
}
