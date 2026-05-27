using Mirror;
using UnityEngine;

/// <summary>
/// Серверный счётчик per-player статистики забега: убитые мобы, падения, собранные монеты.
/// Значения SyncVar — клиент может прочитать их у любого игрока для итогового экрана.
/// Состояние переносится между биомами через RunModifiers.Snapshot.
/// </summary>
public class RunStatsTracker : NetworkBehaviour
{
    [SyncVar] public int killedMobs;
    [SyncVar] public int downedCount;
    [SyncVar] public int collectedCoins;

    [Server] public void RegisterKill() => killedMobs++;
    [Server] public void RegisterDowned() => downedCount++;
    [Server] public void RegisterCoins(int amount) => collectedCoins += Mathf.Max(0, amount);

    public struct Snapshot
    {
        public int killedMobs;
        public int downedCount;
        public int collectedCoins;
    }

    public Snapshot CaptureSnapshot() => new Snapshot
    {
        killedMobs = killedMobs,
        downedCount = downedCount,
        collectedCoins = collectedCoins,
    };

    [Server]
    public void ApplySnapshot(Snapshot s)
    {
        killedMobs = s.killedMobs;
        downedCount = s.downedCount;
        collectedCoins = s.collectedCoins;
    }
}
