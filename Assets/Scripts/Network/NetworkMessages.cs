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
