using Mirror;
using UnityEngine;

public class DungeonKnightNetworkManager : NetworkManager
{
    [Header("Подземелье")]
    [SerializeField] private GridWalkConfig dungeonConfig;

    [Header("Герои")]
    // Временно: один тип героя для всех. Заменить на выбор из лобби.
    [SerializeField] private HeroData defaultHeroData;

    private int authoritativeSeed;
    private bool dungeonGenerated;

    public override void OnStartServer()
    {
        base.OnStartServer();
        dungeonGenerated = false;
        NetworkServer.RegisterHandler<RequestSeedMessage>(OnClientRequestedSeed);
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

        SpawnPlayerForConnection(conn);
    }

    private void SpawnPlayerForConnection(NetworkConnectionToClient conn)
    {
        Vector2 spawnPos = Vector2.zero;
        var dungeonGen = FindAnyObjectByType<GridWalkDungeonGenerator>();
        if (dungeonGen != null && dungeonGen.Generator != null)
            spawnPos = dungeonGen.Generator.StartCell.RoomCenter;

        GameObject player = Instantiate(playerPrefab, spawnPos, Quaternion.identity);

        // Применяем данные героя и добавляем нужный компонент способностей
        if (defaultHeroData != null)
            InitHeroOnPlayer(player, defaultHeroData);

        NetworkServer.AddPlayerForConnection(conn, player);
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
