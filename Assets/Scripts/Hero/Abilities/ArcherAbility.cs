using UnityEngine;

// Лучник: 1 атака, способность — мощный выстрел
public class ArcherAbility : HeroAbility
{
    [Header("Обычный выстрел")]
    public GameObject arrowPrefab;
    public float arrowDamage = 20f;
    public float arrowSpeed = 15f;
    public Transform shootPoint;

    [Header("Мощный выстрел")]
    public GameObject powerArrowPrefab;
    public float powerArrowDamage = 80f;
    public float powerArrowSpeed = 25f;

    private SpriteRenderer spriteRenderer;

    protected override void Awake()
    {
        base.Awake();
        spriteRenderer = GetComponent<SpriteRenderer>();
        ability1Cooldown = 6f;
    }

    private Vector2 GetShootDirection()
    {
        float dirX = spriteRenderer != null && spriteRenderer.flipX ? -1f : 1f;
        return Vector2.right * dirX;
    }

    public override void Attack1()
    {
        animator.SetTrigger("Attack1");
        if (arrowPrefab != null && shootPoint != null)
        {
            var arrow = Instantiate(arrowPrefab, shootPoint.position, shootPoint.rotation);
            var proj = arrow.GetComponent<Projectile>();
            if (proj != null)
                proj.Init(arrowDamage, GetShootDirection(), arrowSpeed);
        }
    }

    // Лучник имеет только 1 атаку — Attack2 не используется
    public override void Attack2() { }

    protected override void OnAbility1()
    {
        animator.SetTrigger("Ability1");
        if (powerArrowPrefab != null && shootPoint != null)
        {
            var arrow = Instantiate(powerArrowPrefab, shootPoint.position, shootPoint.rotation);
            var proj = arrow.GetComponent<Projectile>();
            if (proj != null)
                proj.Init(powerArrowDamage, GetShootDirection(), powerArrowSpeed);
        }
    }
}
