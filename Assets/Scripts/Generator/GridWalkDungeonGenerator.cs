using UnityEngine;
using NavMeshPlus.Components;

public class GridWalkDungeonGenerator : MonoBehaviour
{
    [SerializeField] private GridWalkConfig config;
    [SerializeField] private GridWalkRenderer gridWalkRenderer;
    [SerializeField] private NavMeshSurface navMeshSurface;
    [SerializeField] private bool generateOnStart = false;

    [Header("Декорации лавы")]
    [SerializeField] private GameObject[] volcBubblePrefabs;
    [Range(0.001f, 0.05f)]
    [SerializeField] private float bubbleDensity = 0.005f;

    [Header("Декорации комнат")]
    [SerializeField] private GameObject[] deadTreePrefabs;
    [Range(1, 5)]
    [SerializeField] private int treesPerRoom = 2;

    [Header("Мобы")]
    [SerializeField] private MobSpawner mobSpawner;

    [Header("Сокровищницы")]
    [SerializeField] private GameObject chestPrefab;
    [SerializeField] private GameObject bossChestPrefab;

    private GridWalkGenerator generator;
    private Transform decorContainer;
    private Transform treeContainer;
    private Transform roomContainer;

    public GridWalkGenerator Generator => generator;
    public GridWalkConfig Config => config;

    private void Start()
    {
        if (generateOnStart)
        {
            int s = config != null && !config.useRandomSeed ? config.seed : System.Environment.TickCount;
            GenerateDungeon(s);
        }
    }

    public void GenerateDungeon(int seed)
    {
        if (config == null)
        {
            Debug.LogError("[GridWalk] GridWalkConfig не назначен!");
            return;
        }

        if (gridWalkRenderer == null)
        {
            gridWalkRenderer = FindAnyObjectByType<GridWalkRenderer>();
            if (gridWalkRenderer == null)
            {
                Debug.LogError("[GridWalk] GridWalkRenderer не найден на сцене!");
                return;
            }
        }

        generator = new GridWalkGenerator(config, seed);
        generator.Generate();
        gridWalkRenderer.RenderDungeon(generator, config);

        // Спавним декорации
        SpawnVolcBubbles();
        SpawnDeadTrees();

        // Запекаем NavMesh после всех объектов
        BakeNavMesh();

        // Инициализируем спавнер (мобы спавнятся по входу игрока в комнату через RoomController)
        if (Mirror.NetworkServer.active)
        {
            int playerCount = Mathf.Max(1, Mirror.NetworkServer.connections.Count);
            mobSpawner?.Initialize(seed, playerCount);
        }
        SetupRoomControllers();
    }

    public void RegenerateDungeon(int seed)
    {
        GenerateDungeon(seed);
    }

    private void SpawnVolcBubbles()
    {
        if (volcBubblePrefabs == null || volcBubblePrefabs.Length == 0) return;

        // Удаляем старые декорации при регенерации
        if (decorContainer != null)
            Destroy(decorContainer.gameObject);

        decorContainer = new GameObject("VolcBubbles").transform;

        var random = new System.Random(generator.Seed);
        var groundPositions = gridWalkRenderer.GroundPositions;
        int worldW = config.WorldWidth;
        int worldH = config.WorldHeight;

        for (int x = 0; x < worldW; x++)
        {
            for (int y = 0; y < worldH; y++)
            {
                // Только на лаве (не на полу)
                if (groundPositions.Contains(new Vector2Int(x, y)))
                    continue;

                if (random.NextDouble() < bubbleDensity)
                {
                    var prefab = volcBubblePrefabs[random.Next(volcBubblePrefabs.Length)];
                    // Смещаем на 0.5 чтобы попасть в центр тайла
                    var pos = new Vector3(x + 0.5f, y + 0.5f, 0);
                    Instantiate(prefab, pos, Quaternion.identity, decorContainer);
                }
            }
        }

        Debug.Log($"[GridWalk] Заспавнено пузырьков: {decorContainer.childCount}");
    }

