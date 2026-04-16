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

    [Header("Босс")]
    [SerializeField] private GameObject bossPrefab;

    [Header("Настройки спавна")]
    [SerializeField] private int mobsPerNormalRoom = 1;

    private System.Random rng;
    private int totalWeight;
    private int currentPlayerCount;
    private float currentDifficulty;

    // Boss instance — exposed for UI binding
    private GameObject bossInstance;
    public GameObject BossInstance => bossInstance;

    /// <summary>
    /// Спавнит мобов во всех нормальных комнатах и босса в Boss-комнате.
    /// Должен вызываться после BakeNavMesh().
    /// </summary>
    public void SpawnMobs(GridWalkGenerator generator, int seed, int playerCount = 1, float difficulty = 1f)
    {
        if (!NetworkServer.active)
        {
            Debug.LogWarning("[MobSpawner] SpawnMobs called on non-server, ignoring");
            return;
        }

        if (mobPrefabs == null || mobPrefabs.Length == 0)
        {
            Debug.LogError("[MobSpawner] mobPrefabs not assigned!");
            return;
        }

        currentPlayerCount = playerCount;
        currentDifficulty = difficulty;

        totalWeight = 0;
        foreach (var entry in mobPrefabs)
            totalWeight += entry.weight;

        rng = new System.Random(seed + 42);

        foreach (var cell in generator.Graph.cells)
        {
            if (cell.roomType == RoomType.Normal)
                SpawnMobsInRoom(cell);
            else if (cell.roomType == RoomType.Boss)
                SpawnBossInRoom(cell);
        }
    }

    private void SpawnBossInRoom(CellData cell)
    {
        if (bossPrefab == null)
        {
            Debug.LogWarning("[MobSpawner] bossPrefab not assigned, skipping Boss room");
            return;
        }

        Vector2 pos = cell.RoomCenter;
        bossInstance = Instantiate(bossPrefab, pos, Quaternion.identity);

        var ai = bossInstance.GetComponent<MobAI>();
        if (ai != null)
            ai.Init(pos, null, currentPlayerCount, currentDifficulty);

        var bossHealth = bossInstance.GetComponent<MobHealth>();
        if (bossHealth != null)
            bossHealth.SetBoss(true);

        NetworkServer.Spawn(bossInstance);
        Debug.Log($"[MobSpawner] Boss spawned at {pos}");
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
            ai.Init(roomCenter, group, currentPlayerCount, currentDifficulty);
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
