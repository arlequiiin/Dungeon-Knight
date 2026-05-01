using UnityEngine;

/// <summary>
/// Базовый класс эффекта награды. Наследники реализуют конкретное действие
/// (хил, +damage, разблокировка атаки и т.п.).
/// Apply вызывается на сервере на игроке-получателе.
/// </summary>
public abstract class RewardEffect : ScriptableObject
{
    public abstract void Apply(HeroStats stats, RunModifiers mods);
}
