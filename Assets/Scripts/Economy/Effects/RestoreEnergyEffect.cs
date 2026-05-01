using UnityEngine;

[CreateAssetMenu(fileName = "RestoreEnergy", menuName = "Dungeon Knight/Reward Effect/Restore Energy")]
public class RestoreEnergyEffect : RewardEffect
{
    [Range(0f, 1f)] public float percentOfMax = 0.5f;

    public override void Apply(HeroStats stats, RunModifiers mods)
    {
        if (stats == null) return;
        stats.RestoreEnergy(stats.MaxEnergy * percentOfMax);
    }
}
