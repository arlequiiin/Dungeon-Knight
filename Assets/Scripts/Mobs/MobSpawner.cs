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
    [Header("Таблица спавна (ScriptableObject)")]
    [Tooltip("Основной источник весов. Если не назначен — используется legacy-массив Mob Prefabs ниже.")]
    [SerializeField] private MobSpawnTable spawnTable;

    [Header("Legacy (fallback если spawnTable пустой)")]
    [SerializeField] private MobSpawnEntry[] mobPrefabs;

    [Header("Босс")]
    [SerializeField] private GameObject bossPrefab;

    // Переопределяется LevelConfig.bossPrefab если он задан.
    private GameObject activeBossPrefab;

    [Header("Индикатор спавна")]
    [Tooltip("Префаб ArrowUI — стрелка-индикатор, показывается на месте будущего спавна моба.")]
    [SerializeField] private GameObject spawnIndicatorPrefab;
    public GameObject SpawnIndicatorPrefab => spawnIndicatorPrefab;

    [Header("Настройки спавна")]
    [SerializeField] private int mobsPerNormalRoom = 1;
    private int activeMobsPerNormalRoom = 1;

    private System.Random rng;
    private int currentPlayerCount;
    private float currentDifficulty;
    private MobSpawnTable activeTable;
    private LevelConfig activeLevel;

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
        // LevelConfig.difficulty (если задан) перекрывает аргумент.
        currentDifficulty = activeLevel != null ? activeLevel.difficulty : difficulty;

        // Если SO-таблица назначена — используем её, иначе строим временную из legacy-массива.
        activeTable = spawnTable != null ? spawnTable : BuildLegacyTable();

        // Босс: LevelConfig имеет приоритет над bossPrefab из инспектора.
        activeBossPrefab = activeLevel != null && activeLevel.bossPrefab != null ? activeLevel.bossPrefab : bossPrefab;

        // Масштаб числа мобов в комнате: базовое значение из инспектора умножается на
        // LevelConfig.perPlayerMobMultiplier (игроки сверх первого добавляют пропорционально).
        float mobMul = activeLevel != null ? activeLevel.perPlayerMobMultiplier : 1f;
        int extraPlayers = Mathf.Max(0, playerCount - 1);
        float scaled = mobsPerNormalRoom * (1f + mobMul * extraPlayers);
        activeMobsPerNormalRoom = Mathf.Max(1, Mathf.RoundToInt(scaled));

        rng = new System.Random(seed + 42);
    }

    /// <summary>
    /// Применить LevelConfig до Initialize: подменяет таблицу мобов и префаб босса.
    /// </summary>
    public void ApplyLevelConfig(LevelConfig level)
    {
        activeLevel = level;
        if (level != null && level.mobTable != null)
            spawnTable = level.mobTable;
    }

    /// <summary>
    /// Подменить таблицу спавна в рантайме (например, при смене биома/сложности).
    /// </summary>
    public void SetTable(MobSpawnTable table)
    {
        activeTable = table;
    }

    private MobSpawnTable BuildLegacyTable()
    {
        if (mobPrefabs == null || mobPrefabs.Length == 0) return null;
        var t = ScriptableObject.CreateInstance<MobSpawnTable>();
        t.entries = mobPrefabs;
        return t;
    }

    /// <summary>
    /// Только подбирает позиции для будущей волны мобов (без спавна).
    /// Используется для предварительного показа индикаторов спавна.
    /// </summary>
    public Vector2[] PreparePositions(CellData cell, int count = -1)
    {
        if (count < 0) count = activeMobsPerNormalRoom;
        var arr = new Vector2[count];
        for (int i = 0; i < count; i++)
            arr[i] = GetRandomFloorPosition(cell);
        return arr;
    }

    /// <summary>
    /// Спавнит мобов в обычной комнате по заранее подобранным позициям (если переданы).
    /// Возвращает список MobHealth для отслеживания.
    /// </summary>
    public List<MobHealth> SpawnRoomMobs(CellData cell, Vector2[] presetPositions = null)
    {
        if (!NetworkServer.active) return null;
        if (activeTable == null || activeTable.entries == null || activeTable.entries.Length == 0)
        {
            Debug.LogWarning("[MobSpawner] spawnTable / mobPrefabs не назначены!");
            return null;
        }

        var result = new List<MobHealth>();

        // Создаём менеджер группы для комнаты
        var groupObj = new GameObject($"MobGroup_{cell.roomOrigin}");
        groupObj.transform.position = (Vector3)(Vector2)cell.RoomCenter;
        var group = groupObj.AddComponent<MobGroupManager>();

        int spawnCount = presetPositions != null ? presetPositions.Length : activeMobsPerNormalRoom;
        for (int i = 0; i < spawnCount; i++)
        {
            Vector2 pos = presetPositions != null ? presetPositions[i] : GetRandomFloorPosition(cell);
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
        var prefab = activeBossPrefab != null ? activeBossPrefab : bossPrefab;
        if (prefab == null)
        {
            Debug.LogWarning("[MobSpawner] bossPrefab not assigned!");
            return null;
        }

        Vector2 pos = cell.RoomCenter;
        bossInstance = Instantiate(prefab, pos, Quaternion.identity);

        var ai = bossInstance.GetComponent<MobAI>();
        if (ai != null)
            ai.Init(pos, null, currentPlayerCount, currentDifficulty);

        var health = bossInstance.GetComponent<MobHealth>();
        if (health != null)
            health.SetBoss(true);

        NetworkServer.Spawn(bossInstance);

        return health;
    }

    private GameObject PickRandomPrefab()
    {
        return activeTable != null ? activeTable.PickRandom(rng) : null;
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
