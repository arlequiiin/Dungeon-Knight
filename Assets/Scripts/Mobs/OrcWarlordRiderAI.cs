using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Босс Orc Warlord Rider — активный мили-босс с тремя атаками и саммоном миньонов.
///   • Attack1 — резкий dash к игроку и удар вплотную, после чего короткий backoff.
///   • Attack2 — мах булавой на средней дистанции (без dash'а).
///   • Attack3 — круговой удар в упор, выбирается с шансом когда игрок вплотную.
///   • Summon  — раз в SummonInterval секунд останавливается и спавнит миньонов.
///     После саммона следующие PostSummonDuration секунд босс действует пассивнее.
/// Блок: реализован через MobHealth.TryBlock (hasShield=true на префабе). Здесь логики не нужно.
/// </summary>
public class OrcWarlordRiderAI : MobAI
{
    [Header("Dash (Attack1)")]
    [Tooltip("Множитель скорости во время dash'а к игроку.")]
    public float dashSpeedMultiplier = 2.2f;
    [Tooltip("Максимальное время dash'а до перехода в атаку, сек. Защита от застревания.")]
    public float dashMaxDuration = 1.2f;
    [Tooltip("Дистанция, на которой dash считается завершённым и переходит в Attack1.")]
    public float dashAttackRange = 1.3f;

    [Header("Backoff")]
    [Tooltip("Дистанция отступления после Attack1.")]
    public float backoffDistance = 2.5f;
    [Tooltip("Длительность фазы backoff, сек.")]
    public float backoffDuration = 0.6f;

    [Header("Decision ranges")]
    [Tooltip("Игрок ближе — катим шанс Attack3 (spin), иначе backoff.")]
    public float spinRange = 1.4f;
    [Tooltip("Игрок в этой полосе — Attack2 (mace).")]
    public float maceMinRange = 1.4f;
    public float maceMaxRange = 3.0f;
    [Tooltip("Игрок в этой полосе — шанс Dash, иначе сближаемся spокойно.")]
    public float dashMinRange = 3.0f;
    public float dashMaxRange = 6.0f;
    [Tooltip("Желаемая позиция для maceRange (куда идём при репозиции).")]
    public float desiredCloseRange = 2.2f;

    [Header("Probabilities")]
    [Range(0f, 1f)] public float spinChance = 0.4f;
    [Range(0f, 1f)] public float dashChance = 0.5f;

    [Header("Summon")]
    [Tooltip("Префабы миньонов (один из них выбирается случайно при каждом спавне).")]
    public GameObject[] summonPrefabs;
    [Tooltip("Сколько миньонов за раз.")]
    public int summonCount = 3;
    [Tooltip("Период между саммонами, сек.")]
    public float summonInterval = 27f;
    [Tooltip("Задержка перед первым саммоном после начала боя, сек.")]
    public float firstSummonDelay = 12f;
    [Tooltip("Длительность Summon-фазы (стоит на месте, спавн в середине).")]
    public float summonStateDuration = 1.0f;
    [Tooltip("Сколько секунд после саммона босс действует пассивнее.")]
    public float postSummonDuration = 25f;
    [Tooltip("Радиус вокруг босса, в котором спавнятся миньоны.")]
    public float summonSpawnRadius = 2f;

    [Header("Pacing")]
    [Tooltip("Минимальная пауза между активными решениями (Attack/Dash/Reposition).")]
    public float minDecisionInterval = 0.5f;
    [Tooltip("Максимальная пауза.")]
    public float maxDecisionInterval = 1.4f;

    private enum BossAction { Idle, Reposition, DashIn, Backoff, Summon }
    private BossAction action = BossAction.Idle;

    private float summonTimer;
    private float postSummonTimer;
    private float dashTimer;
    private float backoffTimer;
    private float decisionTimer;

    private Vector2 backoffDir;
    private float baseMoveSpeed;
    private int pendingAttackIndex; // 0 = Attack1, 1 = Attack2, 2 = Attack3
    private bool forceBackoffAfterAttack;

    public override void OnStartServer()
    {
        base.OnStartServer();
        // baseMoveSpeed читаем после ApplyMobData (Init выставил agent.speed).
        if (agent != null) baseMoveSpeed = agent.speed;
        summonTimer = firstSummonDelay;
    }

    private bool InPostSummon => postSummonTimer > 0f;

    // === Overrides ===

