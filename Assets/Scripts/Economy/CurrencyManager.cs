using System;
using UnityEngine;

/// <summary>
/// Менеджер валюты ("душ"). Две раздельные копилки:
///   • MetaCoins — постоянные души для разблокировки героев в лобби, хранятся в PlayerPrefs.
///   • RunCoins — души текущего забега, в памяти, сбрасываются в 0 при старте забега.
///
/// Монеты с мобов и покупки в сундуках работают с RunCoins.
/// При завершении забега (возврат в лобби) непотраченный RunCoins переливается в MetaCoins.
/// </summary>
public static class CurrencyManager
{
    private const string PrefKey = "dk_coins";

    public static event Action<int> OnMetaCoinsChanged;
    public static event Action<int> OnRunCoinsChanged;

    private static int metaCached = -1;
    private static int runCoins;
    private static bool sceneHookInstalled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallSceneHook()
    {
        if (sceneHookInstalled) return;
        sceneHookInstalled = true;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (scene.name.Contains("SampleScene"))
            ResetRunCoins();
        else if (scene.name.Contains("LobbyScene"))
            ConvertRunToMeta();
    }

    // ── Мета-валюта (лобби, разблокировка героев) ──

    public static int MetaCoins
    {
        get
        {
            if (metaCached < 0)
                metaCached = PlayerPrefs.GetInt(PrefKey, 0);
            return metaCached;
        }
        private set
        {
            metaCached = Mathf.Max(0, value);
            PlayerPrefs.SetInt(PrefKey, metaCached);
            PlayerPrefs.Save();
            OnMetaCoinsChanged?.Invoke(metaCached);
        }
    }

    public static void AddMeta(int amount)
    {
        if (amount <= 0) return;
        MetaCoins = MetaCoins + amount;
    }

    public static bool TrySpendMeta(int amount)
    {
        if (amount <= 0) return true;
        if (MetaCoins < amount) return false;
        MetaCoins = MetaCoins - amount;
        return true;
    }

    public static bool CanAffordMeta(int amount) => MetaCoins >= amount;

    // ── Валюта забега (сундуки, дроп с мобов) ──

    public static int RunCoins => runCoins;

    public static void AddRun(int amount)
    {
        if (amount <= 0) return;
        runCoins += amount;
        OnRunCoinsChanged?.Invoke(runCoins);
    }

    public static bool TrySpendRun(int amount)
    {
        if (amount <= 0) return true;
        if (runCoins < amount) return false;
        runCoins -= amount;
        OnRunCoinsChanged?.Invoke(runCoins);
        return true;
    }

    public static bool CanAffordRun(int amount) => runCoins >= amount;

    /// <summary>
    /// Сбрасывает RunCoins в 0. Вызывается при старте нового забега.
    /// </summary>
    public static void ResetRunCoins()
    {
        if (runCoins == 0) return;
        runCoins = 0;
        OnRunCoinsChanged?.Invoke(runCoins);
    }

    /// <summary>
    /// Переливает непотраченные RunCoins в MetaCoins и обнуляет RunCoins.
    /// Вызывается при завершении забега (возврат в лобби, смерть, победа).
    /// </summary>
    public static void ConvertRunToMeta()
    {
        if (runCoins <= 0) return;
        AddMeta(runCoins);
        runCoins = 0;
        OnRunCoinsChanged?.Invoke(runCoins);
    }

    /// <summary>
    /// Только для отладки/сброса прогресса — обнуляет мета-копилку.
    /// </summary>
    public static void DebugResetMeta()
    {
        MetaCoins = 0;
    }
}
