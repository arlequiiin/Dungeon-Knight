using UnityEngine;

/// <summary>
/// Награда «Увеличение макс. здоровья». Имя класса осталось HealEffect для совместимости
/// с существующими ScriptableObject-ассетами; визуальное имя награды берётся из RewardData.
/// </summary>
[CreateAssetMenu(fileName = "MaxHealth", menuName = "Dungeon Knight/Reward Effect/Max Health")]
public class HealEffect : RewardEffect
{
    [Tooltip("На сколько единиц увеличить максимальное здоровье. Игрок также лечится на ту же величину.")]
    public float bonusMaxHealth = 20f;

    public override void Apply(HeroStats stats, RunModifiers mods)
    {
        if (stats == null) return;
        stats.IncreaseMaxHealth(bonusMaxHealth);
    }
}
