using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// Серверный координатор пост-боссового состояния. После победы над боссом каждый клиент
/// должен прислать BossRewardDoneMessage (после выбора награды или Continue в боссовом сундуке).
/// Когда подтверждения пришли от всех подключённых клиентов — рассылается ShowVictoryMessage.
/// </summary>
public static class BossRewardCoordinator
{
    private static bool waiting;
    private static readonly HashSet<int> donePlayers = new();

    /// <summary>
    /// Регистрирует обработчики на сервере и клиенте. Вызывается из NetworkManager.
    /// </summary>
    public static void RegisterServerHandlers()
    {
        NetworkServer.RegisterHandler<BossRewardDoneMessage>(OnPlayerDone);
    }

    public static void RegisterClientHandlers()
    {
        NetworkClient.RegisterHandler<ShowVictoryMessage>(OnShowVictory);
    }

    /// <summary>
    /// Сбросить состояние при старте/перезапуске забега.
    /// </summary>
    public static void Reset()
    {
        waiting = false;
        donePlayers.Clear();
    }

    /// <summary>
    /// Вызывается на сервере, когда босс убит. Начинаем ждать подтверждений от всех клиентов.
    /// </summary>
    [Server]
    public static void OnBossDefeatedServer()
    {
        waiting = true;
        donePlayers.Clear();
    }

    [Server]
    private static void OnPlayerDone(NetworkConnectionToClient conn, BossRewardDoneMessage _)
    {
        if (!waiting) return;
        if (conn == null) return;

        donePlayers.Add(conn.connectionId);

        // Все подключённые подтвердили — показываем VICTORY всем.
        if (donePlayers.Count >= NetworkServer.connections.Count)
        {
            waiting = false;
            NetworkServer.SendToAll(new ShowVictoryMessage());
        }
    }

    private static void OnShowVictory(ShowVictoryMessage _)
    {
        VictoryScreenUI.TriggerVictory();
    }

    /// <summary>
    /// Локальный игрок отправляет «я закончил» — после выбора в боссовом сундуке или Continue.
    /// </summary>
    public static void NotifyLocalPlayerDone()
    {
        if (NetworkClient.active)
            NetworkClient.Send(new BossRewardDoneMessage());
    }
}
