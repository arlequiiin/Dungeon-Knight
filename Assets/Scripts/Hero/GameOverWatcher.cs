using Mirror;
using UnityEngine;

/// <summary>
/// Следит за состоянием всех игроков. Если все упавшие или мертвы — game over.
/// Вызывается сервером из HeroStats.EnterDowned.
/// </summary>
public static class GameOverWatcher
{
    private static bool triggered;

    public static void Reset() => triggered = false;

    [Server]
    public static void CheckAllDowned()
    {
        if (!NetworkServer.active) return;
        if (triggered) return;

        int alivePlayers = 0;
        int totalPlayers = 0;

        foreach (var identity in NetworkServer.spawned.Values)
        {
            if (identity == null) continue;
            var hs = identity.GetComponent<HeroStats>();
            if (hs == null) continue;

            totalPlayers++;
            if (!hs.IsDowned && !hs.IsDead)
                alivePlayers++;
        }

        if (totalPlayers > 0 && alivePlayers == 0)
        {
            triggered = true;
            Debug.Log("[GameOver] Все игроки упали — конец игры");
            NetworkServer.SendToAll(new GameOverMessage());
        }
    }
}

public struct GameOverMessage : NetworkMessage { }
