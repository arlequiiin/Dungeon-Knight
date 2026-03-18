using UnityEngine;

/// <summary>
/// Skeleton Warrior: basic melee mob.
/// Single attack (Attack1), standard behavior.
/// All stats come from MobData.
/// </summary>
public class SkeletonWarriorAI : MobAI
{
    protected override void PerformAttack()
    {
        FaceTarget();
        PrepareHitbox(0, GetAttackDamage(0));
        animator.SetTrigger("Attack1");
        RpcPlayTrigger("Attack1");
    }
}
