using UnityEngine;

[CreateAssetMenu(fileName = "SharpBlade", menuName = "Dungeon Knight/Reward Effect/Sharp Blade")]
public class SharpBladeEffect : RewardEffect
{
    [Range(0f, 1f)] public float bonusPercent = 0.15f;

    public override void Apply(HeroStats stats, RunModifiers mods)
    {
        if (mods == null) return;
        mods.attackDamageBonus += bonusPercent;
    }
}
