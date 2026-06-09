using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

public class DungeonKnightNetworkManager : NetworkManager
{
    [Header("Кампания (последовательность биомов)")]
    [Tooltip("Биомы по порядку. После победы сервер переходит к следующему. " +
             "Если пуст — используется только levelConfig ниже как одиночный уровень.")]
    [SerializeField] private LevelConfig[] campaign;

    [Tooltip("Fallback если campaign пуст: одиночный уровень (legacy).")]
    [SerializeField] private LevelConfig levelConfig;

    private int campaignIndex;

    /// <summary>Активный уровень: текущий из кампании, либо одиночный levelConfig.</summary>
    public LevelConfig LevelConfig
    {
        get
        {
            if (campaign != null && campaign.Length > 0)
                return campaign[Mathf.Clamp(campaignIndex, 0, campaign.Length - 1)];
            return levelConfig;
        }
    }

    /// <summary>Сбросить кампанию на первый биом (новый забег с начала).</summary>
    public void ResetCampaign()
    {
        campaignIndex = 0;
        // Накопленные за прошлый забег модификаторы тоже сбрасываются.
        savedModifiers.Clear();
    }

    /// <summary>
    /// Перейти к следующему биому. Возвращает true если есть следующий, false если кампания пройдена.
    /// </summary>
    public bool AdvanceCampaign()
    {
        if (campaign == null || campaign.Length == 0) return false;
        campaignIndex++;
        return campaignIndex < campaign.Length;
    }

    public bool IsLastCampaignBiome => campaign == null || campaign.Length == 0
        || campaignIndex >= campaign.Length - 1;

    [Header("Герои")]
    [SerializeField] private HeroData defaultHeroData;
    [SerializeField] private HeroData[] allHeroes;
    public HeroData[] AllHeroes => allHeroes;

    [Header("Лобби")]
    [SerializeField] private Vector2 lobbySpawnPoint = Vector2.zero;

    private int authoritativeSeed;
    private bool dungeonGenerated;

    // Хранит выбор героя каждого подключённого игрока (переживает смену сцен)
    private readonly Dictionary<NetworkConnectionToClient, HeroType> selectedHeroes = new();

    // Разблокировки героев у каждого клиента (присылаются клиентом при подключении).
    private readonly Dictionary<NetworkConnectionToClient, HashSet<HeroType>> clientUnlocks = new();

    // Снимок RunModifiers каждого игрока — сохраняется при уничтожении старого player object
    // (переход между биомами кампании) и применяется к новому в OnServerAddPlayer.
    private readonly Dictionary<NetworkConnectionToClient, RunModifiers.Snapshot> savedModifiers = new();

    // Баффы из «казино» лобби, присланные клиентом. Применяются один раз на старте забега
    // (campaignIndex == 0) и затем удаляются — на следующих биомах не действуют повторно.
    private readonly Dictionary<NetworkConnectionToClient, List<CasinoManager.Buff>> pendingCasinoBuffs = new();

    public override void OnStartServer()
    {
        base.OnStartServer();
        dungeonGenerated = false;
        campaignIndex = 0;
        selectedHeroes.Clear();
        clientUnlocks.Clear();
        savedModifiers.Clear();
        pendingCasinoBuffs.Clear();
        GameOverWatcher.Reset();
        BossRewardCoordinator.Reset();
        NetworkServer.RegisterHandler<RequestSeedMessage>(OnClientRequestedSeed);
        NetworkServer.RegisterHandler<ClientUnlocksMessage>(OnClientUnlocksReceived);
        NetworkServer.RegisterHandler<CasinoBuffsMessage>(OnCasinoBuffsReceived);
        BossRewardCoordinator.RegisterServerHandlers();
    }

    [Server]
    private void OnClientUnlocksReceived(NetworkConnectionToClient conn, ClientUnlocksMessage msg)
    {
        var set = new HashSet<HeroType>();
        if (msg.unlockedHeroes != null)
            foreach (var h in msg.unlockedHeroes) set.Add(h);
        clientUnlocks[conn] = set;

        // Если этому клиенту уже выдан герой, которого у него нет в разблокировках —
        // переназначим на разблокированного. Это закрывает баг, когда сервер на старте
        // выбирал героя по своим (хостовским) PlayerPrefs.
        if (selectedHeroes.TryGetValue(conn, out var current) && !set.Contains(current) && !IsUnlockedByDefault(current))
        {
            var swap = PickUnlockedFor(conn);
            if (swap != HeroType.None && swap != current)
            {
                selectedHeroes[conn] = swap;
                SetHeroForConnection(conn, swap);

                var lobbyManager = FindAnyObjectByType<LobbyManager>();
                if (lobbyManager != null) lobbyManager.RegisterInitialHero(conn, swap);
            }
        }
    }

