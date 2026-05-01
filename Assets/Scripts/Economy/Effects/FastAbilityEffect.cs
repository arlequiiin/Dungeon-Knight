using UnityEngine;

[CreateAssetMenu(fileName = "FastAbility", menuName = "Dungeon Knight/Reward Effect/Fast Ability")]
public class FastAbilityEffect : RewardEffect
{
    [Range(0f, 0.9f)] public float reduction = 0.2f;

    public override void Apply(HeroStats stats, RunModifiers mods)
    {
        if (mods == null) return;
        mods.abilityCooldownReduction = Mathf.Min(0.9f, mods.abilityCooldownReduction + reduction);
    }
}
