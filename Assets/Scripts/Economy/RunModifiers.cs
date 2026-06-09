using Mirror;
using UnityEngine;

/// <summary>
/// Все модификаторы текущего забега для одного игрока.
/// Аккумулирует баффы от наград (стакаются), читается HeroStats / HeroAbility / атаками.
/// Серверная логика: значения изменяются только через Apply* методы на сервере.
/// Не использует SyncVar для большинства полей — модификаторы применяются на сервере и используются там же
/// (TakeDamage, ability cooldown, attack damage). Клиенту достаточно событий через анимации.
/// </summary>
public class RunModifiers : NetworkBehaviour
{
    // Бонус урона ко всем атакам (стакается). Финальный множитель = 1 + attackDamageBonus.
    [SyncVar] public float attackDamageBonus;

    // Бонус урона ТОЛЬКО ко второй атаке (Attack2). Стакается.
    [SyncVar] public float attack2DamageBonus;

    // Сопротивление урону. Финальный множитель = 1 - damageResistance (clamp).
    [SyncVar] public float damageResistance;

    // Множитель эффекта способности (урон AoE / хил Priest и т.п.). Стакается.
    [SyncVar] public float abilityPowerBonus;

    // Уменьшение кулдауна способности. 0..1. Стакается.
    [SyncVar] public float abilityCooldownReduction;

    // Регенерация энергии (mana) в секунду. Стакается.
    [SyncVar] public float energyRegenPerSecond;

    // Разблокирована ли вторая атака для мили-героев (Templar/Swordsman/Soldier/Knight).
    // По умолчанию false → Attack2 не работает. Награда #7 ставит true.
    [SyncVar] public bool attack2Unlocked;

    // Одноразовый авто-revive при downed.
    [SyncVar] public bool extraLifeAvailable;

    /// <summary>
    /// Сервер: применяет один бафф из «казино» к соответствующему полю модификаторов.
    /// Баффы стакаются с наградами из сундуков (те же поля).
    /// </summary>
    [Server]
    public void ApplyCasinoBuff(CasinoManager.BuffType type, float magnitude)
    {
        // magnitude может быть отрицательной (дебафф казино). Не клампим здесь —
        // итоговые множители ограничиваются в Modify*-методах (с допуском для штрафов).
        switch (type)
        {
            case CasinoManager.BuffType.AttackDamage:     attackDamageBonus += magnitude; break;
            case CasinoManager.BuffType.DamageResistance: damageResistance += magnitude; break;
            case CasinoManager.BuffType.AbilityPower:     abilityPowerBonus += magnitude; break;
            case CasinoManager.BuffType.AbilityCooldown:  abilityCooldownReduction += magnitude; break;
            case CasinoManager.BuffType.EnergyRegen:      energyRegenPerSecond += magnitude; break;
        }
    }

    /// <summary>
    /// Серверный helper: применяет финальный множитель урона.
    /// </summary>
    public float ModifyOutgoingDamage(float baseDamage, int attackIndex)
    {
        float mult = 1f + attackDamageBonus;
        if (attackIndex == 1) mult += attack2DamageBonus;
        // Пол множителя 0.3 — дебафф урона (казино) не обнуляет атаку полностью.
        return baseDamage * Mathf.Max(0.3f, mult);
    }

    public float ModifyIncomingDamage(float baseDamage)
    {
        // Верхний предел 1.5 (а не 1.0) — чтобы отрицательное сопротивление (дебафф казино)
        // действительно увеличивало входящий урон, но не более +50%.
        return baseDamage * Mathf.Clamp(1f - damageResistance, 0.1f, 1.5f);
    }

    public float ModifyAbilityPower(float baseValue)
    {
        // Нижний предел 0.5 — дебафф силы способности не уводит её ниже половины.
        return baseValue * Mathf.Max(0.5f, 1f + abilityPowerBonus);
    }

    public float ModifyAbilityCooldown(float baseCd)
    {
        // Верхний предел 1.5 — отрицательное reduction (дебафф казино) удлиняет кулдаун,
        // но не более чем в полтора раза.
        return baseCd * Mathf.Clamp(1f - abilityCooldownReduction, 0.1f, 1.5f);
    }

