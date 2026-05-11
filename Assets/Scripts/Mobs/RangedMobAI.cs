using Mirror;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Универсальный дальнобойный моб. Держит дистанцию, спавнит снаряд по Animation Event.
/// Стреляет ТОЛЬКО горизонтально (top-down рогалик: герои/мобы атакуют влево/вправо),
/// поэтому позиционируется так, чтобы цель была на той же Y (с допуском yAlignThreshold).
/// Если цель выше/ниже — двигается по Y, не стреляет.
/// Снаряд берётся из MobData.projectilePrefab, имя триггера — из attackTriggers (fallback Attack1).
/// </summary>
public class RangedMobAI : MobAI
{
    [Header("Projectile")]
    public float projectileSpeed = 10f;
    public float shootOffset = 0.3f;
    public float shootHeightOffset = 0.2f;

    [Header("Positioning")]
    [Tooltip("Если цель ближе этого расстояния — моб отступает.")]
    public float retreatRange = 3.5f;

    [Tooltip("Допуск выравнивания по Y. Стрелять можно только если |targetY - selfY| < этого значения. " +
             "0.6 ≈ рост персонажа.")]
    public float yAlignThreshold = 0.6f;

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

        Vector2 self = transform.position;
        Vector2 tgt = target.position;
        float dx = Mathf.Abs(tgt.x - self.x);
        float dy = Mathf.Abs(tgt.y - self.y);
        float dist2D = Vector2.Distance(self, tgt);

        // Слишком далеко (по 2D) — догоняем через Chase.
        if (dist2D > attackRange * 1.2f)
        {
            ResumeAgent();
            state = State.Chase;
            return;
        }

        // Слишком близко — отступаем (направление: подальше от цели).
        if (dist2D < retreatRange)
        {
            ResumeAgent();
            Vector2 awayDir = (self - tgt).normalized;
            Vector3 retreatPos = (Vector3)(self + awayDir * attackRange);

            if (NavMesh.SamplePosition(retreatPos, out NavMeshHit hit, attackRange, NavMesh.AllAreas))
                agent.SetDestination(hit.position);

            FaceTarget();
            return;
        }

        // Не на одной горизонтали — выравниваемся по Y, держа дистанцию по X.
        // Двигаемся к точке (self.x, target.y) с лёгким смещением по X в сторону attackRange,
        // если по X слишком близко (иначе будем стоять впритык).
        if (dy > yAlignThreshold)
        {
            ResumeAgent();
            float desiredX = self.x;
            if (dx < retreatRange)
            {
                float sign = self.x >= tgt.x ? 1f : -1f;
                desiredX = tgt.x + sign * Mathf.Max(retreatRange, attackRange * 0.7f);
            }
            Vector3 alignPos = new Vector3(desiredX, tgt.y, 0f);
            if (NavMesh.SamplePosition(alignPos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);

            FaceTarget();
            return;
        }

        // Выровнены по Y и в радиусе. Запрашиваем shoot-slot (лимит одновременных стрелков на цель).
        if (groupManager != null && !groupManager.RequestShootSlot(this, target))
        {
            // Слот занят — лёгкий шафл по Y/X чтобы не стоять статуей; ждём пока освободится.
            ResumeAgent();
            FaceTarget();
            // Стой на месте — слот освободится после выстрела соседа.
            StopAgent();
            return;
        }

        // Слот получен, всё ок — стоим, стреляем.
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

        // Снаряд ушёл — освобождаем shoot-slot, чтобы следующий лучник смог занять его.
        if (groupManager != null)
            groupManager.ReleaseShootSlot(this);
    }
}
