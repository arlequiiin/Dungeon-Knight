using UnityEngine;

/// <summary>
/// Контейнер всех настроек одного уровня/биома: геометрия подземелья, мобы, босс,
/// модификаторы сложности. Позволяет создавать пресеты типа Crypt_Easy / Crypt_Hard /
/// Forest_Normal без дублирования геометрии или таблиц мобов.
/// </summary>
[CreateAssetMenu(fileName = "LevelConfig", menuName = "Dungeon Knight/Level Config")]
public class LevelConfig : ScriptableObject
{
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

    [Tooltip("Дополнительные мобы за каждого игрока сверх первого (масштаб волн).")]
    [Range(0f, 2f)] public float perPlayerMobMultiplier = 1f;
}
