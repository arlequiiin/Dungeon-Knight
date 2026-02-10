using UnityEngine;
using System.Collections.Generic;

public class BSPGenerator
{
    private DungeonConfig config;
    private System.Random random;
    private BSPNode bspRoot;
    private List<Room> rooms = new List<Room>();
    private List<Corridor> corridors = new List<Corridor>();
    private HashSet<(int, int)> connectedRoomPairs = new HashSet<(int, int)>();

    public List<Room> Rooms => rooms;
    public List<Corridor> Corridors => corridors;

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

        Debug.Log($"Seed: {config.seed}");
    }

    public void Generate()
    {
        rooms.Clear();
        corridors.Clear();
        connectedRoomPairs.Clear();

        Debug.Log("Начало BSP разбиения");
        bspRoot = new BSPNode(new RectInt(0, 0, config.mapWidth, config.mapHeight));
        RecursiveBSPSplit(bspRoot);

        Debug.Log("Создание комнат BSP");
        CreateRoomsFromLeaves(bspRoot);

        Debug.Log("Построение графа комнат");
        BuildRoomGraph();

        Debug.Log($"Готово, всего комнат: {rooms.Count}, коридоров: {corridors.Count}, ширина коридора: {config.corridorWidth}");
    }

    // Рекурсивное разбиение пространства BSP
    private void RecursiveBSPSplit(BSPNode node, int depth = 0)
    {
        // Минимальный размер области = размер комнаты + отступы
        int minWidth = config.minRoomSize.x + 6;
        int minHeight = config.minRoomSize.y + 6;

        // Ограничение: размер области и глубина рекурсии
        if (node.rect.width < minWidth * 2 || node.rect.height < minHeight * 2 || depth >= config.bspDepth)
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

    // Создание комнат BSP дерева
    private void CreateRoomsFromLeaves(BSPNode node)
    {
        if (node == null) return;

        if (node.IsLeaf)
        {
            // Создаём комнату размером меньше узла, оставляя стены
            int maxWidth = Mathf.Min(config.maxRoomSize.x, node.rect.width - 2);
            int maxHeight = Mathf.Min(config.maxRoomSize.y, node.rect.height - 2);

            // Проверка на минимальный размер
            if (maxWidth < config.minRoomSize.x || maxHeight < config.minRoomSize.y)
            {
                Debug.LogWarning($"Узел слишком мал для комнаты: {node.rect}");
                return;
            }

            int roomWidth = random.Next(config.minRoomSize.x, maxWidth + 1);
            int roomHeight = random.Next(config.minRoomSize.y, maxHeight + 1);

            // Позиция комнаты в пределах узла с отступом
            int xRange = Mathf.Max(1, node.rect.width - roomWidth - 2);
            int yRange = Mathf.Max(1, node.rect.height - roomHeight - 2);

            int roomX = node.rect.x + 1 + random.Next(xRange);
            int roomY = node.rect.y + 1 + random.Next(yRange);

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

    // Построение графа комнат и распределение типов
    private void BuildRoomGraph()
    {
        if (rooms.Count < 2)
        {
            Debug.LogError("Слишком мало комнат для создания графа!");
            return;
        }

        // Соединяем соседние комнаты в BSP дереве (корридорами)
        ConnectBSPRooms(bspRoot); // Пока заглушка

        // Распределяем типы комнат
        AssignRoomTypes();

        // Вычисляем расстояние до босса
        CalculateDistancesToBoss();
    }

    // Соединение соседних комнат в BSP дереве
    private void ConnectBSPRooms(BSPNode node)
    {
        if (node == null || node.IsLeaf) return;

        // Получаем ВСЕ комнаты из поддеревьев
        List<Room> leftRooms = new List<Room>();
        List<Room> rightRooms = new List<Room>();
        GetAllRooms(node.left, leftRooms);
        GetAllRooms(node.right, rightRooms);

        // Находим ближайшую пару комнат из двух поддеревьев
        if (leftRooms.Count > 0 && rightRooms.Count > 0)
        {
            Room closestLeft = null;
            Room closestRight = null;
            float minDistance = float.MaxValue;

            foreach (var left in leftRooms)
            {
                foreach (var right in rightRooms)
                {
                    float dist = Vector2.Distance(left.center, right.center);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        closestLeft = left;
                        closestRight = right;
                    }
                }
            }

            if (closestLeft != null && closestRight != null)
            {
                CreateCorridor(closestLeft, closestRight);
            }
        }

        ConnectBSPRooms(node.left);
        ConnectBSPRooms(node.right);
    }

    // Собирает все комнаты из поддерева
    private void GetAllRooms(BSPNode node, List<Room> roomList)
    {
        if (node == null) return;

        if (node.IsLeaf && node.room != null)
        {
            roomList.Add(node.room);
        }
        else
        {
            GetAllRooms(node.left, roomList);
            GetAllRooms(node.right, roomList);
        }
    }

    // Создание коридора между двумя комнатами через ближайшие точки границ
    private void CreateCorridor(Room roomA, Room roomB)
    {
        // Проверяем, не соединены ли комнаты уже
        var pairKey = (Mathf.Min(roomA.nodeId, roomB.nodeId), Mathf.Max(roomA.nodeId, roomB.nodeId));
        if (connectedRoomPairs.Contains(pairKey))
            return;
        connectedRoomPairs.Add(pairKey);

        var corridor = new Corridor(roomA, roomB);

        // Проверяем перекрытие по осям
        int overlapMinY = Mathf.Max(roomA.bounds.y, roomB.bounds.y);
        int overlapMaxY = Mathf.Min(roomA.bounds.y + roomA.bounds.height, roomB.bounds.y + roomB.bounds.height);
        int overlapMinX = Mathf.Max(roomA.bounds.x, roomB.bounds.x);
        int overlapMaxX = Mathf.Min(roomA.bounds.x + roomA.bounds.width, roomB.bounds.x + roomB.bounds.width);

        int halfWidth = config.corridorWidth / 2;

        if (overlapMinY < overlapMaxY - config.corridorWidth)
        {
            // Есть перекрытие по Y - прямой горизонтальный коридор
            int y = (overlapMinY + overlapMaxY) / 2;
            int x1 = roomA.bounds.x < roomB.bounds.x
                ? roomA.bounds.x + roomA.bounds.width - 1
                : roomA.bounds.x;
            int x2 = roomA.bounds.x < roomB.bounds.x
                ? roomB.bounds.x
                : roomB.bounds.x + roomB.bounds.width - 1;

            AddLineToSet(corridor.tiles, x1, x2, y, true, halfWidth);
        }
        else if (overlapMinX < overlapMaxX - config.corridorWidth)
        {
            // Есть перекрытие по X - прямой вертикальный коридор
            int x = (overlapMinX + overlapMaxX) / 2;
            int y1 = roomA.bounds.y < roomB.bounds.y
                ? roomA.bounds.y + roomA.bounds.height - 1
                : roomA.bounds.y;
            int y2 = roomA.bounds.y < roomB.bounds.y
                ? roomB.bounds.y
                : roomB.bounds.y + roomB.bounds.height - 1;

            AddLineToSet(corridor.tiles, y1, y2, x, false, halfWidth);
        }
        else
        {
            // Нет перекрытия - L-образный коридор
            GetClosestBorderPoints(roomA, roomB, out Vector2Int pointA, out Vector2Int pointB);

            // Пробуем оба варианта и выбираем лучший
            var variant1 = new HashSet<Vector2Int>();
            var variant2 = new HashSet<Vector2Int>();
            AddLinesToSet(variant1, pointA, pointB, true);
            AddLinesToSet(variant2, pointA, pointB, false);

            int score1 = CountTilesNearRooms(variant1, roomA, roomB);
            int score2 = CountTilesNearRooms(variant2, roomA, roomB);

            foreach (var tile in score1 <= score2 ? variant1 : variant2)
                corridor.tiles.Add(tile);
        }

        corridors.Add(corridor);
    }

    // Находит ближайшие точки на границах двух комнат
    private void GetClosestBorderPoints(Room roomA, Room roomB, out Vector2Int pointA, out Vector2Int pointB)
    {
        // Определяем взаимное расположение комнат
        bool aLeftOfB = roomA.bounds.x + roomA.bounds.width <= roomB.bounds.x;
        bool aRightOfB = roomA.bounds.x >= roomB.bounds.x + roomB.bounds.width;
        bool aAboveB = roomA.bounds.y >= roomB.bounds.y + roomB.bounds.height;
        bool aBelowB = roomA.bounds.y + roomA.bounds.height <= roomB.bounds.y;

        // Находим перекрытие по осям для определения точки соединения
        int overlapMinX = Mathf.Max(roomA.bounds.x, roomB.bounds.x);
        int overlapMaxX = Mathf.Min(roomA.bounds.x + roomA.bounds.width, roomB.bounds.x + roomB.bounds.width);
        int overlapMinY = Mathf.Max(roomA.bounds.y, roomB.bounds.y);
        int overlapMaxY = Mathf.Min(roomA.bounds.y + roomA.bounds.height, roomB.bounds.y + roomB.bounds.height);

        if (aLeftOfB)
        {
            // A слева от B - соединяем правую границу A с левой границей B
            int yA = GetOverlapCenter(overlapMinY, overlapMaxY, roomA.bounds.y, roomA.bounds.y + roomA.bounds.height);
            int yB = GetOverlapCenter(overlapMinY, overlapMaxY, roomB.bounds.y, roomB.bounds.y + roomB.bounds.height);
            pointA = new Vector2Int(roomA.bounds.x + roomA.bounds.width - 1, yA);
            pointB = new Vector2Int(roomB.bounds.x, yB);
        }
        else if (aRightOfB)
        {
            // A справа от B
            int yA = GetOverlapCenter(overlapMinY, overlapMaxY, roomA.bounds.y, roomA.bounds.y + roomA.bounds.height);
            int yB = GetOverlapCenter(overlapMinY, overlapMaxY, roomB.bounds.y, roomB.bounds.y + roomB.bounds.height);
            pointA = new Vector2Int(roomA.bounds.x, yA);
            pointB = new Vector2Int(roomB.bounds.x + roomB.bounds.width - 1, yB);
        }
        else if (aBelowB)
        {
            // A ниже B - соединяем верхнюю границу A с нижней границей B
            int xA = GetOverlapCenter(overlapMinX, overlapMaxX, roomA.bounds.x, roomA.bounds.x + roomA.bounds.width);
            int xB = GetOverlapCenter(overlapMinX, overlapMaxX, roomB.bounds.x, roomB.bounds.x + roomB.bounds.width);
            pointA = new Vector2Int(xA, roomA.bounds.y + roomA.bounds.height - 1);
            pointB = new Vector2Int(xB, roomB.bounds.y);
        }
        else if (aAboveB)
        {
            // A выше B
            int xA = GetOverlapCenter(overlapMinX, overlapMaxX, roomA.bounds.x, roomA.bounds.x + roomA.bounds.width);
            int xB = GetOverlapCenter(overlapMinX, overlapMaxX, roomB.bounds.x, roomB.bounds.x + roomB.bounds.width);
            pointA = new Vector2Int(xA, roomA.bounds.y);
            pointB = new Vector2Int(xB, roomB.bounds.y + roomB.bounds.height - 1);
        }
        else
        {
            // Комнаты перекрываются или диагонально - используем центры
            pointA = new Vector2Int((int)roomA.center.x, (int)roomA.center.y);
            pointB = new Vector2Int((int)roomB.center.x, (int)roomB.center.y);
        }
    }

    // Получить центр перекрытия или центр комнаты если перекрытия нет
    private int GetOverlapCenter(int overlapMin, int overlapMax, int roomMin, int roomMax)
    {
        if (overlapMin < overlapMax)
        {
            // Есть перекрытие - берём его центр
            return (overlapMin + overlapMax) / 2;
        }
        // Нет перекрытия - берём центр комнаты
        return (roomMin + roomMax) / 2;
    }

    // Добавляет L-коридор в набор тайлов
    private void AddLinesToSet(HashSet<Vector2Int> tiles, Vector2Int pointA, Vector2Int pointB, bool horizontalFirst)
    {
        int halfWidth = config.corridorWidth / 2;

        if (horizontalFirst)
        {
            // Сначала горизонталь, потом вертикаль
            AddLineToSet(tiles, pointA.x, pointB.x, pointA.y, true, halfWidth);
            AddLineToSet(tiles, pointA.y, pointB.y, pointB.x, false, halfWidth);
        }
        else
        {
            // Сначала вертикаль, потом горизонталь
            AddLineToSet(tiles, pointA.y, pointB.y, pointA.x, false, halfWidth);
            AddLineToSet(tiles, pointA.x, pointB.x, pointB.y, true, halfWidth);
        }
    }

    // Добавляет линию в набор тайлов
    private void AddLineToSet(HashSet<Vector2Int> tiles, int from, int to, int fixedCoord, bool horizontal, int halfWidth)
    {
        int min = Mathf.Min(from, to);
        int max = Mathf.Max(from, to);

        for (int i = min; i <= max; i++)
        {
            for (int offset = -halfWidth; offset <= halfWidth; offset++)
            {
                if (horizontal)
                    tiles.Add(new Vector2Int(i, fixedCoord + offset));
                else
                    tiles.Add(new Vector2Int(fixedCoord + offset, i));
            }
        }
    }

    // Считает количество тайлов коридора, которые находятся рядом с другими комнатами
    private int CountTilesNearRooms(HashSet<Vector2Int> tiles, Room exceptA, Room exceptB)
    {
        int count = 0;
        int margin = 2; // проверяем близость на 2 тайла

        foreach (var tile in tiles)
        {
            foreach (var room in rooms)
            {
                if (room == exceptA || room == exceptB)
                    continue;

                // Проверяем, находится ли тайл рядом с комнатой
                if (tile.x >= room.bounds.x - margin && tile.x < room.bounds.x + room.bounds.width + margin &&
                    tile.y >= room.bounds.y - margin && tile.y < room.bounds.y + room.bounds.height + margin)
                {
                    count++;
                    break; // считаем тайл только один раз
                }
            }
        }

        return count;
    }

    // Добавить горизонтальную линию коридора (с учётом ширины)
    private void AddHorizontalLine(Corridor corridor, int x1, int x2, int y)
    {
        int minX = Mathf.Min(x1, x2);
        int maxX = Mathf.Max(x1, x2);
        int halfWidth = config.corridorWidth / 2;

        for (int x = minX; x <= maxX; x++)
        {
            for (int offset = -halfWidth; offset <= halfWidth; offset++)
            {
                corridor.tiles.Add(new Vector2Int(x, y + offset));
            }
        }
    }

    // Добавить вертикальную линию коридора (с учётом ширины)
    private void AddVerticalLine(Corridor corridor, int y1, int y2, int x)
    {
        int minY = Mathf.Min(y1, y2);
        int maxY = Mathf.Max(y1, y2);
        int halfWidth = config.corridorWidth / 2;

        for (int y = minY; y <= maxY; y++)
        {
            for (int offset = -halfWidth; offset <= halfWidth; offset++)
            {
                corridor.tiles.Add(new Vector2Int(x + offset, y));
            }
        }
    }

    // Получить главную комнату из поддерева
    private Room GetMainRoom(BSPNode node)
    {
        if (node == null) return null;
        if (node.IsLeaf) return node.room;

        // Для промежуточных узлов берём одну из подкомнат
        Room leftRoom = GetMainRoom(node.left);
        return leftRoom ?? GetMainRoom(node.right);
    }

    // Распределение типов комнат
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

    // Вычисление расстояния до комнаты босса 
    private void CalculateDistancesToBoss()
    {
        // используем индекс как приблизительное расстояние
        Room bossRoom = rooms[rooms.Count - 1];
        for (int i = 0; i < rooms.Count; i++)
        {
            rooms[i].distanceToBoss = Mathf.Abs(rooms.Count - 1 - i);
        }
    }

    /// Получить случайную комнату
    public Room GetRandomRoom(RoomType type = RoomType.Normal)
    {
        var filtered = rooms.FindAll(r => r.type == type);
        if (filtered.Count == 0) return null;
        return filtered[random.Next(filtered.Count)];
    }
}
