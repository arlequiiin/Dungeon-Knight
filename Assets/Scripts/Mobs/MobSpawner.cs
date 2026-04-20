using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// Спавнит мобов в комнатах подземелья.
/// Вызывается только на сервере. Инициализируется из GridWalkDungeonGenerator,
/// а спавн мобов происходит по запросу RoomController при входе игрока в комнату.
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
    /// Инициализирует спавнер (seed, масштабирование).
    /// Вызывать один раз после генерации подземелья, перед SpawnRoomMobs/SpawnRoomBoss.
    /// </summary>
    public void Initialize(int seed, int playerCount = 1, float difficulty = 1f)
    {
        if (!NetworkServer.active) return;

        currentPlayerCount = playerCount;
        currentDifficulty = difficulty;

        totalWeight = 0;
        if (mobPrefabs != null)
            foreach (var entry in mobPrefabs)
                totalWeight += entry.weight;

        rng = new System.Random(seed + 42);
    }

    /// <summary>
    /// Спавнит мобов в обычной комнате. Возвращает список MobHealth для отслеживания.
    /// Вызывается из RoomController при входе игрока.
    /// </summary>
    public List<MobHealth> SpawnRoomMobs(CellData cell)
    {
        if (!NetworkServer.active) return null;
        if (mobPrefabs == null || mobPrefabs.Length == 0)
        {
            Debug.LogWarning("[MobSpawner] mobPrefabs not assigned!");
            return null;
        }

        var result = new List<MobHealth>();

        // Создаём менеджер группы для комнаты
        var groupObj = new GameObject($"MobGroup_{cell.roomOrigin}");
        groupObj.transform.position = (Vector3)(Vector2)cell.RoomCenter;
        var group = groupObj.AddComponent<MobGroupManager>();

        for (int i = 0; i < mobsPerNormalRoom; i++)
        {
            Vector2 pos = GetRandomFloorPosition(cell);
            GameObject prefab = PickRandomPrefab();
            GameObject mob = Instantiate(prefab, pos, Quaternion.identity);

            var ai = mob.GetComponent<MobAI>();
            if (ai != null)
            {
                ai.Init(cell.RoomCenter, group, currentPlayerCount, currentDifficulty);
                group.Register(ai);
            }

            NetworkServer.Spawn(mob);

            var health = mob.GetComponent<MobHealth>();
            if (health != null)
                result.Add(health);
        }

        return result;
    }

    /// <summary>
    /// Спавнит босса в Boss-комнате. Возвращает MobHealth для отслеживания.
    /// Вызывается из RoomController при входе игрока.
    /// </summary>
    public MobHealth SpawnRoomBoss(CellData cell)
    {
        if (!NetworkServer.active) return null;
        if (bossPrefab == null)
        {
            Debug.LogWarning("[MobSpawner] bossPrefab not assigned!");
            return null;
        }

        Vector2 pos = cell.RoomCenter;
        bossInstance = Instantiate(bossPrefab, pos, Quaternion.identity);

        var ai = bossInstance.GetComponent<MobAI>();
        if (ai != null)
            ai.Init(pos, null, currentPlayerCount, currentDifficulty);

        var health = bossInstance.GetComponent<MobHealth>();
        if (health != null)
            health.SetBoss(true);

        NetworkServer.Spawn(bossInstance);
        Debug.Log($"[MobSpawner] Boss spawned at {pos}");

        return health;
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
        int margin = 1;
        int attempts = 20;

        for (int i = 0; i < attempts; i++)
        {
            int x = rng.Next(cell.roomOrigin.x + margin, cell.roomOrigin.x + cell.roomSize.x - margin);
            int y = rng.Next(cell.roomOrigin.y + margin, cell.roomOrigin.y + cell.roomSize.y - margin);
            var tile = new Vector2Int(x, y);

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