    private void SpawnDeadTrees()
    {
        if (deadTreePrefabs == null || deadTreePrefabs.Length == 0) return;

        if (treeContainer != null)
            Destroy(treeContainer.gameObject);

        treeContainer = new GameObject("DeadTrees").transform;

        var random = new System.Random(generator.Seed + 1);

        foreach (var cell in generator.Graph.cells)
        {
            // Не спавним в стартовой комнате
            if (cell.roomType == RoomType.Start) continue;

            int placed = 0;
            int attempts = 0;

            while (placed < treesPerRoom && attempts < treesPerRoom * 10)
            {
                attempts++;

                // Случайная позиция внутри комнаты (с отступом от стен)
                int x = random.Next(cell.roomOrigin.x + 1, cell.roomOrigin.x + cell.roomSize.x - 1);
                int y = random.Next(cell.roomOrigin.y + 1, cell.roomOrigin.y + cell.roomSize.y - 1);

                // Проверяем что тайл — пол (может быть вырезан модификацией формы)
                if (!cell.floorTiles.Contains(new Vector2Int(x, y))) continue;

                var prefab = deadTreePrefabs[random.Next(deadTreePrefabs.Length)];
                var pos = new Vector3(x + 0.5f, y + 0.5f, 0);
                Instantiate(prefab, pos, Quaternion.identity, treeContainer);
                placed++;
            }
        }

        Debug.Log($"[GridWalk] Заспавнено деревьев: {treeContainer.childCount}");
    }

    private void SetupRoomControllers()
    {
        // Очистка старых контроллеров при регенерации
        RoomController.ClearRegistry();
        if (roomContainer != null)
            Destroy(roomContainer.gameObject);
        roomContainer = new GameObject("RoomControllers").transform;

        int halfWidth = config.corridorWidth / 2;
        var graph = generator.Graph;

        for (int i = 0; i < graph.cells.Count; i++)
        {
            var cell = graph.cells[i];

            // Сокровищница — спавним сундук на сервере
            if (cell.roomType == RoomType.Treasure && Mirror.NetworkServer.active && chestPrefab != null)
            {
                var chestObj = Instantiate(chestPrefab, cell.RoomCenter, Quaternion.identity, roomContainer);
                Mirror.NetworkServer.Spawn(chestObj);
            }

            // RoomController только для комнат с мобами
            if (cell.roomType != RoomType.Normal && cell.roomType != RoomType.Boss)
                continue;

            var go = new GameObject($"Room_{i}_{cell.roomType}");
            go.transform.position = cell.RoomCenter;
            go.transform.SetParent(roomContainer);

            var rc = go.AddComponent<RoomController>();
            rc.Init(i, cell, graph, halfWidth, mobSpawner, bossChestPrefab);
        }

        Debug.Log($"[GridWalk] RoomControllers созданы: {roomContainer.childCount}");
    }

    private void BakeNavMesh()
    {
        if (navMeshSurface == null)
        {
            navMeshSurface = FindAnyObjectByType<NavMeshSurface>();
            if (navMeshSurface == null)
            {
                Debug.LogWarning("[GridWalk] NavMeshSurface не найден на сцене, навигация не запечена");
                return;
            }
        }

        navMeshSurface.BuildNavMesh();
        Debug.Log("[GridWalk] NavMesh запечён");
    }

    private void PrintDungeonInfo()
    {
        var graph = generator.Graph;
        Debug.Log($"[GridWalk] === Информация о подземелье ===");
        Debug.Log($"[GridWalk] Всего комнат: {graph.cells.Count}, рёбер: {graph.edges.Count}");

        int startCount = 0, normalCount = 0, bossCount = 0, treasureCount = 0, shopCount = 0;
        foreach (var cell in graph.cells)
        {
            switch (cell.roomType)
            {
                case RoomType.Start: startCount++; break;
                case RoomType.Normal: normalCount++; break;
                case RoomType.Boss: bossCount++; break;
                case RoomType.Treasure: treasureCount++; break;
                case RoomType.Shop: shopCount++; break;
            }
        }

        Debug.Log($"[GridWalk] Стартовых: {startCount}, Обычных: {normalCount}, " +
                  $"Босс: {bossCount}, Сокровищниц: {treasureCount}, Магазинов: {shopCount}");

        foreach (var cell in graph.cells)
        {
            Debug.Log($"[GridWalk]   {cell}");
        }
    }
}
