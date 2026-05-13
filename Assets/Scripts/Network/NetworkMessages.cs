using Mirror;
using UnityEngine;

public struct RequestSeedMessage : NetworkMessage { }

public struct SeedBroadcastMessage : NetworkMessage
{
    public int seed;
    public int campaignIndex;
    public string biomeName;
}

// ── Lobby: Hero Selection ──

public struct HeroSelectRequest : NetworkMessage
{
    public HeroType heroType;
}

/// <summary>
/// Клиент → сервер при подключении: список разблокированных героев у этого клиента.
/// Сервер использует его при выборе случайного начального героя, чтобы не дать
/// клиенту персонажа, которого он сам не разблокировал.
/// </summary>
public struct ClientUnlocksMessage : NetworkMessage
{
    public HeroType[] unlockedHeroes;
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

/// <summary>
/// Сервер → всем клиентам: началась новая волна противников в комнате.
/// </summary>
public struct WaveAnnouncementMessage : NetworkMessage
{
    public int wave;   // 1-based номер волны
    public int total;  // всего волн
}

/// <summary>
/// Сервер → всем клиентам: показать предупреждающие индикаторы спавна
/// (стрелки) на указанных позициях. Через несколько секунд сервер заспавнит
/// в этих местах мобов.
/// </summary>
public struct SpawnIndicatorsMessage : NetworkMessage
{
    public Vector2[] positions;
    public float duration;
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

// ── Tutorial ──

/// <summary>
/// Сервер → всем клиентам: показать туториал-подсказку с указанным id.
/// Клиент сам ищет TutorialHint SO по id и рендерит её через TutorialHintUI.
/// </summary>
public struct ShowHintMessage : NetworkMessage
{
    public string hintId;
}
