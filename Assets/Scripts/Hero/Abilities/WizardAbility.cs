using Mirror;
using UnityEngine;

// Маг: 1 атака (огненный шар с взрывом), способность — AoE взрыв на ближайшем враге
public class WizardAbility : HeroAbility
{
    [Header("Атака — огненный шар")]
    public float fireballSpeed = 14f;
    public float castOffset = 0.5f;

    [Header("Ability1 — магический взрыв на враге")]
    public float aoeDamage = 60f;
    public float aoeRange = 6f;

    // Assigned from HeroData.projectilePrefabs
    private GameObject fireballPrefab;
    private GameObject aoeExplosionPrefab;

    protected override void Awake()
    {
        base.Awake();
    }

    public override void ApplyHeroData(HeroData data)
    {
        base.ApplyHeroData(data);
        if (data.projectilePrefabs != null)
        {
            if (data.projectilePrefabs.Length > 0) fireballPrefab = data.projectilePrefabs[0];
            if (data.projectilePrefabs.Length > 1) aoeExplosionPrefab = data.projectilePrefabs[1];
        }
    }

    // Client-side: only animation
    public override void Attack1()
    {
        PlayTrigger("Attack1");
    }

    public override void Attack2() { }

    // Server-side: spawn fireball
    public override void ServerAttack(int attackIndex, float damage, float energyGain, bool flipX)
    {
        if (fireballPrefab == null) return;

        Vector2 dir = flipX ? Vector2.left : Vector2.right;
        Vector3 spawnPos = transform.position + (Vector3)(dir * castOffset);
        var spell = Instantiate(fireballPrefab, spawnPos, Quaternion.identity);
        var proj = spell.GetComponent<Projectile>();
        if (proj != null)
            proj.Init(damage, dir, fireballSpeed, gameObject, energyGain);

        NetworkServer.Spawn(spell);
    }

    protected override void OnAbility1()
    {
        PlayTrigger("Ability1");
    }

    // Server-side: AoE explosion on nearest enemy
    public override void ServerAbility1(bool flipX)
    {
        if (aoeExplosionPrefab == null) return;

        Transform target = FindNearestEnemy();
        if (target == null) return;

        var explosionObj = Instantiate(aoeExplosionPrefab, target.position, Quaternion.identity);
        var aoe = explosionObj.GetComponent<AoeExplosion>();
        if (aoe != null)
            aoe.Init(aoeDamage);

        NetworkServer.Spawn(explosionObj);
    }

    private Transform FindNearestEnemy()
    {
        float bestDist = aoeRange;
        Transform best = null;

        var hits = Physics2D.OverlapCircleAll(transform.position, aoeRange);
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
