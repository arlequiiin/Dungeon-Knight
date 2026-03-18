using Mirror;
using UnityEngine;

// Священник: 1 атака (AoE на ближайшем враге), способность — AoE лечение союзников
public class PriestAbility : HeroAbility
{
    [Header("Атака — магический взрыв на враге")]
    public float attackDamage = 20f;
    public float attackRange = 6f;

    [Header("Ability1 — AoE лечение")]
    public float healAmount = 40f;
    public float healRadius = 5f;

    // Assigned from HeroData.projectilePrefabs
    private GameObject attackExplosionPrefab;  // [0]
    private GameObject healEffectPrefab;       // [1]

    protected override void Awake()
    {
        base.Awake();
    }

    public override void ApplyHeroData(HeroData data)
    {
        base.ApplyHeroData(data);
        if (data.projectilePrefabs != null)
        {
            if (data.projectilePrefabs.Length > 0) attackExplosionPrefab = data.projectilePrefabs[0];
            if (data.projectilePrefabs.Length > 1) healEffectPrefab = data.projectilePrefabs[1];
        }
    }

    // Client-side: only animation
    public override void Attack1()
    {
        PlayTrigger("Attack1");
    }

    public override void Attack2() { }

    // Server-side: AoE damage on nearest enemy
    public override void ServerAttack(int attackIndex, float damage, bool flipX)
    {
        if (attackExplosionPrefab == null) return;

        Transform target = FindNearestEnemy();
        if (target == null) return;

        var explosionObj = Instantiate(attackExplosionPrefab, target.position, Quaternion.identity);
        var aoe = explosionObj.GetComponent<AoeExplosion>();
        if (aoe != null)
            aoe.Init(damage);

        NetworkServer.Spawn(explosionObj);
    }

    protected override void OnAbility1()
    {
        PlayTrigger("Ability1");
    }

    // Server-side: AoE heal on self position
    public override void ServerAbility1(bool flipX)
    {
        // Heal self
        var selfStats = GetComponent<HeroStats>();
        if (selfStats != null)
            selfStats.Heal(healAmount);

        // Heal allies in radius
        var colliders = Physics2D.OverlapCircleAll(transform.position, healRadius);
        foreach (var col in colliders)
        {
            if (col.gameObject == gameObject) continue;
            var allyStats = col.GetComponent<HeroStats>();
            if (allyStats != null && !allyStats.IsDead)
                allyStats.Heal(healAmount);
        }

        // VFX
        if (healEffectPrefab != null)
        {
            var fx = Instantiate(healEffectPrefab, transform.position, Quaternion.identity);
            NetworkServer.Spawn(fx);
            var cleanup = fx.AddComponent<TimedNetworkDestroy>();
            cleanup.delay = 2f;
        }
    }

    private Transform FindNearestEnemy()
    {
        float bestDist = attackRange;
        Transform best = null;

        var hits = Physics2D.OverlapCircleAll(transform.position, attackRange);
        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            var mob = hit.GetComponent<MobHealth>();
            if (mob == null || mob.IsDead) continue;

            float dist = Vector2.Distance(transform.position, hit.transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = hit.transform;
            }
        }

        return best;
    }
}
