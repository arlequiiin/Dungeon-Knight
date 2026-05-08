using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

public class DungeonKnightNetworkManager : NetworkManager
{
    [Header("Подземелье")]
    [SerializeField] private GridWalkConfig dungeonConfig;

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

    public override void OnStartServer()
    {
        base.OnStartServer();
        dungeonGenerated = false;
        selectedHeroes.Clear();
        clientUnlocks.Clear();
        GameOverWatcher.Reset();
        BossRewardCoordinator.Reset();
        NetworkServer.RegisterHandler<RequestSeedMessage>(OnClientRequestedSeed);
        NetworkServer.RegisterHandler<ClientUnlocksMessage>(OnClientUnlocksReceived);
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
            PlayerHUD.LocalInstance.ShowNotification($"WAVE {msg.wave}/{msg.total}");
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

    public override void OnServerSceneChanged(string sceneName)
    {
        base.OnServerSceneChanged(sceneName);
        dungeonGenerated = false;

        // Сброс состояния game over между забегами
        GameOverWatcher.Reset();
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
        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn == null) continue;
            if (conn.identity != null)
            {
                NetworkServer.Destroy(conn.identity.gameObject);
            }
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

    private void SendUnlocksToServer()
    {
        if (allHeroes == null) return;
        var unlocked = new System.Collections.Generic.List<HeroType>();
        foreach (var h in allHeroes)
        {
            if (h == null) continue;
            if (HeroUnlockManager.IsUnlocked(h)) unlocked.Add(h.heroType);
        }
        NetworkClient.Send(new ClientUnlocksMessage { unlockedHeroes = unlocked.ToArray() });
    }

    private void GenerateDungeonOnServer()
    {
        if (dungeonGenerated) return;

        // Сид определяется КАЖДЫЙ забег заново. SO не мутируется — флаг useRandomSeed
        // и поле seed остаются такими, как в ассете, и переживают рестарт игры без изменений.
        authoritativeSeed = dungeonConfig.useRandomSeed
            ? System.Environment.TickCount
            : dungeonConfig.seed;

        var dungeonGen = FindAnyObjectByType<GridWalkDungeonGenerator>();
        if (dungeonGen != null)
        {
            dungeonGen.GenerateDungeon(authoritativeSeed);
            dungeonGenerated = true;
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
        conn.Send(new SeedBroadcastMessage { seed = authoritativeSeed });
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

        var dungeonGen = FindAnyObjectByType<GridWalkDungeonGenerator>();
        if (dungeonGen != null)
        {
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
        //   • Вход в SampleScene (старт забега) — обнуляем RunCoins, чтобы у игрока было 0 на счету.
        //   • Возврат в LobbyScene (конец забега) — переливаем непотраченные RunCoins в мету.
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (scene.Contains("SampleScene"))
            CurrencyManager.ResetRunCoins();
        else if (scene.Contains("LobbyScene"))
            CurrencyManager.ConvertRunToMeta();

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
        ServerChangeScene("LobbyScene");
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
