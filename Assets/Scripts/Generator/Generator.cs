using UnityEngine;

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

    public void GenerateDungeon()
    {
        if (dungeonConfig == null)
        {
            dungeonConfig = new DungeonConfig();
        }

        if (dungeonRenderer == null)
        {
            dungeonRenderer = FindAnyObjectByType<DungeonRenderer>();
            if (dungeonRenderer == null)
            {
                Debug.LogError("DungeonRenderer не найден на сцене!");
                return;
            }
        }

        bspGenerator = new BSPGenerator(dungeonConfig);
        bspGenerator.Generate();

        if (debugMode)
        {
            dungeonRenderer.DebugRenderRooms(bspGenerator, dungeonConfig);
        }
        else
        {
            dungeonRenderer.RenderDungeon(bspGenerator, dungeonConfig);
        }

        PrintDungeonInfo();
    }

    private void PrintDungeonInfo()
    {
        Debug.Log($"Всего комнат: {bspGenerator.Rooms.Count}");

        int startCount = bspGenerator.Rooms.FindAll(r => r.type == RoomType.Start).Count;
        int normalCount = bspGenerator.Rooms.FindAll(r => r.type == RoomType.Normal).Count;
        int specialCount = bspGenerator.Rooms.FindAll(r => r.type == RoomType.Special).Count;
        int bossCount = bspGenerator.Rooms.FindAll(r => r.type == RoomType.Boss).Count;

        Debug.Log($"Стартовых: {startCount}");
        Debug.Log($"Обычных: {normalCount}");
        Debug.Log($"Специальных: {specialCount}");
        Debug.Log($"Боссов: {bossCount}");

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

    public void RegenerateDungeon()
    {
        GenerateDungeon();
    }
}