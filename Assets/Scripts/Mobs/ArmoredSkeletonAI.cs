using UnityEngine;

/// <summary>
/// AI бронированного скелетона: танк со щитом.
/// 2 атаки. Не прерывается ударами во время атаки (суперармор).
/// Щит (IDEA-003) — будет реализован отдельно.
/// </summary>
public class ArmoredSkeletonAI : MobAI
{
    [Header("Урон")]
    public float attack1Damage = 8f;
    public float attack2Damage = 14f;

    [Header("Шанс тяжёлой атаки")]
    [Range(0f, 1f)] public float heavyAttackChance = 0.4f;

    protected override float RecoveryDuration => 0.5f;
    protected override bool CanBeInterrupted => false;

    protected override void PerformAttack()
    {
        FaceTarget();

        bool heavy = Random.value < heavyAttackChance;
        int attack = heavy ? 1 : 0;
        float damage = heavy ? attack2Damage : attack1Damage;

        PrepareHitbox(attack, damage);
        animator.SetTrigger(heavy ? "Attack2" : "Attack1");
    }
}
