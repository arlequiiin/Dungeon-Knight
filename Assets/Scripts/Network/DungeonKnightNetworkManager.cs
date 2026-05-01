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

    public override void OnStartServer()
    {
        base.OnStartServer();
        dungeonGenerated = false;
        selectedHeroes.Clear();
        GameOverWatcher.Reset();
        NetworkServer.RegisterHandler<RequestSeedMessage>(OnClientRequestedSeed);
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        selectedHeroes.Remove(conn);

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
    }

    private void OnCoinDropReceived(CoinDropMessage msg)
    {
        CurrencyManager.Add(msg.amount);
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
        var dungeonGen = FindAnyObjectByType<GridWalkDungeonGenerator>();
        Vector2 spawnPos = Vector2.zero;
        if (dungeonGen?.Generator != null)
            spawnPos = dungeonGen.Generator.StartCell.RoomCenter;

        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn == null) continue;

            // Если у игрока уже есть объект (перенесён через DontDestroyOnLoad)
            if (conn.identity != null)
            {
                var player = conn.identity.gameObject;
                player.transform.position = spawnPos;

                HeroType heroType = selectedHeroes.TryGetValue(conn, out var ht)
                    ? ht
                    : defaultHeroData.heroType;

                ReinitHeroOnPlayer(player, GetHeroData(heroType) ?? defaultHeroData);
                Debug.Log($"[Network] Reinit player {conn.connectionId} as {heroType} at {spawnPos}");
            }
            else
            {
                // Объект не перенёсся — создаём заново
                Debug.Log($"[Network] Respawning player {conn.connectionId}");
                GameObject player = Instantiate(playerPrefab, spawnPos, Quaternion.identity);

                HeroData data = selectedHeroes.TryGetValue(conn, out var ht)
                    ? GetHeroData(ht) ?? defaultHeroData
                    : defaultHeroData;

                InitHeroOnPlayer(player, data);
                NetworkServer.AddPlayerForConnection(conn, player);
            }
        }
    }

    public override void OnClientConnect()
    {
        base.OnClientConnect();
        // base.OnClientConnect() уже вызывает AddPlayer при autoCreatePlayer = true
    }

    private void GenerateDungeonOnServer()
    {
        if (dungeonGenerated) return;

        if (dungeonConfig.useRandomSeed)
            authoritativeSeed = System.Environment.TickCount;
        else
            authoritativeSeed = dungeonConfig.seed;

        dungeonConfig.seed = authoritativeSeed;
        dungeonConfig.useRandomSeed = false;

        var dungeonGen = FindAnyObjectByType<GridWalkDungeonGenerator>();
        if (dungeonGen != null)
        {
            dungeonGen.GenerateDungeon();
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

        // Запрещаем подключение во время игры (только в лобби)
        bool inDungeon = IsInDungeon();
        if (inDungeon)
        {
            Debug.Log($"[Network] Rejected player {conn.connectionId} — game already in progress.");
            conn.Disconnect();
            return;
        }

        GameObject player = Instantiate(playerPrefab, lobbySpawnPoint, Quaternion.identity);

        // Назначаем случайного свободного героя
        HeroData data = GetRandomAvailableHero();
        selectedHeroes[conn] = data.heroType;

        InitHeroOnPlayer(player, data);
        NetworkServer.AddPlayerForConnection(conn, player);

        // Синхронизируем выбор героя с LobbyManager
        var lobbyManager = FindAnyObjectByType<LobbyManager>();
        if (lobbyManager != null)
        {
            lobbyManager.RegisterInitialHero(conn, data.heroType);
            lobbyManager.SendCurrentStateToPlayer(conn);
        }
    }

    private bool IsInDungeon()
    {
        var dungeonGen = FindAnyObjectByType<GridWalkDungeonGenerator>();
        return dungeonGen != null && dungeonGen.Generator != null;
    }

    /// <summary>
    /// Возвращает случайного героя, не занятого другими игроками.
    /// </summary>
    private HeroData GetRandomAvailableHero()
    {
        if (allHeroes == null || allHeroes.Length == 0)
            return defaultHeroData;

        var takenTypes = new HashSet<HeroType>(selectedHeroes.Values);
        var available = allHeroes.Where(h => h != null && !takenTypes.Contains(h.heroType)).ToArray();

        if (available.Length == 0)
            return defaultHeroData; // Все заняты — фоллбэк

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

        dungeonConfig.seed = msg.seed;
        dungeonConfig.useRandomSeed = false;

        var dungeonGen = FindAnyObjectByType<GridWalkDungeonGenerator>();
        if (dungeonGen != null)
        {
            dungeonGen.GenerateDungeon();
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
        base.OnClientSceneChanged();

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
