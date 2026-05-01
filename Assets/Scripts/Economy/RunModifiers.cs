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
    /// Серверный helper: применяет финальный множитель урона.
    /// </summary>
    public float ModifyOutgoingDamage(float baseDamage, int attackIndex)
    {
        float mult = 1f + attackDamageBonus;
        if (attackIndex == 1) mult += attack2DamageBonus;
        return baseDamage * mult;
    }

    public float ModifyIncomingDamage(float baseDamage)
    {
        return baseDamage * Mathf.Clamp(1f - damageResistance, 0.1f, 1f);
    }

    public float ModifyAbilityPower(float baseValue)
    {
        return baseValue * (1f + abilityPowerBonus);
    }

    public float ModifyAbilityCooldown(float baseCd)
    {
        return baseCd * Mathf.Clamp(1f - abilityCooldownReduction, 0.1f, 1f);
    }

    /// <summary>
    /// Сервер: пробует использовать "вторую жизнь". Возвращает true если зачёт.
    /// </summary>
    [Server]
    public bool ConsumeExtraLife()
    {
        if (!extraLifeAvailable) return false;
        extraLifeAvailable = false;
        return true;
    }
}