    [Server]
    private void OnCasinoBuffsReceived(NetworkConnectionToClient conn, CasinoBuffsMessage msg)
    {
        var list = new List<CasinoManager.Buff>();
        if (msg.buffTypes != null && msg.magnitudes != null)
        {
            int n = Mathf.Min(msg.buffTypes.Length, msg.magnitudes.Length);
            for (int i = 0; i < n; i++)
            {
                list.Add(new CasinoManager.Buff
                {
                    type = (CasinoManager.BuffType)msg.buffTypes[i],
                    magnitude = msg.magnitudes[i],
                });
            }
        }
        pendingCasinoBuffs[conn] = list;

        // Если игрок уже в забеге (сообщение пришло ПОСЛЕ спавна — обычный порядок, т.к.
        // AddPlayer отправляется раньше) — применяем со сдвигом на кадр. Иначе host-клиент
        // при initial OnDeserialize свежезаспавненного объекта перезатрёт SyncVar/SyncList
        // дефолтами, и баффы с записями в журнале «пропадут» (ровно симптом «не отображается»).
        if (conn.identity != null)
        {
            var mods = conn.identity.GetComponent<RunModifiers>();
            if (mods != null) StartCoroutine(ApplyCasinoBuffsNextFrame(conn, mods));
        }
    }

    /// <summary>
    /// Применяет отложенные баффы казино к RunModifiers игрока и удаляет их (одноразовые).
    /// </summary>
    [Server]
    private void ApplyCasinoBuffsTo(NetworkConnectionToClient conn, RunModifiers mods)
    {
        if (mods == null) return;
        if (!pendingCasinoBuffs.TryGetValue(conn, out var buffs) || buffs.Count == 0) return;

        var log = mods.GetComponent<RunRewardLog>();
        foreach (var b in buffs)
        {
            mods.ApplyCasinoBuff(b.type, b.magnitude);
            if (log != null)
                log.RecordCasinoModifier(CasinoManager.DescribeModifier(b.type, b.magnitude));
        }

        pendingCasinoBuffs.Remove(conn);
    }

    private bool IsUnlockedByDefault(HeroType type)
    {
        var data = GetHeroData(type);
        return data != null && data.unlockedByDefault;
    }

    /// <summary>
    /// Разблокирован ли герой у конкретного клиента (по присланному ClientUnlocksMessage).
    /// Используется LobbyManager для валидации выбора героя.
    /// </summary>
    [Server]
    public bool IsHeroUnlockedForConn(NetworkConnectionToClient conn, HeroType type)
    {
        if (IsUnlockedByDefault(type)) return true;
        return clientUnlocks.TryGetValue(conn, out var set) && set.Contains(type);
    }

    /// <summary>
    /// Подобрать свободного героя, разблокированного у этого клиента.
    /// Если присланы unlocks — фильтр строгий по ним. Если нет — фолбэк на дефолтных.
    /// </summary>
    [Server]
    private HeroType PickUnlockedFor(NetworkConnectionToClient conn)
    {
        if (allHeroes == null || allHeroes.Length == 0) return HeroType.None;
        var taken = new HashSet<HeroType>(selectedHeroes.Values);
        bool hasUnlocks = clientUnlocks.TryGetValue(conn, out var unlocks);

        var candidates = new List<HeroData>();
        foreach (var h in allHeroes)
        {
            if (h == null) continue;
            if (taken.Contains(h.heroType) && (!selectedHeroes.TryGetValue(conn, out var cur) || cur != h.heroType)) continue;
            bool unlocked = h.unlockedByDefault || (hasUnlocks && unlocks.Contains(h.heroType));
            if (unlocked) candidates.Add(h);
        }

        if (candidates.Count == 0) return HeroType.None;
        return candidates[Random.Range(0, candidates.Count)].heroType;
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        selectedHeroes.Remove(conn);
        clientUnlocks.Remove(conn);

        // Оповещаем LobbyManager чтобы он обновил бродкаст
        var lobbyManager = FindAnyObjectByType<LobbyManager>();
        if (lobbyManager != null)
            lobbyManager.OnPlayerDisconnected(conn);

        base.OnServerDisconnect(conn);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        NetworkClient.RegisterHandler<SeedBroadcastMessage>(OnSeedReceived);
        NetworkClient.RegisterHandler<RoomStateMessage>(OnRoomStateReceived);
        NetworkClient.RegisterHandler<GameOverMessage>(OnGameOverReceived);
        NetworkClient.RegisterHandler<CoinDropMessage>(OnCoinDropReceived);
        NetworkClient.RegisterHandler<WaveAnnouncementMessage>(OnWaveAnnouncement);
        NetworkClient.RegisterHandler<SpawnIndicatorsMessage>(OnSpawnIndicators);
        BossRewardCoordinator.RegisterClientHandlers();
    }