    protected override void PerformAttack()
    {
        FaceTarget();
        PrepareHitbox(pendingAttackIndex, GetAttackDamage(pendingAttackIndex), GetAttackStaggerDamage(pendingAttackIndex));

        string trigger = GetAttackTrigger(pendingAttackIndex);
        animator.SetTrigger(trigger);
        RpcPlayTrigger(trigger);
    }

    /// <summary>
    /// Заменяем стандартную логику Attack-фазы: внутри неё крутится sub-FSM босса.
    /// State.Chase в base-классе пусть остаётся — он подведёт босса к игроку,
    /// а как только dist <= attackRange + 0.5 мы попадаем сюда и принимаем решения.
    /// </summary>
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

        // Таймеры саммона работают всегда, пока есть цель.
        TickTimers();

        // После Attack1 (dash + удар) — гарантированный backoff.
        if (forceBackoffAfterAttack)
        {
            forceBackoffAfterAttack = false;
            BeginBackoff();
            return;
        }

        // Саммон имеет приоритет над всем остальным (кроме смерти/стагера, что handled базой).
        if (action != BossAction.Summon && summonTimer <= 0f && summonPrefabs != null && summonPrefabs.Length > 0)
        {
            BeginSummon();
            return;
        }

        switch (action)
        {
            case BossAction.DashIn:     UpdateDashIn();     return;
            case BossAction.Backoff:    UpdateBackoff();    return;
            case BossAction.Summon:     UpdateSummon();     return;
            case BossAction.Reposition: UpdateReposition(); return;
        }

        // Idle / решение
        if (decisionTimer > 0f)
        {
            decisionTimer -= Time.deltaTime;
            FaceTarget();
            StopAgent();
            return;
        }

