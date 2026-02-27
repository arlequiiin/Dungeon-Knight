using Mirror;

public struct RequestSeedMessage : NetworkMessage { }

public struct SeedBroadcastMessage : NetworkMessage
{
    public int seed;
}
