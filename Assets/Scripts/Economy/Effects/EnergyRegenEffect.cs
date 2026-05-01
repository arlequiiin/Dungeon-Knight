using UnityEngine;

[CreateAssetMenu(fileName = "EnergyRegen", menuName = "Dungeon Knight/Reward Effect/Energy Regen")]
public class EnergyRegenEffect : RewardEffect
{
    public float regenPerSecond = 1f;

    public override void Apply(HeroStats stats, RunModifiers mods)
    {
        if (mods == null) return;
        mods.energyRegenPerSecond += regenPerSecond;
    }
}
