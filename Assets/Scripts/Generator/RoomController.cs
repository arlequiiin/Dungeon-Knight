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
    private GameObject bossChestPrefab;

    // Волны: для обычных комнат totalWaves = max(1, playerCount).
    // Босс-комната всегда 1 "волна" (босс + его призывы).
    private int totalWaves = 1;
    private int currentWave; // 1-based индекс текущей волны
    private float nextWaveDelay; // обратный отсчёт перед следующей волной
    private bool waitingForNextWave;
    private const float WaveDelaySeconds = 1.5f;

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
        {
            rc.UnlockDoors();

            // Уведомление по центру экрана. BOSS DEFEATED — для боссовой комнаты, ROOM CLEARED — для обычной.
            bool isBoss = rc.cell.roomType == RoomType.Boss;
            if (PlayerHUD.LocalInstance != null)
                PlayerHUD.LocalInstance.ShowNotification(isBoss ? "BOSS DEFEATED" : "ROOM CLEARED");
        }

        rc.state = (RoomState)newState;
    }

    public void Init(int index, CellData cell, DungeonGraph graph, int corridorHalfWidth, MobSpawner spawner, GameObject bossChestPrefab = null)
    {
        roomIndex = index;
        this.cell = cell;
        this.graph = graph;
        this.corridorHalfWidth = corridorHalfWidth;
        mobSpawner = spawner;
        this.bossChestPrefab = bossChestPrefab;

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
        var triggeringPlayer = other.GetComponent<PlayerController>();
        if (triggeringPlayer == null) return;

        // Телепортируем всех живых (не downed) игроков ко входу — там же, где встал
        // активировавший триггер игрок, чуть глубже внутрь комнаты, чтобы не оказаться в коридоре.
        Vector3 entrancePos = ComputeSafeEntrancePos(triggeringPlayer.transform.position);
        TeleportLivingPlayersToRoom(entrancePos, triggeringPlayer);

        Analytics.Event("room_enter", "room", roomIndex, "type", cell.roomType.ToString());
        roomEnterTime = Time.time;

        ActivateRoom();
    }

    /// <summary>
    /// Сдвигает позицию входа на пару тайлов в сторону центра комнаты —
    /// чтобы союзники гарантированно оказались внутри (не в стене и не в коридоре),
    /// но всё ещё рядом со входом, а не в центре под мобами.
    /// </summary>
    private Vector3 ComputeSafeEntrancePos(Vector3 rawEntrance)
    {
        Vector3 toCenter = (cell.RoomCenter - (Vector2)rawEntrance);
        Vector3 shifted = rawEntrance;
        if (toCenter.sqrMagnitude > 0.01f)
            shifted += toCenter.normalized * 2f;

        // Клампим по bbox комнаты с отступом от стен — гарантия что точка внутри.
        float padding = 1.5f;
        float minX = cell.roomOrigin.x + padding;
        float maxX = cell.roomOrigin.x + cell.roomSize.x - padding;
        float minY = cell.roomOrigin.y + padding;
        float maxY = cell.roomOrigin.y + cell.roomSize.y - padding;

        shifted.x = Mathf.Clamp(shifted.x, minX, maxX);
        shifted.y = Mathf.Clamp(shifted.y, minY, maxY);
        return shifted;
    }

    [Server]
    private void TeleportLivingPlayersToRoom(Vector3 entrancePos, PlayerController exclude)
    {
        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn == null || conn.identity == null) continue;
            var pc = conn.identity.GetComponent<PlayerController>();
            if (pc == null) continue;
            if (pc == exclude) continue;

            var hs = pc.GetComponent<HeroStats>();
            if (hs == null) continue;
            if (hs.IsDead || hs.IsDowned) continue;

            // Лёгкий разброс вокруг входа (≤ 0.7 тайла), чтобы игроки не наложились друг на друга.
            Vector2 jitter = Random.insideUnitCircle * 0.3f;
            Vector3 dest = entrancePos + new Vector3(jitter.x, jitter.y, 0f);

            // Используем ServerTeleport у NetworkTransform — иначе клиенты будут плавно интерполировать
            // через всю карту, а нам нужен мгновенный телепорт.
            var nt = pc.GetComponent<Mirror.NetworkTransformBase>();
            if (nt != null)
                nt.ServerTeleport(dest, pc.transform.rotation);
            else
                pc.transform.position = dest;

            var rb = pc.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }
    }

    private void ActivateRoom()
    {
        state = RoomState.Active;
        LockDoors();

        // Спавн мобов — только на сервере
        if (NetworkServer.active)
        {
            // Туториал: первая комната с мобами / первая боссовая.
            TutorialManager.Trigger(cell.roomType == RoomType.Boss ? "boss_room" : "combat_room");

            // Босс-комната всегда 1 волна. Обычные — формула из LevelConfig:
            // wavesBase + extraWavesPerPlayer × (playerCount - 1).
            if (cell.roomType == RoomType.Boss)
            {
                totalWaves = 1;
                var bossHealth = mobSpawner.SpawnRoomBoss(cell);
                if (bossHealth != null)
                    trackedMobs.Add(bossHealth);
            }
            else
            {
                int playerCount = Mathf.Max(1, NetworkServer.connections.Count);
                int wavesBase = 1;
                int extraPerPlayer = 1;
                var nm = Mirror.NetworkManager.singleton as DungeonKnightNetworkManager;
                if (nm != null && nm.LevelConfig != null)
                {
                    wavesBase = nm.LevelConfig.wavesBase;
                    extraPerPlayer = nm.LevelConfig.extraWavesPerPlayer;
                }
                totalWaves = Mathf.Max(1, wavesBase + extraPerPlayer * (playerCount - 1));
                SpawnWave(1);
            }

            currentWave = 1;

            // Если босс-комната без босса — сразу зачистка (защита от ошибки конфигурации).
            if (cell.roomType == RoomType.Boss && trackedMobs.Count == 0)
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

            Debug.Log($"[Room] Комната {roomIndex} ({cell.roomType}) активирована! Волн: {totalWaves}");
        }
    }

    [Server]
    private void SpawnWave(int waveIndex)
    {
        // Уведомление о номере волны (если волн больше одной)
        if (totalWaves > 1)
            NetworkServer.SendToAll(new WaveAnnouncementMessage
            {
                wave = waveIndex,
                total = totalWaves
            });

        // Заранее выбираем позиции и шлём клиентам индикаторы (стрелки),
        // через indicatorDelay секунд реально спавним мобов.
        float indicatorDelay = 2f;
        var nmCfg = Mirror.NetworkManager.singleton as DungeonKnightNetworkManager;
        if (nmCfg != null && nmCfg.LevelConfig != null)
            indicatorDelay = nmCfg.LevelConfig.spawnIndicatorDelay;
        var positions = mobSpawner.PreparePositions(cell);

        if (mobSpawner.SpawnIndicatorPrefab != null)
        {
            NetworkServer.SendToAll(new SpawnIndicatorsMessage
            {
                positions = positions,
                duration = indicatorDelay
            });
            StartCoroutine(SpawnAfterDelay(positions, indicatorDelay));
        }
        else
        {
            // Нет префаба индикатора — спавним сразу.
            var mobs = mobSpawner.SpawnRoomMobs(cell, positions);
            if (mobs != null) trackedMobs.AddRange(mobs);
        }
    }

    [Server]
    private System.Collections.IEnumerator SpawnAfterDelay(Vector2[] positions, float delay)
    {
        spawnPending = true;
        yield return new WaitForSeconds(delay);
        spawnPending = false;
        if (state != RoomState.Active) yield break;
        var mobs = mobSpawner.SpawnRoomMobs(cell, positions);
        if (mobs != null) trackedMobs.AddRange(mobs);
    }

    private bool spawnPending; // true пока ждём задержку перед реальным спавном волны

    // ── Отслеживание зачистки ──

    private void Update()
    {
        if (!NetworkServer.active) return;
        if (state != RoomState.Active) return;

        // Пока ждём задержку индикаторов — мобов ещё не существует, не считаем волну зачищенной.
        if (spawnPending) return;

        // Если ждём следующую волну — отсчёт.
        if (waitingForNextWave)
        {
            nextWaveDelay -= Time.deltaTime;
            if (nextWaveDelay <= 0f)
            {
                waitingForNextWave = false;
                currentWave++;
                trackedMobs.Clear();
                SpawnWave(currentWave);
            }
            return;
        }

        // Если все живые мобы либо уже сбегают, либо имеют флаг fleesWhenAlone —
        // запускаем бегство и считаем комнату зачищенной. Боссовых комнат это не касается.
        if (cell.roomType != RoomType.Boss && TryStartFleeingPhase())
        {
            // ClearRoom вызовется ниже, провалившись через все проверки живости.
        }
        else
        {
            // Проверяем все trackedMobs (изначально заспавненные)
            foreach (var mob in trackedMobs)
            {
                if (mob != null && !mob.IsDead && !mob.GetComponent<MobAI>().IsFleeing)
                    return;
            }

            // Дополнительно проверяем призванных мобов в пределах комнаты —
            // боссы (например SkeletonOverlord) спавнят миньонов после ActivateRoom,
            // и они не попадают в trackedMobs.
            if (HasLivingMobsInRoom())
                return;
        }

        // Все мобы текущей волны мертвы.
        if (currentWave < totalWaves)
        {
            // Запускаем задержку перед следующей волной.
            waitingForNextWave = true;
            nextWaveDelay = WaveDelaySeconds;
            return;
        }

        ClearRoom();
    }

    // Кэш чтобы не дёргать FindObjectsByType каждый кадр.
    private float livingMobsCheckTimer;
    private bool cachedHasLivingMobs;
    private const float LivingMobsCheckInterval = 0.5f;

    private float roomEnterTime;

    private bool HasLivingMobsInRoom()
    {
        livingMobsCheckTimer -= Time.deltaTime;
        if (livingMobsCheckTimer > 0f)
            return cachedHasLivingMobs;

        livingMobsCheckTimer = LivingMobsCheckInterval;

        Vector2 roomMin = new Vector2(cell.roomOrigin.x, cell.roomOrigin.y);
        Vector2 roomMax = roomMin + new Vector2(cell.roomSize.x, cell.roomSize.y);

        // Используем NetworkServer.spawned — это уже Dictionary всех заспавненных
        // объектов сервера, в разы дешевле полного сканирования сцены.
        cachedHasLivingMobs = false;
        foreach (var identity in NetworkServer.spawned.Values)
        {
            if (identity == null) continue;
            var mh = identity.GetComponent<MobHealth>();
            if (mh == null || mh.IsDead) continue;
            // Бегущие мобы не блокируют зачистку.
            var ai = identity.GetComponent<MobAI>();
            if (ai != null && ai.IsFleeing) continue;
            Vector2 p = mh.transform.position;
            if (p.x >= roomMin.x && p.x <= roomMax.x && p.y >= roomMin.y && p.y <= roomMax.y)
            {
                cachedHasLivingMobs = true;
                break;
            }
        }
        return cachedHasLivingMobs;
    }

    /// <summary>
    /// Если все живые мобы (отслеживаемые + в bbox комнаты) либо уже бегут,
    /// либо имеют флаг fleesWhenAlone — запускаем бегство всем не-fleeing.
    /// Возвращает true если фаза бегства активирована (или все уже бегут / нет живых).
    /// </summary>
    private bool TryStartFleeingPhase()
    {
        Vector2 roomMin = new Vector2(cell.roomOrigin.x, cell.roomOrigin.y);
        Vector2 roomMax = roomMin + new Vector2(cell.roomSize.x, cell.roomSize.y);

        bool anyLivingFighter = false;        // живой не-fleeing моб без fleesWhenAlone
        bool anyLivingFleeCandidate = false;  // живой моб с fleesWhenAlone, ещё не убежавший

        foreach (var identity in NetworkServer.spawned.Values)
        {
            if (identity == null) continue;
            var mh = identity.GetComponent<MobHealth>();
            if (mh == null || mh.IsDead) continue;
            Vector2 p = mh.transform.position;
            if (p.x < roomMin.x || p.x > roomMax.x || p.y < roomMin.y || p.y > roomMax.y) continue;

            var ai = identity.GetComponent<MobAI>();
            if (ai == null) continue;
            if (ai.IsFleeing) continue;

            bool canFlee = ai.mobData != null && ai.mobData.fleesWhenAlone;
            if (canFlee) anyLivingFleeCandidate = true;
            else anyLivingFighter = true;
        }

        // Запускаем бегство только если живых не-беглецов больше нет.
        if (!anyLivingFighter && anyLivingFleeCandidate)
        {
            foreach (var identity in NetworkServer.spawned.Values)
            {
                if (identity == null) continue;
                var ai = identity.GetComponent<MobAI>();
                var mh = identity.GetComponent<MobHealth>();
                if (ai == null || mh == null || mh.IsDead) continue;
                if (ai.mobData == null || !ai.mobData.fleesWhenAlone) continue;
                if (ai.IsFleeing) continue;

                Vector2 dest = PickFleeDestination(ai.transform.position);
                ai.StartFleeing(roomMin, roomMax, dest);
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// Выбирает точку, куда должен убежать моб: центр ближайшего соседа комнаты в графе.
    /// Если соседей нет — fallback на ближайшую точку за стеной (легаси-поведение).
    /// </summary>
    private Vector2 PickFleeDestination(Vector2 mobPos)
    {
        if (cell.neighbors != null && cell.neighbors.Count > 0)
        {
            CellData best = null;
            float bestDist = float.MaxValue;
            foreach (var n in cell.neighbors)
            {
                if (n == null) continue;
                float d = Vector2.Distance(mobPos, n.RoomCenter);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = n;
                }
            }
            if (best != null) return best.RoomCenter;
        }

        // Fallback: точка за ближайшей стеной комнаты.
        Vector2 roomMin = new Vector2(cell.roomOrigin.x, cell.roomOrigin.y);
        Vector2 roomMax = roomMin + new Vector2(cell.roomSize.x, cell.roomSize.y);
        Vector2 center = (roomMin + roomMax) * 0.5f;
        Vector2 dir = mobPos - center;
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            return new Vector2(dir.x > 0 ? roomMax.x + 2f : roomMin.x - 2f, mobPos.y);
        return new Vector2(mobPos.x, dir.y > 0 ? roomMax.y + 2f : roomMin.y - 2f);
    }

    private void ClearRoom()
    {
        state = RoomState.Cleared;
        UnlockDoors();

        Analytics.Event("room_clear",
            "room", roomIndex,
            "type", cell.roomType.ToString(),
            "duration", Time.time - roomEnterTime);

        // Уведомление в HUD на хосте (на чистом клиенте — в OnRoomStateChanged через RoomStateMessage).
        // Окно VICTORY триггерится отдельно — сервером, когда все клиенты сделали выбор по боссовому сундуку.
        bool isBoss = cell.roomType == RoomType.Boss;
        if (PlayerHUD.LocalInstance != null)
            PlayerHUD.LocalInstance.ShowNotification(isBoss ? "BOSS DEFEATED" : "ROOM CLEARED");

        if (NetworkServer.active && isBoss)
        {
            BossRewardCoordinator.OnBossDefeatedServer();
            TutorialManager.Trigger("boss_defeated");
        }
        else if (NetworkServer.active)
        {
            TutorialManager.Trigger("room_cleared");
        }

        if (NetworkServer.active)
        {
            // Спавн боссового сундука в центре комнаты — только когда умерли босс И все призванные мобы.
            // (trackedMobs к этому моменту полностью пуст или весь IsDead, проверка в Update гарантирует это.)
            if (isBoss && bossChestPrefab != null)
            {
                var chestObj = Instantiate(bossChestPrefab, cell.RoomCenter, Quaternion.identity);
                NetworkServer.Spawn(chestObj);
            }

            // Хил из LevelConfig: процент maxHealth восстанавливается всем живым игрокам.
            // Упавшие сначала ревайвятся (на 30% HP), затем добивают хил сверху.
            float healPercent = 0f;
            var nm = Mirror.NetworkManager.singleton as DungeonKnightNetworkManager;
            if (nm != null && nm.LevelConfig != null)
                healPercent = nm.LevelConfig.healOnRoomClear;

            foreach (var identity in NetworkServer.spawned.Values)
            {
                if (identity == null) continue;
                var hs = identity.GetComponent<HeroStats>();
                if (hs == null) continue;

                if (hs.IsDowned)
                    hs.ForceRevive();

                if (!hs.IsDead && healPercent > 0f)
                    hs.Heal(hs.MaxHealth * healPercent);
            }

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
        // Перекрываем коридор + по 1 тайлу стены с каждой стороны, чтобы игрок не мог
        // проскользнуть впритык к верхнему/нижнему краю коридора и обойти блокировщик.
        int corridorTiles = corridorHalfWidth * 2 + 1;
        int crossWidth = corridorTiles + 2;
        // Толщина по направлению прохода: 2 тайла исключают тоннелирование физики
        // на быстром движении.
        const float thickness = 2f;

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
                float doorX = diff.x > 0
                    ? cell.roomOrigin.x + cell.roomSize.x  // правая стена → первый тайл коридора
                    : cell.roomOrigin.x - 1;                // левая стена → последний тайл коридора

                doors.Add(new DoorInfo
                {
                    center = new Vector2(doorX + 0.5f, centerY + 0.5f),
                    size = new Vector2(thickness, crossWidth)
                });
            }
            else if (diff.y != 0)
            {
                // Вертикальное соединение
                int centerX = (int)((cell.RoomCenter.x + other.RoomCenter.x) / 2f);
                float doorY = diff.y > 0
                    ? cell.roomOrigin.y + cell.roomSize.y  // верхняя стена
                    : cell.roomOrigin.y - 1;                // нижняя стена

                doors.Add(new DoorInfo
                {
                    center = new Vector2(centerX + 0.5f, doorY + 0.5f),
                    size = new Vector2(crossWidth, thickness)
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