    /// <summary>
    /// Сервер: пробует использовать "вторую жизнь". Возвращает true если зачёт.
    /// </summary>
    [Server]
    public bool ConsumeExtraLife()
    {
        if (!extraLifeAvailable) return false;
        extraLifeAvailable = false;

        // Снимаем запись из журнала наград, чтобы плашка ExtraLife пропала из HUD.
        var log = GetComponent<RunRewardLog>();
        if (log != null) log.RemoveRewardByEffectType<ExtraLifeEffect>();

        return true;
    }

    /// <summary>
    /// Сервер рассылает сообщение в ленту событий HUD всем игрокам.
    /// Используется для «союзник упал/поднят/взял награду» и т.п.
    /// </summary>
    [Server]
    public void BroadcastEventFeed(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        RpcAddEvent(text);
    }

    [ClientRpc]
    private void RpcAddEvent(string text)
    {
        if (PlayerHUD.LocalInstance != null)
            PlayerHUD.LocalInstance.AddFeedEvent(text);
    }

    /// <summary>
    /// Снимок всех модификаторов для переноса между биомами кампании.
    /// При смене сцены сервер уничтожает старый player object и спавнит новый со сброшенными SyncVar —
    /// чтобы накопленные награды сохранились, нужно сохранить Snapshot до Destroy и восстановить после спавна.
    /// </summary>
    public struct Snapshot
    {
        public float attackDamageBonus;
        public float attack2DamageBonus;
        public float damageResistance;
        public float abilityPowerBonus;
        public float abilityCooldownReduction;
        public float energyRegenPerSecond;
        public bool attack2Unlocked;
        public bool extraLifeAvailable;
        public string[] rewardLogNames;
        public string[] casinoModifiers;
        public RunStatsTracker.Snapshot stats;
    }

    public Snapshot CaptureSnapshot()
    {
        var log = GetComponent<RunRewardLog>();
        string[] names = null;
        string[] casino = null;
        if (log != null)
        {
            names = new string[log.RewardNames.Count];
            for (int i = 0; i < log.RewardNames.Count; i++) names[i] = log.RewardNames[i];
            casino = new string[log.CasinoModifiers.Count];
            for (int i = 0; i < log.CasinoModifiers.Count; i++) casino[i] = log.CasinoModifiers[i];
        }
        var tracker = GetComponent<RunStatsTracker>();
        return new Snapshot
        {
            attackDamageBonus = attackDamageBonus,
            attack2DamageBonus = attack2DamageBonus,
            damageResistance = damageResistance,
            abilityPowerBonus = abilityPowerBonus,
            abilityCooldownReduction = abilityCooldownReduction,
            energyRegenPerSecond = energyRegenPerSecond,
            attack2Unlocked = attack2Unlocked,
            extraLifeAvailable = extraLifeAvailable,
            rewardLogNames = names,
            casinoModifiers = casino,
            stats = tracker != null ? tracker.CaptureSnapshot() : default,
        };
    }

    [Server]
    public void ApplySnapshot(Snapshot s)
    {
        attackDamageBonus = s.attackDamageBonus;
        attack2DamageBonus = s.attack2DamageBonus;
        damageResistance = s.damageResistance;
        abilityPowerBonus = s.abilityPowerBonus;
        abilityCooldownReduction = s.abilityCooldownReduction;
        energyRegenPerSecond = s.energyRegenPerSecond;
        attack2Unlocked = s.attack2Unlocked;
        extraLifeAvailable = s.extraLifeAvailable;

        var log = GetComponent<RunRewardLog>();
        if (log != null && s.rewardLogNames != null)
        {
            log.RewardNames.Clear();
            foreach (var n in s.rewardLogNames) log.RewardNames.Add(n);
        }
        if (log != null && s.casinoModifiers != null)
        {
            log.CasinoModifiers.Clear();
            foreach (var c in s.casinoModifiers) log.CasinoModifiers.Add(c);
        }
        var tracker = GetComponent<RunStatsTracker>();
        if (tracker != null) tracker.ApplySnapshot(s.stats);
    }
}
