using UnityEngine;
using System.Collections.Generic;

public class GridWalkGenerator
{
    private GridWalkConfig config;
    private System.Random random;
    private DungeonGraph graph;

    // Сетка: gridPos -> CellData (только активные)
    private Dictionary<Vector2Int, CellData> activeCells = new Dictionary<Vector2Int, CellData>();

    // Направления: вверх, вниз, влево, вправо
    private static readonly Vector2Int[] Directions = {
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
    };

    public DungeonGraph Graph => graph;
    public CellData StartCell { get; private set; }
    public CellData BossCell { get; private set; }

    public GridWalkGenerator(GridWalkConfig config)
    {
        this.config = config;
        config.ValidateAndFix();

        if (config.useRandomSeed)
            config.seed = System.Environment.TickCount;

        random = new System.Random(config.seed);
        Debug.Log($"[GridWalk] Seed: {config.seed}");
    }

    public void Generate()
    {
        graph = new DungeonGraph();
        activeCells.Clear();

        // Шаг 1: Random Walk по сетке
        RandomWalk();

        // Шаг 2: Добавление петель
        AddExtraEdges();

        // Шаг 3: Генерация размеров комнат
        GenerateRoomSizes();

        // Шаг 4: Модификация формы
        ModifyRoomShapes();

        // Шаг 7: Назначение ролей
        AssignRoomRoles();

        Debug.Log($"[GridWalk] Готово: {graph.cells.Count} комнат, {graph.edges.Count} рёбер");
    }

    // ==================== Шаг 1: Random Walk ====================
    private void RandomWalk()
    {
        // Стартовая ячейка ближе к центру сетки
        Vector2Int startGrid = new Vector2Int(config.gridSize.x / 2, config.gridSize.y / 2);
        CellData startCell = CreateCell(startGrid);
        Vector2Int current = startGrid;

        int stepsWithoutNewRoom = 0;

        while (graph.cells.Count < config.roomCount)
        {
            // Защита от зацикливания
            if (stepsWithoutNewRoom >= 20)
            {
                // Телепортируемся к случайной активной ячейке
                var cellList = new List<CellData>(activeCells.Values);
                var teleportTarget = cellList[random.Next(cellList.Count)];
                current = teleportTarget.gridPos;
                stepsWithoutNewRoom = 0;
            }

            // Выбираем случайное направление
            Vector2Int dir = Directions[random.Next(Directions.Length)];
            Vector2Int next = current + dir;

            // Проверяем границы сетки
            if (next.x < 0 || next.x >= config.gridSize.x || next.y < 0 || next.y >= config.gridSize.y)
            {
                stepsWithoutNewRoom++;
                continue;
            }

            // Переходим
            bool isNew = !activeCells.ContainsKey(next);
            if (isNew)
            {
                CreateCell(next);
                // Ребро только при первом посещении — чтобы walk не создавал лишних связей
                graph.AddEdge(activeCells[current], activeCells[next]);
                stepsWithoutNewRoom = 0;
            }
            else
            {
                stepsWithoutNewRoom++;
            }

            current = next;
        }

        // Запоминаем стартовую ячейку
        StartCell = activeCells[startGrid];
    }

    private CellData CreateCell(Vector2Int gridPos)
    {
        Vector2Int worldOrigin = new Vector2Int(
            gridPos.x * (config.cellSize.x + config.cellGap),
            gridPos.y * (config.cellSize.y + config.cellGap)
        );

        var cell = new CellData(gridPos, worldOrigin);
        activeCells[gridPos] = cell;
        graph.cells.Add(cell);
        return cell;
    }

    // ==================== Шаг 2: Добавление петель ====================
    private void AddExtraEdges()
    {
        var candidates = new List<(CellData, CellData)>();

        foreach (var cell in graph.cells)
        {
            foreach (var dir in Directions)
            {
                Vector2Int neighborPos = cell.gridPos + dir;
                if (activeCells.TryGetValue(neighborPos, out CellData neighbor))
                {
                    if (!graph.HasEdge(cell, neighbor))
                    {
                        candidates.Add((cell, neighbor));
                    }
                }
            }
        }

        // Убираем дубли (A,B) и (B,A)
        var uniqueCandidates = new List<(CellData, CellData)>();
        var seen = new HashSet<(Vector2Int, Vector2Int)>();
        foreach (var (a, b) in candidates)
        {
            var key = a.gridPos.x < b.gridPos.x || (a.gridPos.x == b.gridPos.x && a.gridPos.y < b.gridPos.y)
                ? (a.gridPos, b.gridPos) : (b.gridPos, a.gridPos);
            if (seen.Add(key))
                uniqueCandidates.Add((a, b));
        }

        // Перемешиваем и берём extraEdges штук
        Shuffle(uniqueCandidates);
        int toAdd = Mathf.Min(config.extraEdges, uniqueCandidates.Count);
        for (int i = 0; i < toAdd; i++)
        {
            var (a, b) = uniqueCandidates[i];
            graph.AddEdge(a, b);
        }
    }

