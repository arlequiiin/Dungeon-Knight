using UnityEngine;

/// <summary>
/// Skeleton Greatsword: slow but powerful.
/// 3 attacks with weighted probabilities. Long recovery — punish window.
/// All stats come from MobData.
/// </summary>
public class SkeletonGreatswordAI : MobAI
{
    private static readonly string[] TriggerNames = { "Attack1", "Attack2", "Attack3" };

    protected override void PerformAttack()
    {
        FaceTarget();

        int attack = ChooseWeightedAttack();
        float damage = GetAttackDamage(attack);
        string trigger = attack < TriggerNames.Length ? TriggerNames[attack] : "Attack1";

        PrepareHitbox(attack, damage);
        animator.SetTrigger(trigger);
        RpcPlayTrigger(trigger);
    }
}
