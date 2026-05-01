using UnityEngine;

[CreateAssetMenu(fileName = "Heal", menuName = "Dungeon Knight/Reward Effect/Heal")]
public class HealEffect : RewardEffect
{
    [Range(0f, 1f)] public float percentOfMax = 0.5f;

    public override void Apply(HeroStats stats, RunModifiers mods)
    {
        if (stats == null) return;
        stats.Heal(stats.MaxHealth * percentOfMax);
    }
}
