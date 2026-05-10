using Mirror;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Универсальный дальнобойный моб. Держит дистанцию, спавнит снаряд по Animation Event.
/// Снаряд берётся из MobData.projectilePrefab, имя триггера — из attackTriggers (fallback Attack1).
/// Отступает если игрок подходит ближе retreatRange.
/// </summary>
public class RangedMobAI : MobAI
{
    [Header("Projectile")]
    public float projectileSpeed = 10f;
    public float shootOffset = 0.3f;
    public float shootHeightOffset = 0.2f;

    [Header("Retreat")]
    [Tooltip("Если цель ближе этого расстояния — моб отступает.")]
    public float retreatRange = 2.5f;

    // Направление выстрела фиксируется в PerformAttack (момент старта анимации) —
    // чтобы стрела летела туда, куда моб начал замах, даже если игрок успеет
    // обежать его за время анимации.
    private Vector2 lockedShotDirection = Vector2.right;

    protected override void UpdateAttack()
    {
        if (target == null || !IsTargetAlive())
        {
            SetTarget(null);
            ResumeAgent();
            state = State.Patrol;
            SetPatrolDestination();
            return;
        }

        float dist = Vector2.Distance(transform.position, target.position);

        // Слишком далеко — догоняем
        if (dist > attackRange * 1.2f)
        {
            ResumeAgent();
            state = State.Chase;
            return;
        }

        // Слишком близко — отступаем
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

        // В радиусе — стоим и атакуем
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

        // Защёлкиваем направление выстрела на момент замаха — иначе игрок может пробежать
        // мимо во время windup'а и стрела полетит назад.
        if (target != null)
            lockedShotDirection = target.position.x < transform.position.x ? Vector2.left : Vector2.right;

        // Дальние мобы обычно используют атаку 0; если их несколько — можно ввести веса.
        int attack = ChooseWeightedAttack();
        string trigger = GetAttackTrigger(attack);

        animator.SetTrigger(trigger);
        RpcPlayTrigger(trigger);
    }

    protected override void ServerSpawnProjectile()
    {
        var prefab = mobData != null ? mobData.projectilePrefab : null;
        if (prefab == null)
        {
            Debug.LogWarning($"[RangedMobAI] projectilePrefab не назначен в MobData на {gameObject.name}!");
            return;
        }

        Vector2 dir = lockedShotDirection;
        Vector3 spawnPos = transform.position + (Vector3)(dir * shootOffset) + Vector3.up * shootHeightOffset;

        var projectile = Instantiate(prefab, spawnPos, Quaternion.identity);
        var proj = projectile.GetComponent<Projectile>();
        if (proj != null)
            proj.Init(GetAttackDamage(0), dir, projectileSpeed, gameObject);

        NetworkServer.Spawn(projectile);
    }
}
