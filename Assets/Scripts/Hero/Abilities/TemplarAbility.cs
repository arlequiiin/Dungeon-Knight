using UnityEngine;

// Тамплиер: 2 атаки, способности — двойная атака и щит
public class TemplarAbility : HeroAbility
{
    [Header("Атаки")]
    public float attack1Damage = 15f;
    public float attack2Damage = 25f;

    [Header("Двойная атака")]
    public float doubleAttackDamage = 35f;
    public float doubleAttackHitboxDuration = 0.2f;
    public float timeBetweenHits = 0.3f;

    [Header("Щит")]
    public float shieldDuration = 4f;
    public float damageReduction = 0.6f;
    private bool shieldActive;

    protected override void Awake()
    {
        base.Awake();
        ability1Cooldown = 8f;
        ability2Cooldown = 18f;
    }

    public override void Attack1()
    {
        PrepareHitbox(0, attack1Damage);
        animator.SetTrigger("Attack1");
    }

    public override void Attack2()
    {
        PrepareHitbox(1, attack2Damage);
        animator.SetTrigger("Attack2");
    }

    protected override void OnAbility1()
    {
        PrepareHitbox(2, doubleAttackDamage);
        animator.SetTrigger("Ability1");
        // Первый удар — через Animation Event (EnableHitbox/DisableHitbox)
        // Второй удар — через Invoke
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

    protected override void OnAbility2()
    {
        if (shieldActive) return;
        animator.SetTrigger("Ability2");
        shieldActive = true;
        Invoke(nameof(DeactivateShield), shieldDuration);
    }

    private void DeactivateShield()
    {
        shieldActive = false;
    }
}
