using Mirror;
using UnityEngine;

// Рыцарь: 2 атаки, способности — огненный удар мечом и щит
public class KnightAbility : HeroAbility
{
    [Header("Атака 1 — базовый удар")]
    public float attack1Damage = 15f;

    [Header("Атака 2 — усиленный удар")]
    public float attack2Damage = 25f;

    [Header("Огненный удар (Ability1)")]
    public float fireStrikeDamage = 50f;
    public GameObject fireEffectPrefab;

    [Header("Щит (Ability2)")]
    public float shieldDuration = 3f;
    [Range(0f, 1f)]
    public float damageReduction = 0.5f;
    public GameObject shieldEffectPrefab;

    public float DamageMultiplier { get; private set; } = 1f;

    private bool shieldActive;
    private HeroStats stats;

    protected override void Awake()
    {
        base.Awake();
        stats = GetComponent<HeroStats>();
        ability1Cooldown = 10f;
        ability2Cooldown = 15f;
    }

    public override void Attack1()
    {
        PrepareHitbox(0, attack1Damage);
        animator.SetTrigger("Attack1");
        // EnableHitbox / DisableHitbox вызываются из Animation Event
    }

    public override void Attack2()
    {
        PrepareHitbox(1, attack2Damage);
        animator.SetTrigger("Attack2");
    }

    protected override void OnAbility1()
    {
        PrepareHitbox(2, fireStrikeDamage);
        animator.SetTrigger("Attack3");

        if (fireEffectPrefab != null)
        {
            var fx = Instantiate(fireEffectPrefab, transform.position, Quaternion.identity);
            Destroy(fx, 2f);
        }
    }

    protected override void OnAbility2()
    {
        if (shieldActive) return;

        animator.SetTrigger("Block");
        shieldActive = true;
        DamageMultiplier = 1f - damageReduction;

        if (shieldEffectPrefab != null)
        {
            var fx = Instantiate(shieldEffectPrefab, transform.position, transform.rotation);
            fx.transform.SetParent(transform);
            Destroy(fx, shieldDuration);
        }

        Invoke(nameof(DeactivateShield), shieldDuration);
    }

    private void DeactivateShield()
    {
        shieldActive = false;
        DamageMultiplier = 1f;
    }

    public bool IsShieldActive() => shieldActive;
}