    private void OnCoinDropReceived(CoinDropMessage msg)
    {
        CurrencyManager.AddRun(msg.amount);
    }

    private void OnWaveAnnouncement(WaveAnnouncementMessage msg)
    {
        if (PlayerHUD.LocalInstance != null)
            PlayerHUD.LocalInstance.ShowNotification($"ВОЛНА {msg.wave}/{msg.total}");
    }

    private System.Collections.IEnumerator TriggerHintAfter(string hintId, float delay)
    {
        yield return new WaitForSeconds(delay);
        TutorialManager.Trigger(hintId);
    }

    private System.Collections.IEnumerator ShowBiomeWhenReady(string title)
    {
        float waited = 0f;
        while (PlayerHUD.LocalInstance == null && waited < 5f)
        {
            waited += Time.deltaTime;
            yield return null;
        }
        if (PlayerHUD.LocalInstance != null)
            PlayerHUD.LocalInstance.ShowNotification(title.ToUpperInvariant(), 3.5f);
    }

    private void OnSpawnIndicators(SpawnIndicatorsMessage msg)
    {
        var spawner = FindAnyObjectByType<MobSpawner>();
        if (spawner == null || spawner.SpawnIndicatorPrefab == null) return;
        if (msg.positions == null) return;

        foreach (var pos in msg.positions)
        {
            var go = Instantiate(spawner.SpawnIndicatorPrefab, new Vector3(pos.x, pos.y, 0f), Quaternion.identity);
            Destroy(go, msg.duration);
        }
    }

    private void OnGameOverReceived(GameOverMessage msg)
    {
        DeathScreenUI.ShowGameOver();
    }

    /// <summary>
    /// Вызывается ДО смены сцены на сервере. Mirror уничтожит player object'ы при загрузке новой сцены,
    /// поэтому Snapshot RunModifiers нужно снимать здесь — позже identity будет уже null.
    /// </summary>
    private System.Collections.IEnumerator ApplySnapshotNextFrame(RunModifiers mods, RunModifiers.Snapshot snapshot)
    {
        // Ждём 2 кадра, чтобы Mirror точно успел провести initial OnDeserialize host-клиента.
        yield return null;
        yield return null;
        if (mods != null)
            mods.ApplySnapshot(snapshot);
    }

    private System.Collections.IEnumerator ApplyCasinoBuffsNextFrame(NetworkConnectionToClient conn, RunModifiers mods)
    {
        // Та же причина задержки, что и в ApplySnapshotNextFrame: ждём initial OnDeserialize.
        yield return null;
        yield return null;
        ApplyCasinoBuffsTo(conn, mods);
    }

