using UnityEngine;

// Рыцарь: 2 атаки, способность — огненный удар мечом
public class KnightAbility : HeroAbility
{
    [Header("Атака 1 — базовый удар")]
    public float attack1Damage = 15f;

    [Header("Атака 2 — усиленный удар")]
    public float attack2Damage = 25f;

    [Header("Огненный удар (Ability1)")]
    public float fireStrikeDamage = 50f;
    public GameObject fireEffectPrefab;

    public float DamageMultiplier { get; set; } = 1f;

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
        PrepareHitbox(2, fireStrikeDamage);
        PlayTrigger("Attack3");

        if (fireEffectPrefab != null)
        {
            var fx = Instantiate(fireEffectPrefab, transform.position, Quaternion.identity);
            Destroy(fx, 2f);
        }
    }

    public bool IsShieldActive() => false;
}
