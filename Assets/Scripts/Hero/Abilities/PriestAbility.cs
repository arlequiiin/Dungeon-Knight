using Mirror;
using UnityEngine;

// Священник: 1 атака, способность — лечение союзников
public class PriestAbility : HeroAbility
{
    [Header("Атака")]
    public float attackDamage = 15f;

    [Header("Лечение")]
    public float healAmount = 40f;
    public float healRadius = 5f;
    public GameObject healEffectPrefab;

    private HeroStats stats;

    protected override void Awake()
    {
        base.Awake();
        stats = GetComponent<HeroStats>();
        ability1Cooldown = 10f;
    }

    public override void Attack1()
    {
        PrepareHitbox(0, attackDamage);
        animator.SetTrigger("Attack1");
    }

    public override void Attack2() { }

    protected override void OnAbility1()
    {
        animator.SetTrigger("Ability1");

        if (healEffectPrefab != null)
            Instantiate(healEffectPrefab, transform.position, Quaternion.identity);

        if (!NetworkServer.active) return;

        if (stats != null)
            stats.Heal(healAmount);

        var colliders = Physics2D.OverlapCircleAll(transform.position, healRadius);
        foreach (var col in colliders)
        {
            if (col.gameObject == gameObject) continue;
            var allyStats = col.GetComponent<HeroStats>();
            if (allyStats != null)
                allyStats.Heal(healAmount);
        }
    }
}
