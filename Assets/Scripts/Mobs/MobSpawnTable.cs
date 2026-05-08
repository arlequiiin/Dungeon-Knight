using UnityEngine;

/// <summary>
/// ScriptableObject с таблицей весов спавна мобов.
/// Используется MobSpawner-ом для определения вероятности появления каждого моба.
/// Можно создавать разные таблицы под разные карты, биомы, сложности или количество игроков
/// и подменять их через MobSpawner.SetTable() в рантайме.
/// </summary>
[CreateAssetMenu(fileName = "MobSpawnTable", menuName = "Dungeon Knight/Mob Spawn Table")]
public class MobSpawnTable : ScriptableObject
{
    [Tooltip("Список мобов с весами. Шанс = weight / сумма всех weights.")]
    public MobSpawnEntry[] entries;

    /// <summary>Сумма всех весов (для PickRandom).</summary>
    public int TotalWeight
    {
        get
        {
            int sum = 0;
            if (entries != null)
                foreach (var e in entries) sum += Mathf.Max(0, e.weight);
            return sum;
        }
    }

    /// <summary>Случайный префаб с учётом весов.</summary>
    public GameObject PickRandom(System.Random rng)
    {
        if (entries == null || entries.Length == 0) return null;
        int total = TotalWeight;
        if (total <= 0) return entries[0].prefab;

        int roll = rng.Next(total);
        int cumulative = 0;
        foreach (var e in entries)
        {
            cumulative += Mathf.Max(0, e.weight);
            if (roll < cumulative) return e.prefab;
        }
        return entries[entries.Length - 1].prefab;
    }
}
