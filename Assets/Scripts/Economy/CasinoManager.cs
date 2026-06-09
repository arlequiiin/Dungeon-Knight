using System;
using UnityEngine;

/// <summary>
/// Минимальное «казино» лобби: игрок тратит мета-валюту и получает «сделку» на СЛЕДУЮЩИЙ
/// забег — ОДНОВРЕМЕННО бафф на одну характеристику И дебафф на другую. Бафф в среднем
/// сильнее дебаффа (см. buffBonusMul), поэтому сделка в среднем выгодна, но всегда чем-то
/// платишь. Риск — в том, КАКАЯ характеристика ослабнет.
///
/// Модель «один активный результат»: каждый спин ЗАМЕНЯЕТ предыдущую сделку. Не нравится —
/// перекрути за деньги. Хранится в PlayerPrefs, применяется один раз при старте забега
/// (campaignIndex == 0) и расходуется.
///
/// Без выделенного UI: вход через CasinoAltar (F), результат показывается уведомлением HUD.
/// Сетевой путь повторяет ClientUnlocksMessage: клиент копит сделку локально и шлёт серверу
/// (две записи), сервер применяет к RunModifiers соответствующего игрока на старте забега.
/// </summary>
public static class CasinoManager
{
    /// <summary>Типы модификаторов — каждый маппится на поле RunModifiers.</summary>
    public enum BuffType
    {
        AttackDamage,        // attackDamageBonus
        DamageResistance,    // damageResistance
        AbilityPower,        // abilityPowerBonus
        AbilityCooldown,     // abilityCooldownReduction (бафф = быстрее, дебафф = медленнее)
        EnergyRegen,         // energyRegenPerSecond
    }

    /// <summary>
    /// Один модификатор. magnitude со знаком: положительная = усиление, отрицательная = штраф.
    /// </summary>
    public struct Buff
    {
        public BuffType type;
        public float magnitude;
        public RewardRarity rarity;
        public bool IsDebuff => magnitude < 0f;
    }

    /// <summary>Результат спина: бафф на одну характеристику + дебафф на другую.</summary>
    public struct SpinResult
    {
        public Buff buff;
        public Buff debuff;
    }

    // ── Настройка ──

    [Serializable]
    public struct RarityConfig
    {
        public RewardRarity rarity;
        public float weight;        // вес выпадения
        public float magnitudeMul;  // множитель базовой величины
    }

    /// <summary>Стоимость одного спина в мета-валюте.</summary>
    public const int SpinCost = 50;

    /// <summary>Во сколько раз бафф в среднем сильнее дебаффа (перевес в сторону игрока).</summary>
    private const float buffBonusMul = 1.5f;

    // Вес и сила редкостей. Редкие/эпики реже, но сильнее. Редкость баффа и дебаффа
    // катаются НЕЗАВИСИМО — может выпасть обычный бафф и редкий дебафф (или наоборот).
    private static readonly RarityConfig[] rarityTable =
    {
        new RarityConfig { rarity = RewardRarity.Common, weight = 70f, magnitudeMul = 1f },
        new RarityConfig { rarity = RewardRarity.Rare,   weight = 25f, magnitudeMul = 2f },
        new RarityConfig { rarity = RewardRarity.Epic,   weight = 5f,  magnitudeMul = 4f },
    };

    // Базовая величина каждого типа (для Common). Умножается на magnitudeMul редкости.
    private static float BaseMagnitude(BuffType t) => t switch
    {
        BuffType.AttackDamage     => 0.08f,  // 8% урона (Common)
        BuffType.DamageResistance => 0.05f,  // 5% сопротивления
        BuffType.AbilityPower     => 0.10f,  // 10% силы способности
        BuffType.AbilityCooldown  => 0.05f,  // 5% кулдауна
        BuffType.EnergyRegen      => 1.0f,   // 1 энергии/сек
        _ => 0f,
    };

    public static string DisplayName(BuffType t) => t switch
    {
        BuffType.AttackDamage     => "Урон",
        BuffType.DamageResistance => "Защита",
        BuffType.AbilityPower     => "Сила способности",
        BuffType.AbilityCooldown  => "Перезарядка",
        BuffType.EnergyRegen      => "Реген энергии",
        _ => t.ToString(),
    };

    /// <summary>
    /// Готовая строка-описание модификатора со знаком: "+15% Урон" / "−8% Защита".
    /// Используется и алтарём (уведомление), и журналом наград HUD.
    /// </summary>
    public static string DescribeModifier(BuffType type, float magnitude)
    {
        string sign = magnitude < 0f ? "-" : "+";
        float abs = Mathf.Abs(magnitude);
        // EnergyRegen — абсолютное значение (энергии/сек), остальные — проценты.
        string value = type == BuffType.EnergyRegen
            ? abs.ToString("0.#")
            : Mathf.RoundToInt(abs * 100f) + "%";
        return $"{sign}{value} {DisplayName(type)}";
    }

    public static string RarityName(RewardRarity r) => r switch
    {
        RewardRarity.Common => "Обычный",
        RewardRarity.Rare   => "Редкий",
        RewardRarity.Epic   => "Эпический",
        _ => r.ToString(),
    };

