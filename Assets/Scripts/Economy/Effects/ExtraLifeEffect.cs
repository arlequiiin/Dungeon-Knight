using UnityEngine;

[CreateAssetMenu(fileName = "ExtraLife", menuName = "Dungeon Knight/Reward Effect/Extra Life")]
public class ExtraLifeEffect : RewardEffect
{
    public override void Apply(HeroStats stats, RunModifiers mods)
    {
        if (mods == null) return;
        mods.extraLifeAvailable = true;
    }
}
