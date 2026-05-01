using UnityEngine;

[CreateAssetMenu(fileName = "AbilityPower", menuName = "Dungeon Knight/Reward Effect/Ability Power")]
public class AbilityPowerEffect : RewardEffect
{
    [Range(0f, 1f)] public float bonusPercent = 0.3f;

    public override void Apply(HeroStats stats, RunModifiers mods)
    {
        if (mods == null) return;
        mods.abilityPowerBonus += bonusPercent;
    }
}
