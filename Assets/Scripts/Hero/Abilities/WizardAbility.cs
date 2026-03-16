using UnityEngine;

// Маг: 2 атаки (заготовка, детали способностей уточняются)
public class WizardAbility : HeroAbility
{
    [Header("Атака 1 — базовый снаряд")]
    public GameObject spellPrefab;
    public float spellDamage = 25f;
    public float spellSpeed = 14f;
    public Transform castPoint;

    [Header("Атака 2 — усиленный снаряд")]
    public GameObject heavySpellPrefab;
    public float heavySpellDamage = 45f;
    public float heavySpellSpeed = 12f;

    private SpriteRenderer spriteRenderer;

    protected override void Awake()
    {
        base.Awake();
        spriteRenderer = GetComponent<SpriteRenderer>();
        ability1Cooldown = 8f;
        ability2Cooldown = 15f;
    }

    private Vector2 GetCastDirection()
    {
        float dirX = spriteRenderer != null && spriteRenderer.flipX ? -1f : 1f;
        return Vector2.right * dirX;
    }

    public override void Attack1()
    {
        animator.SetTrigger("Attack1");
        if (spellPrefab != null && castPoint != null)
        {
            var spell = Instantiate(spellPrefab, castPoint.position, castPoint.rotation);
            var proj = spell.GetComponent<Projectile>();
            if (proj != null)
                proj.Init(spellDamage, GetCastDirection(), spellSpeed);
        }
    }

    public override void Attack2()
    {
        animator.SetTrigger("Attack2");
        if (heavySpellPrefab != null && castPoint != null)
        {
            var spell = Instantiate(heavySpellPrefab, castPoint.position, castPoint.rotation);
            var proj = spell.GetComponent<Projectile>();
            if (proj != null)
                proj.Init(heavySpellDamage, GetCastDirection(), heavySpellSpeed);
        }
    }

    protected override void OnAbility1()
    {
        animator.SetTrigger("Ability1");
    }

    protected override void OnAbility2()
    {
        animator.SetTrigger("Ability2");
    }
}
