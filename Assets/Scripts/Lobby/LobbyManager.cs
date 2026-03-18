using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// Серверная логика лобби: отслеживает выбор героев, валидирует уникальность,
/// управляет готовностью игроков и запуском игры.
/// Размещается на объекте в LobbyScene (не DontDestroyOnLoad).
/// </summary>
public class LobbyManager : NetworkBehaviour
{
    public static LobbyManager Instance { get; private set; }

    private DungeonKnightNetworkManager NetManager =>
        (DungeonKnightNetworkManager)NetworkManager.singleton;

    // Серверное состояние
    private readonly Dictionary<NetworkConnectionToClient, HeroType> heroSelections = new();
    private readonly Dictionary<NetworkConnectionToClient, bool> readyStates = new();

    // Клиентское состояние (обновляется через бродкаст)
    private HeroSelection[] currentSelections = System.Array.Empty<HeroSelection>();
    public HeroSelection[] CurrentSelections => currentSelections;

    public event System.Action OnSelectionsUpdated;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        NetworkServer.RegisterHandler<HeroSelectRequest>(OnHeroSelectRequest);
        NetworkServer.RegisterHandler<PlayerReadyMessage>(OnPlayerReadyMessage);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        NetworkClient.RegisterHandler<HeroSelectionsUpdate>(OnSelectionsReceived);
    }

    // ── Серверные хендлеры ──

    [Server]
    private void OnHeroSelectRequest(NetworkConnectionToClient conn, HeroSelectRequest msg)
    {
        // Проверяем: не занят ли этот герой другим игроком
        foreach (var kvp in heroSelections)
        {
            if (kvp.Value == msg.heroType && kvp.Key != conn)
            {
                Debug.Log($"[Lobby] Герой {msg.heroType} уже занят другим игроком");
                return;
            }
        }

        heroSelections[conn] = msg.heroType;

        // Снимаем готовность при смене героя
        readyStates[conn] = false;

        // Применяем визуал на сервере (и синхронизируем клиентам через Mirror)
        var netManager = (DungeonKnightNetworkManager)NetworkManager.singleton;
        netManager.SetHeroForConnection(conn, msg.heroType);

        BroadcastSelections();
    }

    [Server]
    private void OnPlayerReadyMessage(NetworkConnectionToClient conn, PlayerReadyMessage msg)
    {
        // Нельзя быть готовым без выбранного героя
        if (!heroSelections.ContainsKey(conn))
        {
            Debug.Log("[Lobby] Игрок пытается быть готовым без выбора героя");
            return;
        }

        // Toggle
        readyStates[conn] = !readyStates.GetValueOrDefault(conn, false);
        BroadcastSelections();

        CheckAllReady();
    }

    /// <summary>
    /// Вызывается из NetworkManager при спавне игрока с начальным героем.
    /// </summary>
    [Server]
    public void RegisterInitialHero(NetworkConnectionToClient conn, HeroType heroType)
    {
        heroSelections[conn] = heroType;
        readyStates[conn] = false;
        BroadcastSelections();
    }

    [Server]
    public void OnPlayerDisconnected(NetworkConnectionToClient conn)
    {
        heroSelections.Remove(conn);
        readyStates.Remove(conn);
        BroadcastSelections();
    }

    [Server]
    public void SendCurrentStateToPlayer(NetworkConnectionToClient conn)
    {
        conn.Send(new HeroSelectionsUpdate { selections = BuildSelectionsArray() });
    }

    [Server]
    private void BroadcastSelections()
    {
        var update = new HeroSelectionsUpdate { selections = BuildSelectionsArray() };
        NetworkServer.SendToAll(update);
    }

    [Server]
    private HeroSelection[] BuildSelectionsArray()
    {
        var list = new List<HeroSelection>();

        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn.identity == null) continue;

            var sel = new HeroSelection
            {
                netId = conn.identity.netId,
                heroType = heroSelections.GetValueOrDefault(conn, HeroType.None),
                isReady = readyStates.GetValueOrDefault(conn, false)
            };

            // Помечаем heroType только если игрок реально выбрал
            if (!heroSelections.ContainsKey(conn))
                continue; // Не включаем в список тех, кто не выбрал

            list.Add(sel);
        }

        return list.ToArray();
    }

    [Server]
    private void CheckAllReady()
    {
        if (NetworkServer.connections.Count == 0) return;

        // Все подключённые должны выбрать героя и быть готовы
        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn.identity == null) continue;

            if (!heroSelections.ContainsKey(conn)) return;
            if (!readyStates.GetValueOrDefault(conn, false)) return;
        }

        Debug.Log("[Lobby] Все игроки готовы! Запуск игры...");
        var netManager = (DungeonKnightNetworkManager)NetworkManager.singleton;
        netManager.StartGame();
    }

    // ── Клиентский хендлер ──

    private void OnSelectionsReceived(HeroSelectionsUpdate msg)
    {
        currentSelections = msg.selections ?? System.Array.Empty<HeroSelection>();
        OnSelectionsUpdated?.Invoke();
    }

    // ── Утилиты для UI ──

    public bool IsHeroTaken(HeroType type)
    {
        uint localNetId = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.netId : 0;
        foreach (var sel in currentSelections)
        {
            if (sel.heroType == type && sel.netId != localNetId)
                return true;
        }
        return false;
    }

    public HeroType? GetLocalPlayerHero()
    {
        uint localNetId = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.netId : 0;
        foreach (var sel in currentSelections)
        {
            if (sel.netId == localNetId)
                return sel.heroType;
        }
        return null;
    }

    public bool IsLocalPlayerReady()
    {
        uint localNetId = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.netId : 0;
        foreach (var sel in currentSelections)
        {
            if (sel.netId == localNetId)
                return sel.isReady;
        }
        return false;
    }

    public HeroData[] AllHeroes => NetManager != null ? NetManager.AllHeroes : null;
}
