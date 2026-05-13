using UnityEngine;

/// <summary>
/// Контейнер всех настроек одного уровня/биома: геометрия подземелья, мобы, босс,
/// модификаторы сложности. Позволяет создавать пресеты типа Crypt_Easy / Crypt_Hard /
/// Forest_Normal без дублирования геометрии или таблиц мобов.
/// </summary>
[CreateAssetMenu(fileName = "LevelConfig", menuName = "Dungeon Knight/Level Config")]
public class LevelConfig : ScriptableObject
{
    [Header("Отображение")]
    [Tooltip("Название биома для уведомления при старте забега. Если пусто — берётся имя ассета.")]
    public string displayName;

    [Header("Геометрия подземелья")]
    public GridWalkConfig dungeon;

    [Header("Мобы")]
    [Tooltip("Таблица весов для обычных комнат.")]
    public MobSpawnTable mobTable;

    [Tooltip("Префаб босса для боссовой комнаты. Если не задан — используется bossPrefab из MobSpawner.")]
    public GameObject bossPrefab;

    [Header("Модификаторы сложности")]
    [Tooltip("Передаётся в MobAI.Init и используется для масштабирования урона/HP мобов.")]
    [Range(0.5f, 3f)] public float difficulty = 1f;

    [Header("Количество мобов")]
    [Tooltip("Базовое число мобов в одной волне обычной комнаты (на 1 игрока).")]
    [Range(1, 10)] public int mobsPerNormalRoom = 1;

    [Tooltip("Множитель числа мобов за каждого игрока сверх первого. " +
             "0 = одинаково независимо от числа игроков, 1 = +100% за каждого, и т.д.")]
    [Range(0f, 2f)] public float perPlayerMobMultiplier = 1f;

    [Header("Волны")]
    [Tooltip("Базовое количество волн в обычной комнате (минимум).")]
    [Range(1, 5)] public int wavesBase = 1;

    [Tooltip("Дополнительные волны за каждого игрока сверх первого. " +
             "1 = новая волна за игрока (как раньше), 0 = всегда wavesBase волн.")]
    [Range(0, 3)] public int extraWavesPerPlayer = 1;

    [Tooltip("Процент maxHealth, восстанавливаемый игрокам после зачистки комнаты. " +
             "0 = без хила, 0.25 = 25% от макс HP, 1 = полный хил.")]
    [Range(0f, 1f)] public float healOnRoomClear = 0f;

    [Header("Демо / темп")]
    [Tooltip("Задержка между показом индикаторов спавна и реальным появлением мобов. " +
             "Стандарт 2с, для демо можно укоротить до 0.8-1.0с.")]
    [Range(0.2f, 3f)] public float spawnIndicatorDelay = 2f;
}
