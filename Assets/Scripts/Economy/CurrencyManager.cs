using System;
using UnityEngine;

/// <summary>
/// Глобальный менеджер валюты ("душ").
/// Хранит количество монет в PlayerPrefs — сохраняется между забегами и сессиями.
/// Не требует сетевой синхронизации: каждый игрок копит свои душ локально.
/// </summary>
public static class CurrencyManager
{
    private const string PrefKey = "dk_coins";

    public static event Action<int> OnCoinsChanged;

    private static int cached = -1;

    public static int Coins
    {
        get
        {
            if (cached < 0)
                cached = PlayerPrefs.GetInt(PrefKey, 0);
            return cached;
        }
        private set
        {
            cached = Mathf.Max(0, value);
            PlayerPrefs.SetInt(PrefKey, cached);
            PlayerPrefs.Save();
            OnCoinsChanged?.Invoke(cached);
        }
    }

    public static void Add(int amount)
    {
        if (amount <= 0) return;
        Coins = Coins + amount;
    }

    public static bool TrySpend(int amount)
    {
        if (amount <= 0) return true;
        if (Coins < amount) return false;
        Coins = Coins - amount;
        return true;
    }

    public static bool CanAfford(int amount) => Coins >= amount;

    /// <summary>
    /// Только для отладки — обнулить копилку.
    /// </summary>
    public static void DebugReset()
    {
        Coins = 0;
    }
}
