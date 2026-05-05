using Mirror;

public struct RequestSeedMessage : NetworkMessage { }

public struct SeedBroadcastMessage : NetworkMessage
{
    public int seed;
}

// ── Lobby: Hero Selection ──

public struct HeroSelectRequest : NetworkMessage
{
    public HeroType heroType;
}

public struct PlayerReadyMessage : NetworkMessage { }

public struct HeroSelection
{
    public uint netId;
    public HeroType heroType;
    public bool isReady;
}

public struct HeroSelectionsUpdate : NetworkMessage
{
    public HeroSelection[] selections;
}

// ── Room State Sync ──

public struct RoomStateMessage : NetworkMessage
{
    public int roomIndex;
    public byte state; // 0 = Idle, 1 = Active, 2 = Cleared
}

// ── Economy ──

public struct CoinDropMessage : NetworkMessage
{
    public int amount;
}

// ── Boss Reward Sync ──

/// <summary>
/// Клиент → сервер: «я закончил выбор по боссовому сундуку (или закрыл его)».
/// Сервер ждёт сообщения от всех подключённых, потом шлёт ShowVictoryMessage.
/// </summary>
public struct BossRewardDoneMessage : NetworkMessage { }

/// <summary>
/// Сервер → всем клиентам: «можно показать VICTORY».
/// </summary>
public struct ShowVictoryMessage : NetworkMessage { }