    public override void OnServerChangeScene(string newSceneName)
    {
        base.OnServerChangeScene(newSceneName);

        // Переносим модификаторы только между игровыми сценами (биом → биом).
        // При выходе в лобби модификаторы должны сбрасываться — это делает ResetCampaign().
        if (!newSceneName.Contains("SampleScene")) return;

        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn == null || conn.identity == null) continue;
            var mods = conn.identity.GetComponent<RunModifiers>();
            if (mods != null)
                savedModifiers[conn] = mods.CaptureSnapshot();
        }
    }

    public override void OnServerSceneChanged(string sceneName)
    {
        base.OnServerSceneChanged(sceneName);
        dungeonGenerated = false;

        // Сброс состояния game over и боссовой координации между забегами
        // (в т.ч. при переходе SampleScene → SampleScene между биомами кампании).
        GameOverWatcher.Reset();
        BossRewardCoordinator.Reset();
        Time.timeScale = 1f;

        if (!sceneName.Contains("SampleScene")) return;

        GenerateDungeonOnServer();
        ReinitPlayersForDungeon();
    }

    private void ReinitPlayersForDungeon()
    {
        // Уничтожаем старых игроков (если они перенеслись через DontDestroyOnLoad).
        // НЕ спавним новых сами — клиенты при загрузке сцены пришлют AddPlayerMessage,
        // и OnServerAddPlayer создаст игроков с правильными хиро-данными.
        // Перед Destroy снимаем Snapshot RunModifiers, чтобы накопленные награды пережили
        // переход между биомами кампании (иначе SyncVar'ы сбрасываются в дефолт).
        // Snapshot модификаторов снят раньше — в OnServerChangeScene, пока identity ещё существовал.
        // Mirror к этому моменту уже уничтожил player object'ы; цикл оставлен на случай,
        // если какие-то identity всё же дожили (DontDestroyOnLoad и т.п.).
        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn == null) continue;
            if (conn.identity != null)
                NetworkServer.Destroy(conn.identity.gameObject);
        }
    }

    public override void OnClientConnect()
    {
        base.OnClientConnect();
        // base.OnClientConnect() уже вызывает AddPlayer при autoCreatePlayer = true

        // Сообщаем серверу свои разблокировки, чтобы при назначении начального героя
        // он не выдал нам залоченного.
        SendUnlocksToServer();
    }

    /// <summary>
    /// Отправляет серверу актуальный список разблокированных героев.
    /// Вызывается при подключении и после покупки героя в лобби — иначе серверный
    /// clientUnlocks остаётся устаревшим и отклоняет выбор только что купленного героя.
    /// </summary>
    public void SendUnlocksToServer()
    {
        if (!NetworkClient.active || NetworkClient.connection == null) return;
        if (allHeroes == null) return;
        var unlocked = new System.Collections.Generic.List<HeroType>();
        foreach (var h in allHeroes)
        {
            if (h == null) continue;
            if (HeroUnlockManager.IsUnlocked(h)) unlocked.Add(h.heroType);
        }
        NetworkClient.Send(new ClientUnlocksMessage { unlockedHeroes = unlocked.ToArray() });
    }

    /// <summary>
    /// Отправляет серверу выигранные в «казино» баффы и забирает их из локального хранилища
    /// (одноразовые — действуют только на стартующий забег).
    /// </summary>
    public void SendCasinoBuffsToServer()
    {
        if (!NetworkClient.active || NetworkClient.connection == null) return;

        if (!CasinoManager.ConsumeCurrent(out var deal)) return;

        // Сделка казино = бафф + дебафф (две записи на разные характеристики).
        NetworkClient.Send(new CasinoBuffsMessage
        {
            buffTypes = new[] { (byte)deal.buff.type, (byte)deal.debuff.type },
            magnitudes = new[] { deal.buff.magnitude, deal.debuff.magnitude },
        });
    }

    private void GenerateDungeonOnServer()
    {
        if (dungeonGenerated) return;

        var active = LevelConfig;
        if (active == null || active.dungeon == null)
        {
            Debug.LogError("[Network] LevelConfig или его dungeon не назначены!");
            return;
        }

        // Сид определяется КАЖДЫЙ забег заново. SO не мутируется — флаг useRandomSeed
        // и поле seed остаются такими, как в ассете.
        var cfg = active.dungeon;
        authoritativeSeed = cfg.useRandomSeed
            ? System.Environment.TickCount
            : cfg.seed;

        var dungeonGen = FindAnyObjectByType<GridWalkDungeonGenerator>();
        if (dungeonGen != null)
        {
            dungeonGen.ApplyLevelConfig(active);
            dungeonGen.GenerateDungeon(authoritativeSeed);
            dungeonGenerated = true;

            Analytics.StartRun();
            Analytics.Event("run_start",
                "level", active.name,
                "difficulty", active.difficulty,
                "players", NetworkServer.connections.Count,
                "seed", authoritativeSeed);

            // Туториал: подсказку движения показываем только на первом биоме забега,
            // чтобы не повторять её при каждой смене биома.
            if (campaignIndex == 0)
            {
                if (TutorialManager.Instance != null) TutorialManager.Instance.ResetShown();
                StartCoroutine(TriggerHintAfter("movement", 1.0f));
            }

            // Хост: на хосте OnSeedReceived не вызовется (он сам сервер), показываем уведомление здесь.
            if (NetworkClient.active)
            {
                string title = !string.IsNullOrEmpty(active.displayName) ? active.displayName : active.name;
                StartCoroutine(ShowBiomeWhenReady(title));
            }
        }
        else
        {
            Debug.LogError("[Network] GridWalkDungeonGenerator не найден на сцене!");
        }
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        if (conn.identity != null) return;

        // Если у этого conn уже есть выбранный герой — значит это тот же игрок,
        // который сменил сцену (лобби↔забег). Не отвергаем, спавним заново.
        bool isReturningPlayer = selectedHeroes.ContainsKey(conn);
        bool inDungeon = IsInDungeon();

        if (inDungeon && !isReturningPlayer)
        {
            // Запрещаем НОВЫМ игрокам подключаться посреди забега.
            Debug.Log($"[Network] Rejected new player {conn.connectionId} — game already in progress.");
            conn.Disconnect();
            return;
        }

        // Позиция спавна: в забеге — стартовая комната, в лобби — lobbySpawnPoint.
        Vector2 spawnPos = lobbySpawnPoint;
        if (inDungeon)
        {
            var dungeonGen = FindAnyObjectByType<GridWalkDungeonGenerator>();
            if (dungeonGen?.Generator != null)
                spawnPos = dungeonGen.Generator.StartCell.RoomCenter;
        }

        GameObject player = Instantiate(playerPrefab, spawnPos, Quaternion.identity);

        // Герой: если у conn уже был выбран — используем его; иначе случайный свободный.
        HeroData data;
        if (isReturningPlayer)
        {
            data = GetHeroData(selectedHeroes[conn]) ?? defaultHeroData;
        }
        else
        {
            data = GetRandomAvailableHero();
            selectedHeroes[conn] = data.heroType;
        }

        InitHeroOnPlayer(player, data);
        NetworkServer.AddPlayerForConnection(conn, player);

        // Восстанавливаем накопленные модификаторы забега (вторая атака, бонусы урона и т.д.).
        // Snapshot был снят в ReinitPlayersForDungeon при уничтожении предыдущего player object.
        // На host-клиенте Mirror после Spawn локально десериализует initial state с дефолтами
        // (он зафиксирован ДО нашего ApplySnapshot). Поэтому применяем Snapshot со сдвигом
        // на один кадр — иначе OnDeserialize перепишет наши значения нулями.
        if (savedModifiers.TryGetValue(conn, out var snapshot))
        {
            var mods = player.GetComponent<RunModifiers>();
            if (mods != null)
                StartCoroutine(ApplySnapshotNextFrame(mods, snapshot));
            savedModifiers.Remove(conn);
        }
        else if (campaignIndex == 0)
        {
            // Старт нового забега (первый биом, без снапшота с прошлого биома) —
            // применяем выигранные в лобби баффы казино. На host-клиенте, как и со снапшотом,
            // initial OnDeserialize мог зафиксировать дефолты, поэтому применяем со сдвигом на кадр.
            var mods = player.GetComponent<RunModifiers>();
            if (mods != null && pendingCasinoBuffs.ContainsKey(conn))
                StartCoroutine(ApplyCasinoBuffsNextFrame(conn, mods));
        }

        // Синхронизируем выбор героя с LobbyManager (только при первом входе).
        if (!isReturningPlayer)
        {
            var lobbyManager = FindAnyObjectByType<LobbyManager>();
            if (lobbyManager != null)
            {
                lobbyManager.RegisterInitialHero(conn, data.heroType);
                lobbyManager.SendCurrentStateToPlayer(conn);
            }
        }
    }

    private bool IsInDungeon()
    {
        var dungeonGen = FindAnyObjectByType<GridWalkDungeonGenerator>();
        return dungeonGen != null && dungeonGen.Generator != null;
    }

    /// <summary>
    /// Возвращает случайного героя, не занятого другими игроками,
    /// разблокированного по умолчанию (на момент подключения сервер ещё не знает
    /// клиентских разблокировок — они приходят отдельным сообщением и подменят
    /// героя в OnClientUnlocksReceived если нужно).
    /// </summary>
    private HeroData GetRandomAvailableHero()
    {
        if (allHeroes == null || allHeroes.Length == 0)
            return defaultHeroData;

        var takenTypes = new HashSet<HeroType>(selectedHeroes.Values);
        var available = allHeroes
            .Where(h => h != null
                        && !takenTypes.Contains(h.heroType)
                        && h.unlockedByDefault)
            .ToArray();

        if (available.Length == 0)
            return defaultHeroData; // Все заняты или залочены — фоллбэк

        return available[Random.Range(0, available.Length)];
    }

    // ── Публичные методы для LobbyManager ──

    public HeroData GetHeroData(HeroType type)
    {
        if (allHeroes == null) return null;
        foreach (var h in allHeroes)
            if (h != null && h.heroType == type) return h;
        return null;
    }

    public void SetHeroForConnection(NetworkConnectionToClient conn, HeroType type)
    {
        selectedHeroes[conn] = type;

        // Сразу обновляем визуал игрока в лобби
        if (conn.identity != null)
        {
            var data = GetHeroData(type);
            if (data != null)
                ReinitHeroOnPlayer(conn.identity.gameObject, data);
        }
    }

    public void ClearHeroSelection(NetworkConnectionToClient conn)
    {
        selectedHeroes.Remove(conn);
    }

    public void StartGame()
    {
        ServerChangeScene("SampleScene");
    }

    private void ReinitHeroOnPlayer(GameObject player, HeroData data)
    {
        // Удаляем старый компонент способностей
        var oldAbility = player.GetComponent<HeroAbility>();
        if (oldAbility != null) Destroy(oldAbility);

        // Удаляем старые хитбоксы оружия
        foreach (var hitbox in player.GetComponentsInChildren<WeaponHitbox>())
            Destroy(hitbox.gameObject);

        InitHeroOnPlayer(player, data);
    }

    private void InitHeroOnPlayer(GameObject player, HeroData data)
    {
        // 1. Сначала назначаем AnimatorController — до AddComponent<HeroAbility>,
        //    чтобы HeroAbility.Awake() получил аниматор уже с контроллером
        var animator = player.GetComponent<Animator>();
        if (animator != null && data.animatorController != null)
            animator.runtimeAnimatorController = data.animatorController;

        // 2. Инициализируем HeroStats (должен быть на префабе)
        var stats = player.GetComponent<HeroStats>();
        if (stats != null)
            stats.Init(data);

        // 3. Добавляем компонент способностей
        switch (data.heroType)
        {
            case HeroType.Knight:    player.AddComponent<KnightAbility>();    break;
            case HeroType.Soldier:   player.AddComponent<SoldierAbility>();   break;
            case HeroType.Templar:   player.AddComponent<TemplarAbility>();   break;
            case HeroType.Swordsman: player.AddComponent<SwordsmanAbility>(); break;
            case HeroType.Archer:    player.AddComponent<ArcherAbility>();    break;
            case HeroType.Wizard:    player.AddComponent<WizardAbility>();    break;
            case HeroType.Priest:    player.AddComponent<PriestAbility>();    break;
            default:                 player.AddComponent<KnightAbility>();    break;
        }

        // 4. Инициализируем PlayerController (статы, данные героя)
        var controller = player.GetComponent<PlayerController>();
        controller?.InitHero(data);
    }

    private void OnClientRequestedSeed(NetworkConnectionToClient conn, RequestSeedMessage msg)
    {
        var active = LevelConfig;
        string title = active != null
            ? (!string.IsNullOrEmpty(active.displayName) ? active.displayName : active.name)
            : "";
        conn.Send(new SeedBroadcastMessage
        {
            seed = authoritativeSeed,
            campaignIndex = campaignIndex,
            biomeName = title
        });
    }

    private void OnRoomStateReceived(RoomStateMessage msg)
    {
        // Хост уже обработал локально в RoomController
        if (NetworkServer.active) return;
        RoomController.OnRoomStateChanged(msg.roomIndex, msg.state);
    }

    private void OnSeedReceived(SeedBroadcastMessage msg)
    {
        if (NetworkServer.active) return;

        // Синхронизируем индекс кампании от сервера до выбора активного LevelConfig.
        campaignIndex = msg.campaignIndex;

        // Показываем уведомление с названием биома только в сцене забега.
        // RequestSeedMessage клиент отправляет на КАЖДОЙ смене сцены (в т.ч. при первом
        // входе в лобби), поэтому без этой проверки название биома всплывало бы сразу
        // после подключения к лобби.
        string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (activeScene.Contains("SampleScene") && !string.IsNullOrEmpty(msg.biomeName))
            StartCoroutine(ShowBiomeWhenReady(msg.biomeName));

        var dungeonGen = FindAnyObjectByType<GridWalkDungeonGenerator>();
        if (dungeonGen != null)
        {
            dungeonGen.ApplyLevelConfig(LevelConfig);
            dungeonGen.GenerateDungeon(msg.seed);
        }
    }

    public override void OnClientDisconnect()
    {
        // Если MainMenuUI существует — значит клиент ещё на экране меню (reject при подключении)
        var mainMenu = FindAnyObjectByType<MainMenuUI>();
        bool wasOnMainMenu = mainMenu != null;

        base.OnClientDisconnect();
        Debug.Log("[Network] Client disconnected.");

        if (wasOnMainMenu)
            mainMenu.ShowDisconnectMessage("Connection failed. Game may already be in progress.");
    }

    public override void OnClientSceneChanged()
    {
        // Экраны победы/поражения ставят Time.timeScale = 0. У клиентов нет кнопки
        // «В лобби» (она только у хоста), поэтому при принудительной смене сцены сервером
        // timeScale оставался нулевым и клиент «зависал». Сбрасываем при любой смене сцены.
        Time.timeScale = 1f;

        // Делаем клиента ready, чтобы он принимал spawned-сообщения.
        if (NetworkClient.connection != null && NetworkClient.connection.isAuthenticated && !NetworkClient.ready)
            NetworkClient.Ready();

        // AddPlayer вызываем ТОЛЬКО если у клиента ещё нет PlayerObject —
        // т.е. при первом подключении к лобби. На последующих сменах сцен (лобби↔забег)
        // сервер сам переносит существующего игрока в ReinitPlayersForDungeon, и повторный
        // AddPlayer вызвал бы ошибку "There is already a player for this connection".
        if (NetworkClient.connection != null
            && NetworkClient.connection.isAuthenticated
            && autoCreatePlayer
            && NetworkClient.localPlayer == null)
        {
            NetworkClient.AddPlayer();
        }

        // Жизненный цикл валюты забега:
        //   • Вход в SampleScene на ПЕРВОМ биоме (campaignIndex == 0) — старт забега, обнуляем RunCoins.
        //   • Переход между биомами (campaignIndex > 0) — НЕ сбрасываем, валюта переносится.
        //   • Возврат в LobbyScene (конец забега) — переливаем непотраченные RunCoins в мету.
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (scene.Contains("SampleScene"))
        {
            if (campaignIndex == 0)
            {
                CurrencyManager.ResetRunCoins();
                SendCasinoBuffsToServer();
            }
        }
        else if (scene.Contains("LobbyScene"))
        {
            CurrencyManager.ConvertRunToMeta();
        }

        if (!NetworkServer.active)
        {
            NetworkClient.Send(new RequestSeedMessage());
        }
    }

    /// <summary>
    /// Вернуть всех игроков в лобби (после смерти / завершения забега).
    /// Вызывается только на сервере.
    /// </summary>
    public void ReturnToLobby()
    {
        if (!NetworkServer.active) return;
        // Защита от повторного вызова (например двойной клик по кнопке)
        if (!string.IsNullOrEmpty(networkSceneName) && networkSceneName.Contains("LobbyScene")) return;
        ResetCampaign();
        ServerChangeScene("LobbyScene");
    }

    /// <summary>
    /// Перезагрузить игровую сцену — переход к следующему биому кампании.
    /// Вызывается только на сервере.
    /// </summary>
    [Server]
    public void RestartGameScene()
    {
        if (!NetworkServer.active) return;
        ServerChangeScene("SampleScene");
    }

    /// <summary>
    /// Остановить хост/сервер и вернуться в главное меню.
    /// Вызывается из UI (например, кнопка "Выйти в меню").
    /// </summary>
    public void ReturnToMainMenu()
    {
        if (NetworkServer.active && NetworkClient.isConnected)
            StopHost();
        else if (NetworkClient.isConnected)
            StopClient();
        else if (NetworkServer.active)
            StopServer();
    }
}
