using UnityEngine;

// Солдат: 2 мили-атаки, способность — мощный выстрел из лука
public class SoldierAbility : HeroAbility
{
    [Header("Атака")]
    public float attack1Damage = 15f;
    public float attack2Damage = 25f;

    [Header("Мощный выстрел")]
    public GameObject powerArrowPrefab;
    public float powerArrowDamage = 60f;
    public float powerArrowSpeed = 20f;
    public Transform shootPoint;

    private SpriteRenderer spriteRenderer;

    protected override void Awake()
    {
        base.Awake();
        spriteRenderer = GetComponent<SpriteRenderer>();
        ability1Cooldown = 8f;
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
        animator.SetTrigger("Ability1");
        if (powerArrowPrefab != null && shootPoint != null)
        {
            var arrow = Instantiate(powerArrowPrefab, shootPoint.position, shootPoint.rotation);
            float dirX = spriteRenderer != null && spriteRenderer.flipX ? -1f : 1f;
            var proj = arrow.GetComponent<Projectile>();
            if (proj != null)
                proj.Init(powerArrowDamage, Vector2.right * dirX, powerArrowSpeed);
        }
    }
}
