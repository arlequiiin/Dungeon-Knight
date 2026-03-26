using Mirror;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Skeleton Archer: ranged mob.
/// Keeps distance from target, spawns arrow projectile.
/// Retreats if player gets too close.
/// </summary>
public class SkeletonArcherAI : MobAI
{
    [Header("Projectile")]
    public float arrowSpeed = 10f;
    public float shootOffset = 0.3f;
    public float shootHeightOffset = 0.2f;

    [Header("Retreat")]
    [Tooltip("If target is closer than this, mob retreats")]
    public float retreatRange = 2.5f;

    protected override void Awake()
    {
        base.Awake();
    }

    /// <summary>
    /// Override: ranged mob retreats if player is too close,
    /// stands and attacks if at optimal range.
    /// </summary>
    protected override void UpdateAttack()
    {
        if (target == null || !IsTargetAlive())
        {
            target = null;
            ResumeAgent();
            state = State.Patrol;
            SetPatrolDestination();
            return;
        }

        float dist = Vector2.Distance(transform.position, target.position);

        // Too far — chase
        if (dist > attackRange * 1.2f)
        {
            ResumeAgent();
            state = State.Chase;
            return;
        }

        // Too close — retreat
        if (dist < retreatRange)
        {
            ResumeAgent();
            Vector2 awayDir = ((Vector2)(transform.position - target.position)).normalized;
            Vector3 retreatPos = transform.position + (Vector3)(awayDir * attackRange);

            if (NavMesh.SamplePosition(retreatPos, out NavMeshHit hit, attackRange, NavMesh.AllAreas))
                agent.SetDestination(hit.position);

            FaceTarget();
            return;
        }

        // In range — stop and attack
        StopAgent();
        FaceTarget();

        if (attackTimer <= 0f)
        {
            state = State.AttackWindup;
            windupTimer = attackWindupDuration;
        }
    }

    protected override void PerformAttack()
    {
        FaceTarget();
        PrepareProjectile();
        animator.SetTrigger("Attack1");
        RpcPlayTrigger("Attack1");
    }

    /// <summary>
    /// Called from Animation Event (SpawnProjectile) on the arrow release frame.
    /// </summary>
    protected override void ServerSpawnProjectile()
    {
        if (target == null) return;

        Vector2 dir = target.position.x < transform.position.x ? Vector2.left : Vector2.right;
        Vector3 spawnPos = transform.position + (Vector3)(dir * shootOffset) + Vector3.up * shootHeightOffset;

        var arrowPrefab = mobData != null ? mobData.projectilePrefab : null;
        if (arrowPrefab == null)
        {
            Debug.LogWarning($"[SkeletonArcherAI] projectilePrefab not assigned in MobData on {gameObject.name}!");
            return;
        }

        var arrow = Instantiate(arrowPrefab, spawnPos, Quaternion.identity);
        var proj = arrow.GetComponent<Projectile>();
        if (proj != null)
            proj.Init(GetAttackDamage(0), dir, arrowSpeed, gameObject);

        NetworkServer.Spawn(arrow);
    }
}
