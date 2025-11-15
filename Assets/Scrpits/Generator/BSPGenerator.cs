using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Генератор подземелья с использованием Binary Space Partition
/// </summary>
public class BSPGenerator
{
    private DungeonConfig config;
    private System.Random random;
    private BSPNode bspRoot;
    private List<Room> rooms = new List<Room>();

    public List<Room> Rooms => rooms;

    public BSPGenerator(DungeonConfig config)
    {
        this.config = config;
        config.ValidateAndFix();

        // Инициализируем seed
        if (config.useRandomSeed)
        {
            config.seed = System.Environment.TickCount;
        }
        random = new System.Random(config.seed);

        Debug.Log($"[DungeonGenerator] Seed: {config.seed}");
    }

    /// <summary>
    /// Основной метод генерации подземелья
    /// </summary>
    public void Generate()
    {
        rooms.Clear();

        // Шаг 1: BSP разбиение пространства
        Debug.Log("[DungeonGenerator] Начало BSP разбиения...");
        bspRoot = new BSPNode(new RectInt(0, 0, config.mapWidth, config.mapHeight));
        RecursiveBSPSplit(bspRoot);

        // Шаг 2: Создание комнат для каждого листа BSP
        Debug.Log("[DungeonGenerator] Создание комнат из листьев BSP...");
        CreateRoomsFromLeaves(bspRoot);

        // Шаг 3: Построение графа и распределение типов комнат
        Debug.Log("[DungeonGenerator] Построение графа комнат...");
        BuildRoomGraph();

        Debug.Log($"[DungeonGenerator] Генерация завершена. Всего комнат: {rooms.Count}");
    }

    /// <summary>
    /// Рекурсивное разбиение пространства BSP
    /// </summary>
    private void RecursiveBSPSplit(BSPNode node, int depth = 0)
    {
        // Критерии остановки
        int minWidth = config.minRoomSize.x + 2;
        int minHeight = config.minRoomSize.y + 2;

        // Не разбиваем слишком маленькие комнаты и слишком глубокие уровни
        if (node.rect.width <= minWidth * 2 || node.rect.height <= minHeight * 2 || depth > 8)
        {
            return;
        }

        bool splitVertically = random.Next(2) == 0;

        if (splitVertically)
        {
            // Вертикальное разбиение (по X)
            int splitX = random.Next(
                minWidth,
                node.rect.width - minWidth
            ) + node.rect.x;

            node.left = new BSPNode(new RectInt(
                node.rect.x,
                node.rect.y,
                splitX - node.rect.x,
                node.rect.height
            ));

            node.right = new BSPNode(new RectInt(
                splitX,
                node.rect.y,
                node.rect.x + node.rect.width - splitX,
                node.rect.height
            ));
        }
        else
        {
            // Горизонтальное разбиение (по Y)
            int splitY = random.Next(
                minHeight,
                node.rect.height - minHeight
            ) + node.rect.y;

            node.left = new BSPNode(new RectInt(
                node.rect.x,
                node.rect.y,
                node.rect.width,
                splitY - node.rect.y
            ));

            node.right = new BSPNode(new RectInt(
                node.rect.x,
                splitY,
                node.rect.width,
                node.rect.y + node.rect.height - splitY
            ));
        }

        // Рекурсивно разбиваем потомков
        RecursiveBSPSplit(node.left, depth + 1);
        RecursiveBSPSplit(node.right, depth + 1);
    }

    /// <summary>
    /// Создание комнат из листьев BSP дерева
    /// </summary>
    private void CreateRoomsFromLeaves(BSPNode node)
    {
        if (node == null) return;

        if (node.IsLeaf)
        {
            // Создаём комнату размером меньше узла, оставляя "стены"
            int roomWidth = random.Next(
                config.minRoomSize.x,
                Mathf.Min(config.maxRoomSize.x, node.rect.width - 2) + 1
            );
            int roomHeight = random.Next(
                config.minRoomSize.y,
                Mathf.Min(config.maxRoomSize.y, node.rect.height - 2) + 1
            );

            // Позиция комнаты в пределах узла с отступом
            int roomX = node.rect.x + 1 + random.Next(node.rect.width - roomWidth - 2);
            int roomY = node.rect.y + 1 + random.Next(node.rect.height - roomHeight - 2);

            var room = new Room(RoomType.Normal, new RectInt(roomX, roomY, roomWidth, roomHeight));
            room.nodeId = rooms.Count;
            rooms.Add(room);
            node.room = room;
        }
        else
        {
            CreateRoomsFromLeaves(node.left);
            CreateRoomsFromLeaves(node.right);
        }
    }

    /// <summary>
    /// Построение графа комнат и распределение типов
    /// </summary>
    private void BuildRoomGraph()
    {
        if (rooms.Count < 2)
        {
            Debug.LogError("Слишком мало комнат для создания графа!");
            return;
        }

        // Соединяем соседние комнаты в BSP дереве (корридорами)
        ConnectBSPRooms(bspRoot);

        // Распределяем типы комнат
        AssignRoomTypes();

        // Вычисляем расстояние до босса
        CalculateDistancesToBoss();
    }

    /// <summary>
    /// Соединение соседних комнат в BSP дереве
    /// </summary>
    private void ConnectBSPRooms(BSPNode node)
    {
        if (node == null || node.IsLeaf) return;

        // Получаем комнаты из поддеревьев
        Room leftRoom = GetMainRoom(node.left);
        Room rightRoom = GetMainRoom(node.right);

        if (leftRoom != null && rightRoom != null)
        {
            // Создаём логическую связь между комнатами
            // (В будущем здесь будут коридоры)
        }

        ConnectBSPRooms(node.left);
        ConnectBSPRooms(node.right);
    }

    /// <summary>
    /// Получить главную комнату из поддерева
    /// </summary>
    private Room GetMainRoom(BSPNode node)
    {
        if (node == null) return null;
        if (node.IsLeaf) return node.room;

        // Для промежуточных узлов берём одну из подкомнат
        Room leftRoom = GetMainRoom(node.left);
        return leftRoom ?? GetMainRoom(node.right);
    }

    /// <summary>
    /// Распределение типов комнат
    /// </summary>
    private void AssignRoomTypes()
    {
        // Стартовая комната - первая
        rooms[0].type = RoomType.Start;

        // Последняя комната - босс
        rooms[rooms.Count - 1].type = RoomType.Boss;

        // Специальные комнаты
        int specialAssigned = 0;
        for (int i = 1; i < rooms.Count - 1 && specialAssigned < config.specialRoomsCount; i++)
        {
            if (random.NextDouble() < 0.5)
            {
                rooms[i].type = RoomType.Special;
                specialAssigned++;
            }
        }
    }

    /// <summary>
    /// Вычисление расстояния до комнаты босса (простая версия)
    /// </summary>
    private void CalculateDistancesToBoss()
    {
        // В простой версии используем индекс как приблизительное расстояние
        Room bossRoom = rooms[rooms.Count - 1];
        for (int i = 0; i < rooms.Count; i++)
        {
            rooms[i].distanceToBoss = Mathf.Abs(rooms.Count - 1 - i);
        }
    }

    /// <summary>
    /// Получить случайную комнату (для тестирования)
    /// </summary>
    public Room GetRandomRoom(RoomType type = RoomType.Normal)
    {
        var filtered = rooms.FindAll(r => r.type == type);
        if (filtered.Count == 0) return null;
        return filtered[random.Next(filtered.Count)];
    }
}
