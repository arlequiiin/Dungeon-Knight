using UnityEngine;

// Тамплиер: 2 атаки, способность — двойная атака
public class TemplarAbility : HeroAbility
{
    [Header("Атаки")]
    public float attack1Damage = 15f;
    public float attack2Damage = 25f;

    [Header("Двойная атака")]
    public float doubleAttackDamage = 35f;
    public float doubleAttackHitboxDuration = 0.2f;
    public float timeBetweenHits = 0.3f;

    protected override void Awake()
    {
        base.Awake();
    }

    public override void Attack1()
    {
        PrepareHitbox(0, attack1Damage);
        PlayTrigger("Attack1");
    }

    public override void Attack2()
    {
        PrepareHitbox(1, attack2Damage);
        PlayTrigger("Attack2");
    }

    protected override void OnAbility1()
    {
        PrepareHitbox(2, doubleAttackDamage);
        PlayTrigger("Ability1");
        Invoke(nameof(SecondHit), timeBetweenHits);
    }

    private void SecondHit()
    {
        var hitbox = GetHitbox(2);
        if (hitbox != null)
        {
            hitbox.Activate(doubleAttackDamage);
            Invoke(nameof(DeactivateSecondHit), doubleAttackHitboxDuration);
        }
    }

    private void DeactivateSecondHit()
    {
        var hitbox = GetHitbox(2);
        if (hitbox != null)
            hitbox.Deactivate();
    }
}
