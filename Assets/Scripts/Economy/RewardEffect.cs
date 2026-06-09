using UnityEngine;

/// <summary>
/// Базовый класс эффекта награды. Наследники реализуют конкретное действие
/// (хил, +damage, разблокировка атаки и т.п.).
/// Apply вызывается на сервере на игроке-получателе.
/// </summary>
public abstract class RewardEffect : ScriptableObject
{
    public abstract void Apply(HeroStats stats, RunModifiers mods);

    /// <summary>
    /// Краткое числовое описание суммарного вклада при count стаках — для строки бонуса в HUD
    /// (вместо «x2»). Например «+30%» для двух «Острых клинков» по 15%.
    /// Возвращает null если у эффекта нет осмысленного числа — тогда HUD показывает «xN».
    /// </summary>
    public virtual string DescribeTotal(int count) => null;
}
