using System.Collections.Generic;
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
    }

    public override void OnServerSceneChanged(string sceneName)
    {
        base.OnServerSceneChanged(sceneName);
        dungeonGenerated = false;

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

        // Если base не вызвал AddPlayer (autoCreatePlayer выключен),
        // вызываем вручную после Ready
        if (NetworkClient.ready && NetworkClient.localPlayer == null)
        {
            NetworkClient.AddPlayer();
        }
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

        // В лобби — спавн на фиксированной позиции с дефолтным героем
        var dungeonGen = FindAnyObjectByType<GridWalkDungeonGenerator>();
        bool inDungeon = dungeonGen != null && dungeonGen.Generator != null;

        Vector2 spawnPos = inDungeon
            ? (Vector2)dungeonGen.Generator.StartCell.RoomCenter
            : lobbySpawnPoint;

        GameObject player = Instantiate(playerPrefab, spawnPos, Quaternion.identity);

        HeroData data = defaultHeroData;
        if (inDungeon && selectedHeroes.TryGetValue(conn, out var heroType))
            data = GetHeroData(heroType) ?? defaultHeroData;

        if (data != null)
            InitHeroOnPlayer(player, data);

        NetworkServer.AddPlayerForConnection(conn, player);
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

    public override void OnClientSceneChanged()
    {
        base.OnClientSceneChanged();

        if (!NetworkServer.active)
        {
            NetworkClient.Send(new RequestSeedMessage());
        }
    }
}
