using UnityEngine;

[CreateAssetMenu(fileName = "UnlockSecondAttack", menuName = "Dungeon Knight/Reward Effect/Unlock Second Attack")]
public class UnlockSecondAttackEffect : RewardEffect
{
    public override void Apply(HeroStats stats, RunModifiers mods)
    {
        if (mods == null) return;
        mods.attack2Unlocked = true;
    }
}
