using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Контроллер комнаты. Отвечает за:
/// - Обнаружение входа игрока (trigger)
/// - Спавн мобов при входе (сервер)
/// - Запирание/отпирание выходов (двери-коллайдеры)
/// - Отслеживание зачистки комнаты
/// Создаётся на сервере и клиентах во время генерации подземелья.
/// Синхронизация состояния — через RoomStateMessage.
/// </summary>
public class RoomController : MonoBehaviour
{
    public enum RoomState : byte { Idle = 0, Active = 1, Cleared = 2 }

    private RoomState state = RoomState.Idle;
    private CellData cell;
    private DungeonGraph graph;
    private int corridorHalfWidth;
    private MobSpawner mobSpawner;
    private int roomIndex;

    private readonly List<MobHealth> trackedMobs = new();
    private readonly List<GameObject> doorBlockers = new();

    // ── Статический реестр для синхронизации по сети ──
    private static readonly Dictionary<int, RoomController> registry = new();

    public static void ClearRegistry() => registry.Clear();

    /// <summary>
    /// Вызывается клиентом при получении RoomStateMessage от сервера.
    /// </summary>
    public static void OnRoomStateChanged(int roomIndex, byte newState)
    {
        if (!registry.TryGetValue(roomIndex, out var rc)) return;

        if (newState == (byte)RoomState.Active && rc.state == RoomState.Idle)
            rc.LockDoors();
        else if (newState == (byte)RoomState.Cleared && rc.state == RoomState.Active)
            rc.UnlockDoors();

        rc.state = (RoomState)newState;
    }

    public void Init(int index, CellData cell, DungeonGraph graph, int corridorHalfWidth, MobSpawner spawner)
    {
        roomIndex = index;
        this.cell = cell;
        this.graph = graph;
        this.corridorHalfWidth = corridorHalfWidth;
        mobSpawner = spawner;

        // Trigger-коллайдер покрывает всю площадь комнаты
        var col = gameObject.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(cell.roomSize.x, cell.roomSize.y);

        registry[index] = this;
    }

    // ── Обнаружение входа игрока ──

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!NetworkServer.active) return;
        if (state != RoomState.Idle) return;
        if (other.GetComponent<PlayerController>() == null) return;

        ActivateRoom();
    }

    private void ActivateRoom()
    {
        state = RoomState.Active;
        LockDoors();

        // Спавн мобов — только на сервере
        if (NetworkServer.active)
        {
            if (cell.roomType == RoomType.Boss)
            {
                var bossHealth = mobSpawner.SpawnRoomBoss(cell);
                if (bossHealth != null)
                    trackedMobs.Add(bossHealth);
            }
            else
            {
                var mobs = mobSpawner.SpawnRoomMobs(cell);
                if (mobs != null)
                    trackedMobs.AddRange(mobs);
            }

            // Если мобов нет — сразу зачистка
            if (trackedMobs.Count == 0)
            {
                ClearRoom();
                return;
            }

            // Оповещаем клиентов о блокировке
            NetworkServer.SendToAll(new RoomStateMessage
            {
                roomIndex = roomIndex,
                state = (byte)RoomState.Active
            });

            Debug.Log($"[Room] Комната {roomIndex} ({cell.roomType}) активирована! Мобов: {trackedMobs.Count}");
        }
    }

    // ── Отслеживание зачистки ──

    private void Update()
    {
        if (!NetworkServer.active) return;
        if (state != RoomState.Active) return;

        // Проверяем, все ли мобы мертвы
        foreach (var mob in trackedMobs)
        {
            if (mob != null && !mob.IsDead)
                return;
        }

        ClearRoom();
    }

    private void ClearRoom()
    {
        state = RoomState.Cleared;
        UnlockDoors();

        if (NetworkServer.active)
        {
            NetworkServer.SendToAll(new RoomStateMessage
            {
                roomIndex = roomIndex,
                state = (byte)RoomState.Cleared
            });
        }

        Debug.Log($"[Room] === КОМНАТА {roomIndex} ЗАЧИЩЕНА! ({cell.roomType}) ===");
    }

    // ── Двери (блокировщики коллайдерами) ──

    private void LockDoors()
    {
        var doors = ComputeDoorPositions();
        foreach (var door in doors)
        {
            var blocker = new GameObject($"DoorBlocker_{roomIndex}");
            blocker.transform.position = door.center;
            var col = blocker.AddComponent<BoxCollider2D>();
            col.size = door.size;

            // NavMeshObstacle с карвингом — мобы не смогут проложить путь через дверь
            var obstacle = blocker.AddComponent<NavMeshObstacle>();
            obstacle.carving = true;
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.size = new Vector3(door.size.x, door.size.y, 1f);

            doorBlockers.Add(blocker);
        }
    }

    private void UnlockDoors()
    {
        foreach (var blocker in doorBlockers)
        {
            if (blocker != null)
                Destroy(blocker);
        }
        doorBlockers.Clear();
    }

    // ── Вычисление позиций дверей ──

    private struct DoorInfo
    {
        public Vector2 center;
        public Vector2 size;
    }

    /// <summary>
    /// Для каждого коридора, ведущего в эту комнату, вычисляет позицию блокировщика —
    /// один тайл в коридор от стены комнаты, перекрывая всю ширину коридора.
    /// </summary>
    private List<DoorInfo> ComputeDoorPositions()
    {
        var doors = new List<DoorInfo>();
        int tileWidth = corridorHalfWidth * 2 + 1;

        foreach (var (cellA, cellB) in graph.edges)
        {
            CellData other;
            if (cellA == cell) other = cellB;
            else if (cellB == cell) other = cellA;
            else continue;

            Vector2Int diff = other.gridPos - cell.gridPos;

            if (diff.x != 0)
            {
                // Горизонтальное соединение
                int centerY = (int)((cell.RoomCenter.y + other.RoomCenter.y) / 2f);
                int doorX = diff.x > 0
                    ? cell.roomOrigin.x + cell.roomSize.x  // правая стена → первый тайл коридора
                    : cell.roomOrigin.x - 1;                // левая стена → последний тайл коридора

                doors.Add(new DoorInfo
                {
                    center = new Vector2(doorX + 0.5f, centerY + 0.5f),
                    size = new Vector2(1f, tileWidth)
                });
            }
            else if (diff.y != 0)
            {
                // Вертикальное соединение
                int centerX = (int)((cell.RoomCenter.x + other.RoomCenter.x) / 2f);
                int doorY = diff.y > 0
                    ? cell.roomOrigin.y + cell.roomSize.y  // верхняя стена
                    : cell.roomOrigin.y - 1;                // нижняя стена

                doors.Add(new DoorInfo
                {
                    center = new Vector2(centerX + 0.5f, doorY + 0.5f),
                    size = new Vector2(tileWidth, 1f)
                });
            }
        }

        return doors;
    }

    private void OnDestroy()
    {
        if (registry.TryGetValue(roomIndex, out var rc) && rc == this)
            registry.Remove(roomIndex);
    }
}
