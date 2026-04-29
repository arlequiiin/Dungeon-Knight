using UnityEngine;

/// <summary>
/// Armored Skeleton: tank with shield.
/// 2 attacks (light/heavy). Super armor during attack (can't be interrupted).
/// All stats come from MobData.
/// </summary>
public class ArmoredSkeletonAI : MobAI
{
    protected override void PerformAttack()
    {
        FaceTarget();

        int attack = ChooseWeightedAttack();
        float damage = GetAttackDamage(attack);
        string trigger = attack == 0 ? "Attack1" : "Attack2";

        PrepareHitbox(attack, damage, GetAttackStaggerDamage(attack));
        animator.SetTrigger(trigger);
        RpcPlayTrigger(trigger);
    }
}