    // ── Хранилище текущей сделки (PlayerPrefs) ──
    // Формат: две записи "type:magnitude:rarity" через ';' (бафф;дебафф). Пусто = сделки нет.
    private const string PrefKey = "dk_casino_deal";

    public static event Action OnCurrentChanged;

    private static bool loaded;
    private static bool hasCurrent;
    private static SpinResult current;

    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        hasCurrent = TryDeserialize(PlayerPrefs.GetString(PrefKey, ""), out current);
    }

    public static bool HasCurrent { get { EnsureLoaded(); return hasCurrent; } }
    public static SpinResult Current { get { EnsureLoaded(); return current; } }

    // ── Спин ──

    /// <summary>
    /// Тратит SpinCost мета-валюты и ЗАМЕНЯЕТ текущую сделку новой (бафф + дебафф).
    /// Возвращает true и результат через out, либо false если не хватило монет.
    /// </summary>
    public static bool TrySpin(out SpinResult result)
    {
        EnsureLoaded();
        result = default;
        if (!CurrencyManager.TrySpendMeta(SpinCost)) return false;

        current = Roll();
        hasCurrent = true;
        result = current;
        Save();
        return true;
    }

    private static SpinResult Roll()
    {
        // Две РАЗНЫЕ характеристики: одна получает бафф, другая — дебафф.
        var types = (BuffType[])Enum.GetValues(typeof(BuffType));
        int buffIdx = UnityEngine.Random.Range(0, types.Length);
        int debuffIdx = UnityEngine.Random.Range(0, types.Length - 1);
        if (debuffIdx >= buffIdx) debuffIdx++; // гарантируем debuffIdx != buffIdx

        var buffType = types[buffIdx];
        var debuffType = types[debuffIdx];

        var buffRarity = RollRarity();
        var debuffRarity = RollRarity();

        return new SpinResult
        {
            buff = new Buff
            {
                type = buffType,
                magnitude = BaseMagnitude(buffType) * MagnitudeMul(buffRarity) * buffBonusMul,
                rarity = buffRarity,
            },
            debuff = new Buff
            {
                type = debuffType,
                magnitude = -BaseMagnitude(debuffType) * MagnitudeMul(debuffRarity),
                rarity = debuffRarity,
            },
        };
    }

    private static RewardRarity RollRarity()
    {
        float total = 0f;
        foreach (var rc in rarityTable) total += rc.weight;
        float roll = UnityEngine.Random.value * total;
        float cum = 0f;
        foreach (var rc in rarityTable)
        {
            cum += rc.weight;
            if (roll < cum) return rc.rarity;
        }
        return rarityTable[0].rarity;
    }

    private static float MagnitudeMul(RewardRarity r)
    {
        foreach (var rc in rarityTable)
            if (rc.rarity == r) return rc.magnitudeMul;
        return 1f;
    }

    /// <summary>
    /// Забирает текущую сделку и очищает хранилище. Вызывается клиентом при старте забега
    /// (одноразово). Возвращает false если сделки нет.
    /// </summary>
    public static bool ConsumeCurrent(out SpinResult result)
    {
        EnsureLoaded();
        result = current;
        bool had = hasCurrent;

        hasCurrent = false;
        current = default;
        PlayerPrefs.DeleteKey(PrefKey);
        PlayerPrefs.Save();
        OnCurrentChanged?.Invoke();
        return had;
    }

    /// <summary>Только для отладки/сброса прогресса.</summary>
    public static void Clear()
    {
        hasCurrent = false;
        current = default;
        PlayerPrefs.DeleteKey(PrefKey);
        PlayerPrefs.Save();
        OnCurrentChanged?.Invoke();
    }

    private static void Save()
    {
        PlayerPrefs.SetString(PrefKey, $"{Serialize(current.buff)};{Serialize(current.debuff)}");
        PlayerPrefs.Save();
        OnCurrentChanged?.Invoke();
    }

    private static string Serialize(Buff b)
    {
        return $"{(int)b.type}:" +
               $"{b.magnitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}:" +
               $"{(int)b.rarity}";
    }

    private static bool TryDeserialize(string s, out SpinResult result)
    {
        result = default;
        if (string.IsNullOrEmpty(s)) return false;
        var parts = s.Split(';');
        if (parts.Length != 2) return false;
        if (!TryDeserializeBuff(parts[0], out var buff)) return false;
        if (!TryDeserializeBuff(parts[1], out var debuff)) return false;
        result = new SpinResult { buff = buff, debuff = debuff };
        return true;
    }

    private static bool TryDeserializeBuff(string s, out Buff b)
    {
        b = default;
        if (string.IsNullOrEmpty(s)) return false;
        var f = s.Split(':');
        if (f.Length != 3) return false;
        if (!int.TryParse(f[0], out int t)) return false;
        if (!float.TryParse(f[1], System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float mag)) return false;
        if (!int.TryParse(f[2], out int r)) return false;
        b = new Buff { type = (BuffType)t, magnitude = mag, rarity = (RewardRarity)r };
        return true;
    }
}
