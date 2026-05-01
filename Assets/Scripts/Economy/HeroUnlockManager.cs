using System;
using UnityEngine;

/// <summary>
/// Хранит и проверяет разблокировки героев между сессиями (PlayerPrefs).
/// </summary>
public static class HeroUnlockManager
{
    public const int DefaultUnlockCost = 100;

    public static event Action OnUnlocksChanged;

    private static string Key(HeroType t) => $"dk_hero_unlocked_{t}";

    public static bool IsUnlocked(HeroData data)
    {
        if (data == null) return false;
        if (data.unlockedByDefault) return true;
        return PlayerPrefs.GetInt(Key(data.heroType), 0) == 1;
    }

    public static int GetUnlockCost(HeroData data) => DefaultUnlockCost;

    /// <summary>
    /// Тратит монеты и разблокирует героя. Возвращает true при успехе.
    /// </summary>
    public static bool TryUnlock(HeroData data)
    {
        if (data == null) return false;
        if (IsUnlocked(data)) return true;

        int cost = GetUnlockCost(data);
        if (!CurrencyManager.TrySpend(cost)) return false;

        PlayerPrefs.SetInt(Key(data.heroType), 1);
        PlayerPrefs.Save();
        OnUnlocksChanged?.Invoke();
        return true;
    }

    public static void DebugLockAll()
    {
        foreach (HeroType t in System.Enum.GetValues(typeof(HeroType)))
        {
            if (t == HeroType.None) continue;
            PlayerPrefs.DeleteKey(Key(t));
        }
        PlayerPrefs.Save();
        OnUnlocksChanged?.Invoke();
    }
}
