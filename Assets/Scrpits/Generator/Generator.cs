using UnityEngine;

/// <summary>
/// Главный класс для управления генерацией и визуализацией подземелья
/// </summary>
public class Generator : MonoBehaviour
{
    [SerializeField] private DungeonConfig dungeonConfig;
    [SerializeField] private DungeonRenderer dungeonRenderer;
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private bool debugMode = true;

    private BSPGenerator bspGenerator;

    private void Start()
    {
        if (generateOnStart)
        {
            GenerateDungeon();
        }
    }

    /// <summary>
    /// Генерирует новое подземелье
    /// </summary>
    public void GenerateDungeon()
    {
        if (dungeonConfig == null)
        {
            dungeonConfig = new DungeonConfig();
            Debug.LogWarning("[Generator] DungeonConfig не назначен, создан новый с параметрами по умолчанию");
        }

        if (dungeonRenderer == null)
        {
            dungeonRenderer = FindObjectOfType<DungeonRenderer>();
            if (dungeonRenderer == null)
            {
                Debug.LogError("[Generator] DungeonRenderer не найден на сцене!");
                return;
            }
        }

        Debug.Log("[Generator] Начинаю генерацию подземелья...");

        // Создаём и запускаем генератор
        bspGenerator = new BSPGenerator(dungeonConfig);
        bspGenerator.Generate();

        // Визуализируем результат
        if (debugMode)
        {
            dungeonRenderer.DebugRenderRooms(bspGenerator);
        }
        else
        {
            dungeonRenderer.RenderDungeon(bspGenerator);
        }

        PrintDungeonInfo();
    }

    /// <summary>
    /// Выводит информацию о сгенерированном подземелье в консоль
    /// </summary>
    private void PrintDungeonInfo()
    {
        Debug.Log("\n=== ИНФОРМАЦИЯ О ПОДЗЕМЕЛЬЕ ===");
        Debug.Log($"Всего комнат: {bspGenerator.Rooms.Count}");

        int startCount = bspGenerator.Rooms.FindAll(r => r.type == RoomType.Start).Count;
        int normalCount = bspGenerator.Rooms.FindAll(r => r.type == RoomType.Normal).Count;
        int specialCount = bspGenerator.Rooms.FindAll(r => r.type == RoomType.Special).Count;
        int bossCount = bspGenerator.Rooms.FindAll(r => r.type == RoomType.Boss).Count;

        Debug.Log($"  - Стартовых: {startCount}");
        Debug.Log($"  - Обычных: {normalCount}");
        Debug.Log($"  - Специальных: {specialCount}");
        Debug.Log($"  - Боссов: {bossCount}");

        if (debugMode)
        {
            Debug.Log("\nКомнаты:");
            for (int i = 0; i < bspGenerator.Rooms.Count; i++)
            {
                var room = bspGenerator.Rooms[i];
                Debug.Log($"  [{i}] {room}");
            }
        }
    }

    /// <summary>
    /// Regenerate dungeon (for editor buttons)
    /// </summary>
    public void RegenerateDungeon()
    {
        GenerateDungeon();
    }
}