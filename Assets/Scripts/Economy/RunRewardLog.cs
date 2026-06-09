using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// Журнал подобранных наград игроком за текущий забег.
/// Сервер записывает каждую взятую награду (по имени RewardData),
/// клиент использует список для отрисовки столбика бонусов в PlayerHUD.
/// Переживает переход между биомами через RunModifiers.Snapshot.
/// </summary>
public class RunRewardLog : NetworkBehaviour
{
    [Tooltip("Общий пул, в котором ищутся RewardData по имени (для клиентской отрисовки).")]
    [SerializeField] private RewardPool rewardPool;

    // Имена RewardData в порядке подбора. Дубликаты разрешены — клиент сам считает стаки.
    public readonly SyncList<string> RewardNames = new();

    // Текстовые описания модификаторов казино (нет RewardData-ассета, у них только текст).
    // Напр. "+15% Урон", "−8% Защита". Отрисовываются в журнале HUD отдельными строками без иконки.
    public readonly SyncList<string> CasinoModifiers = new();

    /// <summary>Событие на клиенте при изменении списка. Подписывается PlayerHUD.</summary>
    public event System.Action OnLogChanged;

    public override void OnStartClient()
    {
        RewardNames.OnChange += HandleChange;
        CasinoModifiers.OnChange += HandleChange;
    }

    public override void OnStopClient()
    {
        RewardNames.OnChange -= HandleChange;
        CasinoModifiers.OnChange -= HandleChange;
    }

    private void HandleChange(SyncList<string>.Operation op, int index, string oldValue)
    {
        OnLogChanged?.Invoke();
    }

    /// <summary>
    /// Удалить одну запись о награде указанного типа (например, при срабатывании "Второй жизни").
    /// Если несколько стаков — снимется только первая запись.
    /// </summary>
    [Server]
    public void RemoveRewardByEffectType<T>() where T : RewardEffect
    {
        if (rewardPool == null || rewardPool.allRewards == null) return;
        foreach (var r in rewardPool.allRewards)
        {
            if (r == null || !(r.effect is T)) continue;
            int idx = RewardNames.IndexOf(r.name);
            if (idx >= 0)
            {
                RewardNames.RemoveAt(idx);
                return;
            }
        }
    }

    [Server]
    public void RecordReward(RewardData reward)
    {
        if (reward == null) return;
        RewardNames.Add(reward.name);

        // Лента событий: остальным игрокам показываем, что союзник взял награду.
        var mods = GetComponent<RunModifiers>();
        var pc = GetComponent<PlayerController>();
        string heroName = pc != null && pc.heroData != null ? pc.heroData.heroName : "Союзник";
        if (mods != null)
            mods.BroadcastEventFeed($"{heroName} получает: {reward.rewardName}");
    }

    /// <summary>
    /// Записать модификатор казино (готовая строка-описание) для отображения в журнале HUD.
    /// </summary>
    [Server]
    public void RecordCasinoModifier(string description)
    {
        if (string.IsNullOrEmpty(description)) return;
        CasinoModifiers.Add(description);
    }

    public override void OnStartServer()
    {
        // Подписываемся на события HeroStats для рассылки в ленту.
        var stats = GetComponent<HeroStats>();
        if (stats != null)
        {
            stats.onDowned.AddListener(OnServerDowned);
            stats.onRevived.AddListener(OnServerRevived);
        }
    }

    public override void OnStopServer()
    {
        var stats = GetComponent<HeroStats>();
        if (stats != null)
        {
            stats.onDowned.RemoveListener(OnServerDowned);
            stats.onRevived.RemoveListener(OnServerRevived);
        }
    }

    [Server]
    private void OnServerDowned()
    {
        var mods = GetComponent<RunModifiers>();
        var pc = GetComponent<PlayerController>();
        string heroName = pc != null && pc.heroData != null ? pc.heroData.heroName : "Союзник";
        if (mods != null)
            mods.BroadcastEventFeed($"{heroName} повержен");
    }

    [Server]
    private void OnServerRevived()
    {
        var mods = GetComponent<RunModifiers>();
        var pc = GetComponent<PlayerController>();
        string heroName = pc != null && pc.heroData != null ? pc.heroData.heroName : "Союзник";
        if (mods != null)
            mods.BroadcastEventFeed($"{heroName} поднят");
    }

    /// <summary>
    /// Группирует записи по RewardData и возвращает пары (награда, количество).
    /// Используется PlayerHUD для отрисовки.
    /// </summary>
    public List<(RewardData reward, int count)> GetStackedRewards()
    {
        var result = new List<(RewardData, int)>();
        if (rewardPool == null || rewardPool.allRewards == null) return result;

        var counts = new Dictionary<string, int>();
        foreach (var name in RewardNames)
        {
            if (string.IsNullOrEmpty(name)) continue;
            counts[name] = counts.TryGetValue(name, out var c) ? c + 1 : 1;
        }

        foreach (var r in rewardPool.allRewards)
        {
            if (r == null) continue;
            if (counts.TryGetValue(r.name, out var c))
                result.Add((r, c));
        }

        return result;
    }
}
