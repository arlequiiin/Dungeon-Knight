using UnityEngine;

[CreateAssetMenu(fileName = "StoneSkin", menuName = "Dungeon Knight/Reward Effect/Stone Skin")]
public class StoneSkinEffect : RewardEffect
{
    [Range(0f, 0.9f)] public float resistance = 0.15f;

    public override void Apply(HeroStats stats, RunModifiers mods)
    {
        if (mods == null) return;
        mods.damageResistance = Mathf.Min(0.9f, mods.damageResistance + resistance);
    }
}