    // ==================== Шаг 3: Генерация размеров комнат ====================
    private void GenerateRoomSizes()
    {
        foreach (var cell in graph.cells)
        {
            int w = random.Next(config.roomMinSize.x, config.roomMaxSize.x + 1);
            int h = random.Next(config.roomMinSize.y, config.roomMaxSize.y + 1);
            cell.roomSize = new Vector2Int(w, h);

            // Центрируем комнату внутри ячейки
            cell.roomOrigin = new Vector2Int(
                cell.worldOrigin.x + (config.cellSize.x - w) / 2,
                cell.worldOrigin.y + (config.cellSize.y - h) / 2
            );

            // Заполняем прямоугольник тайлами пола
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    cell.floorTiles.Add(new Vector2Int(cell.roomOrigin.x + x, cell.roomOrigin.y + y));
                }
            }
        }
    }

    // ==================== Шаг 4: Модификация формы ====================
    private void ModifyRoomShapes()
    {
        foreach (var cell in graph.cells)
        {
            if (random.NextDouble() > config.shapeModifyChance)
                continue;

            // 50% вырез угла, 50% выступ
            if (random.Next(2) == 0)
                CutCorner(cell);
            else
                AddProtrusion(cell);
        }
    }

    private void CutCorner(CellData cell)
    {
        int cutW = random.Next(2, 5); // 2-4
        int cutH = random.Next(2, 5);

        // Не вырезаем больше трети стороны
        cutW = Mathf.Min(cutW, cell.roomSize.x / 3);
        cutH = Mathf.Min(cutH, cell.roomSize.y / 3);
        if (cutW < 2 || cutH < 2) return;

        int corner = random.Next(4); // 0=BL, 1=BR, 2=TL, 3=TR

        int startX, startY;
        switch (corner)
        {
            case 0: // нижний-левый
                startX = cell.roomOrigin.x;
                startY = cell.roomOrigin.y;
                break;
            case 1: // нижний-правый
                startX = cell.roomOrigin.x + cell.roomSize.x - cutW;
                startY = cell.roomOrigin.y;
                break;
            case 2: // верхний-левый
                startX = cell.roomOrigin.x;
                startY = cell.roomOrigin.y + cell.roomSize.y - cutH;
                break;
            default: // верхний-правый
                startX = cell.roomOrigin.x + cell.roomSize.x - cutW;
                startY = cell.roomOrigin.y + cell.roomSize.y - cutH;
                break;
        }

        for (int x = startX; x < startX + cutW; x++)
            for (int y = startY; y < startY + cutH; y++)
                cell.floorTiles.Remove(new Vector2Int(x, y));
    }

    private void AddProtrusion(CellData cell)
    {
        int protW = random.Next(3, 6); // 3-5
        int protH = random.Next(2, 4); // 2-3

        int side = random.Next(4); // 0=лево, 1=право, 2=низ, 3=верх

        int startX, startY, sizeX, sizeY;
        switch (side)
        {
            case 0: // левая сторона
                sizeX = protH; sizeY = protW;
                startX = cell.roomOrigin.x - sizeX;
                startY = cell.roomOrigin.y + random.Next(0, Mathf.Max(1, cell.roomSize.y - sizeY));
                break;
            case 1: // правая сторона
                sizeX = protH; sizeY = protW;
                startX = cell.roomOrigin.x + cell.roomSize.x;
                startY = cell.roomOrigin.y + random.Next(0, Mathf.Max(1, cell.roomSize.y - sizeY));
                break;
            case 2: // нижняя сторона
                sizeX = protW; sizeY = protH;
                startX = cell.roomOrigin.x + random.Next(0, Mathf.Max(1, cell.roomSize.x - sizeX));
                startY = cell.roomOrigin.y - sizeY;
                break;
            default: // верхняя сторона
                sizeX = protW; sizeY = protH;
                startX = cell.roomOrigin.x + random.Next(0, Mathf.Max(1, cell.roomSize.x - sizeX));
                startY = cell.roomOrigin.y + cell.roomSize.y;
                break;
        }

        // Проверяем что выступ не выходит за пределы ячейки
        int cellEndX = cell.worldOrigin.x + config.cellSize.x;
        int cellEndY = cell.worldOrigin.y + config.cellSize.y;

        if (startX < cell.worldOrigin.x || startX + sizeX > cellEndX) return;
        if (startY < cell.worldOrigin.y || startY + sizeY > cellEndY) return;

        for (int x = startX; x < startX + sizeX; x++)
            for (int y = startY; y < startY + sizeY; y++)
                cell.floorTiles.Add(new Vector2Int(x, y));
    }

    // ==================== Шаг 7: Назначение ролей ====================
    private void AssignRoomRoles()
    {
        // Стартовая комната
        StartCell.roomType = RoomType.Start;

        // Комната босса — самая дальняя по BFS от старта
        BossCell = graph.FindFarthestCell(StartCell);
        BossCell.roomType = RoomType.Boss;

        // Листья графа (1 сосед) → сокровищницы
        var leaves = graph.FindLeaves(StartCell, BossCell);
        foreach (var leaf in leaves)
            leaf.roomType = RoomType.Treasure;

        // Остальные — Normal (уже по умолчанию)
    }

    // ==================== Утилиты ====================
    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
