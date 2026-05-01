using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Пул наград. Хранит все RewardData ScriptableObjects, разделённые по редкости.
/// Используется сундуками для генерации случайного выбора.
/// </summary>
[CreateAssetMenu(fileName = "RewardPool", menuName = "Dungeon Knight/Reward Pool")]
public class RewardPool : ScriptableObject
{
    [Tooltip("Все награды в игре (любой редкости)")]
    public RewardData[] allRewards;

    /// <summary>
    /// Случайно выбирает 3 награды для сундука.
    /// Обычная сокровищница: 2 Common + 1 Rare.
    /// Боссовая: 2 Rare + 1 Epic.
    /// </summary>
    public List<RewardData> RollChestRewards(bool isBossChest, HeroType heroType, RunModifiers playerMods, System.Random rng)
    {
        var result = new List<RewardData>();

        if (isBossChest)
        {
            AddRandomFromRarity(result, RewardRarity.Rare, 2, heroType, playerMods, rng);
            AddRandomFromRarity(result, RewardRarity.Epic, 1, heroType, playerMods, rng);
        }
        else
        {
            AddRandomFromRarity(result, RewardRarity.Common, 2, heroType, playerMods, rng);
            AddRandomFromRarity(result, RewardRarity.Rare, 1, heroType, playerMods, rng);
        }

        return result;
    }

    private void AddRandomFromRarity(List<RewardData> result, RewardRarity rarity, int count,
        HeroType heroType, RunModifiers mods, System.Random rng)
    {
        var pool = new List<RewardData>();
        foreach (var r in allRewards)
        {
            if (r == null) continue;
            if (r.rarity != rarity) continue;
            if (!IsValidForHero(r, heroType)) continue;
            if (r.unique && IsAlreadyTaken(r, mods)) continue;
            if (result.Contains(r)) continue;
            pool.Add(r);
        }

        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            int idx = rng.Next(pool.Count);
            result.Add(pool[idx]);
            pool.RemoveAt(idx);
        }
    }

    private bool IsValidForHero(RewardData reward, HeroType heroType)
    {
        if (!reward.requiresMeleeWithSecondAttack) return true;
        return heroType == HeroType.Templar || heroType == HeroType.Swordsman
            || heroType == HeroType.Soldier || heroType == HeroType.Knight;
    }

    private bool IsAlreadyTaken(RewardData reward, RunModifiers mods)
    {
        if (mods == null) return false;
        if (reward.effect is ExtraLifeEffect && mods.extraLifeAvailable) return true;
        if (reward.effect is UnlockSecondAttackEffect && mods.attack2Unlocked) return true;
        return false;
    }
}