        DecideNextAction();
    }

    private void TickTimers()
    {
        if (summonTimer > 0f)      summonTimer      -= Time.deltaTime;
        if (postSummonTimer > 0f)  postSummonTimer  -= Time.deltaTime;
        if (dashTimer > 0f)        dashTimer        -= Time.deltaTime;
        if (backoffTimer > 0f)     backoffTimer     -= Time.deltaTime;
    }

    private void DecideNextAction()
    {
        float dist = Vector2.Distance(transform.position, target.position);
        bool yAligned = IsYAligned();

        // Вплотную: spin (area, не требует Y) или backoff.
        if (dist < spinRange)
        {
            if (Random.value < EffectiveChance(spinChance))
            {
                pendingAttackIndex = 2; // Attack3 (spin) — area
                state = State.AttackWindup;
                windupTimer = attackWindupDuration;
                attackTimer = attackCooldown;
            }
            else
            {
                BeginBackoff();
            }
            return;
        }

        // Mid range: mace — направленная, требует Y-выравнивания.
        if (dist >= maceMinRange && dist <= maceMaxRange)
        {
            if (!yAligned)
            {
                BeginReposition(desiredCloseRange);
                return;
            }
            pendingAttackIndex = 1; // Attack2 (mace)
            state = State.AttackWindup;
            windupTimer = attackWindupDuration;
            attackTimer = attackCooldown;
            return;
        }

        // Dash range: рывок (Attack1 направленный — есть смысл только если есть надежда выровняться)
        // или подойти.
        if (dist > dashMinRange && dist <= dashMaxRange)
        {
            if (Random.value < EffectiveChance(dashChance))
            {
                BeginDashIn();
                return;
            }
            BeginReposition(desiredCloseRange);
            return;
        }

        // Слишком далеко — спокойно сокращаем дистанцию.
        BeginReposition(desiredCloseRange);
    }

    /// <summary>
    /// В PostSummon-фазе шансы агрессии умножаются на 0.5 — босс ведёт себя пассивнее.
    /// </summary>
    private float EffectiveChance(float baseChance)
    {
        return InPostSummon ? baseChance * 0.5f : baseChance;
    }

    private float NextDecisionDelay()
    {
        float min = minDecisionInterval;
        float max = maxDecisionInterval;
        if (InPostSummon) { min *= 1.6f; max *= 1.6f; }
        return Random.Range(min, max);
    }

    // === Sub-actions ===

    private void BeginDashIn()
    {
        action = BossAction.DashIn;
        dashTimer = dashMaxDuration;
        ResumeAgent();
        if (agent != null) agent.speed = baseMoveSpeed * dashSpeedMultiplier;
    }

    private void UpdateDashIn()
    {
        if (target == null) { EndDash(); return; }

        agent.SetDestination(target.position);
        FaceTarget();

        float dist = Vector2.Distance(transform.position, target.position);
        if (dist <= dashAttackRange)
        {
            EndDash();
            // После dash проверяем Y: если игрок ушёл по Y — Attack1 (направленная) полетит в пустоту.
            // В этом случае атаку отменяем, переходим в backoff и перерешаем.
            if (!IsYAligned())
            {
                BeginBackoff();
                return;
            }
            pendingAttackIndex = 0; // Attack1
            state = State.AttackWindup;
            windupTimer = attackWindupDuration * 0.5f; // dash-атака бьёт быстрее
            attackTimer = attackCooldown;
            forceBackoffAfterAttack = true; // гарантируем "чуть отступить" после удара
            return;
        }

        dashTimer -= Time.deltaTime;
        if (dashTimer <= 0f)
        {
            // Не догнал — возвращаемся к обычному решению.
            EndDash();
            decisionTimer = NextDecisionDelay();
        }
    }

    private void EndDash()
    {
        if (agent != null) agent.speed = baseMoveSpeed;
        action = BossAction.Idle;
    }

    private void BeginBackoff()
    {
        action = BossAction.Backoff;
        backoffTimer = backoffDuration;
        backoffDir = ((Vector2)(transform.position - target.position)).normalized;
        if (backoffDir.sqrMagnitude < 0.01f) backoffDir = Vector2.right;
        ResumeAgent();

        Vector3 dest = transform.position + (Vector3)(backoffDir * backoffDistance);
        if (NavMesh.SamplePosition(dest, out NavMeshHit hit, backoffDistance, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
    }

    private void UpdateBackoff()
    {
        FaceTarget();
        backoffTimer -= Time.deltaTime;
        if (backoffTimer <= 0f)
        {
            action = BossAction.Idle;
            decisionTimer = NextDecisionDelay();
        }
    }

    private void BeginReposition(float desiredDist)
    {
        action = BossAction.Reposition;
        ResumeAgent();

        Vector2 self = transform.position;
        Vector2 tgt = target.position;
        Vector2 toSelf = (self - tgt);
        if (toSelf.sqrMagnitude < 0.01f) toSelf = Vector2.right;
        Vector2 dir = toSelf.normalized;
        Vector3 dest = (Vector3)(tgt + dir * desiredDist);

        if (NavMesh.SamplePosition(dest, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);

        // Через короткий тик заново решаем (если уже на месте — DecideNextAction перебросит в атаку).
        decisionTimer = NextDecisionDelay();
    }

    private void UpdateReposition()
    {
        FaceTarget();

        decisionTimer -= Time.deltaTime;
        if (decisionTimer <= 0f)
        {
            action = BossAction.Idle;
        }
    }

    // === Summon ===

    private void BeginSummon()
    {
        action = BossAction.Summon;
        StopAgent();
        FaceTarget();

        backoffTimer = summonStateDuration; // переиспользуем таймер
        // Анимации саммона нет — просто пауза, спавн в середине через DoSummon().
        Invoke(nameof(DoSummon), summonStateDuration * 0.5f);
    }

    private void UpdateSummon()
    {
        StopAgent();
        FaceTarget();
        backoffTimer -= Time.deltaTime;
        if (backoffTimer <= 0f)
        {
            action = BossAction.Idle;
            summonTimer = summonInterval;
            postSummonTimer = postSummonDuration;
            decisionTimer = NextDecisionDelay();
        }
    }

    private void DoSummon()
    {
        if (!NetworkServer.active) return;
        if (health.IsDead) return;
        if (summonPrefabs == null || summonPrefabs.Length == 0) return;

        for (int i = 0; i < summonCount; i++)
        {
            var prefab = summonPrefabs[Random.Range(0, summonPrefabs.Length)];
            if (prefab == null) continue;

            Vector2 offset = Random.insideUnitCircle.normalized * summonSpawnRadius;
            Vector3 spawnPos = transform.position + (Vector3)offset;
            if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, summonSpawnRadius, NavMesh.AllAreas))
                spawnPos = hit.position;

            var minion = Instantiate(prefab, spawnPos, Quaternion.identity);
            var minionAI = minion.GetComponent<MobAI>();
            if (minionAI != null)
                minionAI.Init(transform.position, null);
            NetworkServer.Spawn(minion);
        }
    }
}
