using UnityEngine;

/// <summary>
/// AI скелетона с двуручным мечом: медленный, но сильный.
/// 3 атаки с разным уроном. Долгий recovery после атаки — окно для наказания.
/// Короткий flinch (тяжёлый моб, сложнее прервать).
/// </summary>
public class SkeletonGreatswordAI : MobAI
{
    [Header("Урон")]
    public float attack1Damage = 10f;
    public float attack2Damage = 14f;
    public float attack3Damage = 20f;

    [Header("Веса атак (вероятность выбора)")]
    [Range(0f, 1f)] public float attack1Weight = 0.5f;
    [Range(0f, 1f)] public float attack2Weight = 0.35f;
    // attack3Weight = остаток (1 - attack1 - attack2)

    protected override float RecoveryDuration => 0.8f;
    protected override float HitReactionDuration => 0.2f;

    protected override void PerformAttack()
    {
        FaceTarget();

        int attack = ChooseAttack();
        float damage = GetDamageForAttack(attack);

        PrepareHitbox(attack, damage);
        animator.SetTrigger(GetTriggerName(attack));
    }

    private int ChooseAttack()
    {
        float roll = Random.value;

        if (roll < attack1Weight)
            return 0;
        if (roll < attack1Weight + attack2Weight)
            return 1;
        return 2;
    }

    private float GetDamageForAttack(int index)
    {
        return index switch
        {
            0 => attack1Damage,
            1 => attack2Damage,
            2 => attack3Damage,
            _ => attack1Damage
        };
    }

    private string GetTriggerName(int index)
    {
        return index switch
        {
            0 => "Attack1",
            1 => "Attack2",
            2 => "Attack3",
            _ => "Attack1"
        };
    }
}
