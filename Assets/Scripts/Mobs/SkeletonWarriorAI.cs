using UnityEngine;

/// <summary>
/// AI скелетона-воина: базовый мили-моб.
/// Одна атака (Attack1), средняя скорость, стандартное поведение.
/// </summary>
public class SkeletonWarriorAI : MobAI
{
    [Header("Урон")]
    public float attack1Damage = 10f;

    protected override void PerformAttack()
    {
        FaceTarget();
        PrepareHitbox(0, attack1Damage);
        animator.SetTrigger("Attack1");
    }

    protected override float RecoveryDuration => 0.4f;
}
