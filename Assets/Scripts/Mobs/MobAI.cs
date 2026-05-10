using Mirror;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Base class for all mob AI.
/// Reads stats from MobData (ScriptableObject) at init.
/// Contains FSM, patrol, chase, hitbox logic.
/// Subclasses override PerformAttack() for custom behavior.
/// Server-only: NavMeshAgent and physics are server-controlled.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(MobHealth))]
[RequireComponent(typeof(Animator))]
public abstract class MobAI : NetworkBehaviour
{
    [Header("Mob Data")]
    public MobData mobData;

    [Tooltip("Включить, если спрайт моба по умолчанию смотрит влево (инвертирует flip)")]
    public bool spriteFacesLeft;

    // --- Runtime stats (applied from MobData + scaling) ---
    protected float detectionRange;
    protected float loseRange;
    protected float attackRange;
    protected float attackCooldown;
    protected float patrolRadius;
    protected float patrolWaitMin;
    protected float patrolWaitMax;
    protected float attackWindupDuration;
    protected float hitReactionDuration;
    protected float recoveryDuration;
    protected bool canBeInterrupted;
    protected float[] attackDamages;
    protected float[] attackStaggerDamages;
    protected float[] attackWeights;

    // --- Components ---
    protected NavMeshAgent agent;
    protected Animator animator;
    protected MobHealth health;
    protected SpriteRenderer spriteRenderer;

    // --- FSM ---
    protected enum State { Patrol, Chase, CircleWait, AttackWindup, Attack, HitReaction, Recovery, Stagger, Flee }
    protected State state = State.Patrol;

    public bool IsFleeing => state == State.Flee;

    private Vector2 fleeRoomMin;
    private Vector2 fleeRoomMax;
    private Vector2 fleeTarget;

    // --- Target ---
    protected Transform target;
    protected Vector2 roomCenter;

    // --- Group AI ---
    private MobGroupManager groupManager;

    // --- Timers ---
    protected float attackTimer;
    private float patrolTimer;
    private bool isWaitingAtPatrolPoint;
    protected float windupTimer;
    private float hitReactionTimer;
    private float recoveryTimer;
    private float staggerTimer;

    // --- SyncVar for client animation ---
    [SyncVar] private bool syncIsMoving;
    [SyncVar] private bool syncFlipX;

    // --- Hitboxes (same pattern as HeroAbility) ---
    private WeaponHitbox[] weaponHitboxes;
    protected float pendingDamage;
    protected float pendingStaggerDamage;
    protected int pendingHitboxIndex;

    // === Scaling ===
    private float difficultyMultiplier = 1f;

    // === Init ===

    public void Init(Vector2 center, MobGroupManager group = null, int playerCount = 1, float difficulty = 1f)
    {
        roomCenter = center;
        groupManager = group;

        // Масштабирование от количества игроков:
        //   +25% HP и +15% урона за каждого игрока сверх первого.
        //   Пример: 2 игрока → 1.25× HP, 1.15× урон. 3 игрока → 1.5× HP, 1.3× урон.
        // difficulty — общий множитель (для будущих настроек сложности).
        difficultyMultiplier = difficulty;
        float hpScale = difficulty * (1f + 0.25f * (playerCount - 1));
        float dmgScale = difficulty * (1f + 0.15f * (playerCount - 1));

        ApplyMobData(hpScale, dmgScale);
    }

    private void ApplyMobData(float hpScale, float dmgScale)
    {
        if (mobData == null)
        {
            Debug.LogError($"[MobAI] MobData not assigned on {gameObject.name}!");
            return;
        }

        // Detection & patrol
        detectionRange = mobData.detectionRange;
        loseRange = mobData.loseRange;
        patrolRadius = mobData.patrolRadius;
        patrolWaitMin = mobData.patrolWaitMin;
        patrolWaitMax = mobData.patrolWaitMax;

        // Attack
        attackRange = mobData.attackRange;
        attackCooldown = mobData.attackCooldown;
        attackDamages = new float[mobData.attackDamages.Length];
        for (int i = 0; i < mobData.attackDamages.Length; i++)
            attackDamages[i] = mobData.attackDamages[i] * dmgScale;

        // Stagger damages (если не задано — дефолт 5)
        attackStaggerDamages = new float[mobData.attackDamages.Length];
        for (int i = 0; i < attackStaggerDamages.Length; i++)
        {
            attackStaggerDamages[i] = (mobData.attackStaggerDamages != null && i < mobData.attackStaggerDamages.Length)
                ? mobData.attackStaggerDamages[i]
                : 5f;
        }

        attackWeights = mobData.attackWeights;

        // Windup & Reaction
        attackWindupDuration = mobData.attackWindupDuration;
        hitReactionDuration = mobData.hitReactionDuration;
        recoveryDuration = mobData.recoveryDuration;
        canBeInterrupted = mobData.canBeInterrupted;

        // Movement / Navigation
        if (agent != null)
        {
            agent.speed = mobData.moveSpeed;
            agent.acceleration = mobData.navAcceleration;
            agent.angularSpeed = mobData.navAngularSpeed;
            agent.stoppingDistance = mobData.navStoppingDistance;
            agent.radius = mobData.navRadius;
            agent.autoBraking = true;
            agent.obstacleAvoidanceType = mobData.navAvoidanceQuality;
            // Лёгкий рандом приоритета чтобы агенты не залипали друг на друге одинаково.
            agent.avoidancePriority = Mathf.Clamp(mobData.navPriority + Random.Range(-3, 4), 0, 99);
        }

        // Health (scaled)
        health.SetMaxHealth(mobData.maxHealth * hpScale);

        // Poise (scales with difficulty, not with player count)
        health.SetPoise(mobData.maxPoise * hpScale, mobData.poiseRecoveryRate, mobData.staggerDuration);

        // Knockback resistance
        var hitEffect = GetComponent<HitEffect>();
        if (hitEffect != null)
            hitEffect.knockbackResistance = mobData.knockbackResistance;
    }

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        health = GetComponent<MobHealth>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // NavMeshAgent for 2D
        agent.updateRotation = false;
        agent.updateUpAxis = false;

        weaponHitboxes = GetComponentsInChildren<WeaponHitbox>(true);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!isServer)
            agent.enabled = false;
    }

    private void OnDisable()
    {
        ReleaseSlot();
        if (groupManager != null)
        {
            groupManager.NotifyTargetChanged(this, target, null);
            groupManager.Unregister(this);
        }
    }

    // === Update: FSM dispatch ===

    private void Update()
    {
        // Animation and flip on all clients via SyncVar
        animator.SetBool("IsMoving", syncIsMoving);
        if (spriteRenderer != null)
            spriteRenderer.flipX = spriteFacesLeft ? !syncFlipX : syncFlipX;

        if (!isServer) return;
        if (health.IsDead) return;

        if (attackTimer > 0f) attackTimer -= Time.deltaTime;

        switch (state)
        {
            case State.Patrol:       UpdatePatrol();      break;
            case State.Chase:        UpdateChase();       break;
            case State.CircleWait:   UpdateCircleWait();  break;
            case State.AttackWindup: UpdateAttackWindup(); break;
            case State.Attack:       UpdateAttack();      break;
            case State.HitReaction:  UpdateHitReaction(); break;
            case State.Recovery:     UpdateRecovery();    break;
            case State.Stagger:      UpdateStagger();     break;
            case State.Flee:         UpdateFlee();        break;
        }

        bool moving = agent.velocity.sqrMagnitude > 0.01f;
        if (syncIsMoving != moving) syncIsMoving = moving;

        FlipSprite();
    }

    // === States ===

    private void UpdatePatrol()
    {
        var player = FindTarget();
        if (player != null)
        {
            SetTarget(player);
            state = State.Chase;
            isWaitingAtPatrolPoint = false;
            return;
        }

        if (isWaitingAtPatrolPoint)
        {
            patrolTimer -= Time.deltaTime;
            if (patrolTimer <= 0f)
            {
                isWaitingAtPatrolPoint = false;
                SetPatrolDestination();
            }
        }
        else
        {
            if (!agent.hasPath || agent.remainingDistance < 0.3f)
            {
                isWaitingAtPatrolPoint = true;
                patrolTimer = Random.Range(patrolWaitMin, patrolWaitMax);
                agent.ResetPath();
            }
        }
    }

    private void UpdateChase()
    {
        if (target == null || !IsTargetAlive())
        {
            SetTarget(null);
            ReleaseSlot();
            state = State.Patrol;
            SetPatrolDestination();
            return;
        }

        float dist = Vector2.Distance(transform.position, target.position);

        if (dist > loseRange)
        {
            SetTarget(null);
            ReleaseSlot();
            state = State.Patrol;
            SetPatrolDestination();
            return;
        }

        bool useSlot = mobData != null && mobData.usesAttackSlot;

        // Дальнобойные — слот не запрашивают.
        if (!useSlot)
        {
            if (dist <= attackRange)
            {
                agent.ResetPath();
                state = State.Attack;
                return;
            }
            agent.SetDestination(target.position);
            return;
        }

        // Ближний бой — через слот-систему.
        float engageRange = groupManager != null ? groupManager.circleRadius : attackRange;
        if (dist <= engageRange)
        {
            if (groupManager != null && !groupManager.RequestAttackSlot(this, target))
            {
                state = State.CircleWait;
                return;
            }
        }

        // В радиусе атаки — атакуем (без жёсткой привязки к точке).
        if (dist <= attackRange)
        {
            agent.ResetPath();
            state = State.Attack;
            return;
        }

        // Цель назначения: позиция слота (по горизонтали с игроком). Если слот ещё не выдан —
        // идём прямо на target. NavMeshAgent остановится сам по stoppingDistance.
        Vector2 dest = groupManager != null && (groupManager.IsLeftSlot(this) || groupManager.IsRightSlot(this))
            ? groupManager.GetSlotPosition(this, target, attackRange)
            : (Vector2)target.position;

        agent.SetDestination(dest);
    }

    private void UpdateCircleWait()
    {
        if (target == null || !IsTargetAlive())
        {
            SetTarget(null);
            state = State.Patrol;
            SetPatrolDestination();
            return;
        }

        if (groupManager == null || groupManager.RequestAttackSlot(this, target))
        {
            state = State.Chase;
            return;
        }

        Vector2 circlePos = groupManager.GetCirclePosition(this, target.position);
        agent.SetDestination(new Vector3(circlePos.x, circlePos.y, 0f));

        FaceTarget();
    }

    /// <summary>
    /// Attack state: mob is in range. If cooldown ready, enter windup; otherwise chase if target moves away.
    /// </summary>
    protected virtual void UpdateAttack()
    {
        if (target == null || !IsTargetAlive())
        {
            SetTarget(null);
            ResumeAgent();
            state = State.Patrol;
            return;
        }

        float dist = Vector2.Distance(transform.position, target.position);

        if (dist > attackRange * 1.2f)
        {
            if (attackTimer > 0f)
            {
                ResumeAgent();
                agent.SetDestination(target.position);
                return;
            }
            ResumeAgent();
            state = State.Chase;
            return;
        }

        StopAgent();
        FaceTarget();

        if (attackTimer <= 0f)
        {
            // Enter windup before performing the actual attack
            state = State.AttackWindup;
            windupTimer = attackWindupDuration;
        }
    }

    /// <summary>
    /// Windup state: mob stands still facing target, telegraphing the attack.
    /// After delay, performs the attack and enters recovery.
    /// </summary>
    private void UpdateAttackWindup()
    {
        StopAgent();

        if (target == null || !IsTargetAlive())
        {
            SetTarget(null);
            ResumeAgent();
            state = State.Patrol;
            SetPatrolDestination();
            return;
        }

        FaceTarget();

        windupTimer -= Time.deltaTime;
        if (windupTimer <= 0f)
        {
            PerformAttack();
            attackTimer = attackCooldown;
            EnterRecovery();
        }
    }

    private void UpdateHitReaction()
    {
        StopAgent();

        hitReactionTimer -= Time.deltaTime;
        if (hitReactionTimer <= 0f)
        {
            ResumeAgent();
            if (target != null && IsTargetAlive())
                state = State.Chase;
            else
            {
                SetTarget(null);
                state = State.Patrol;
                SetPatrolDestination();
            }
        }
    }

    private void UpdateRecovery()
    {
        StopAgent();

        recoveryTimer -= Time.deltaTime;
        if (recoveryTimer <= 0f)
        {
            ResumeAgent();
            if (target != null && IsTargetAlive())
                state = State.Chase;
            else
            {
                SetTarget(null);
                state = State.Patrol;
                SetPatrolDestination();
            }
        }
    }

    // === Abstract methods ===

    /// <summary>
    /// Perform attack: choose type, call PrepareHitbox + SetTrigger.
    /// </summary>
    protected abstract void PerformAttack();

    /// <summary>
    /// Sync animation trigger to all clients.
    /// Call this after animator.SetTrigger() in PerformAttack().
    /// </summary>
    [ClientRpc]
    protected void RpcPlayTrigger(string triggerName)
    {
        if (isServer) return;
        animator.SetTrigger(triggerName);
    }

    // === Common logic ===

    /// <summary>
    /// Fully stops the NavMeshAgent (velocity + path cleared).
    /// </summary>
    protected void StopAgent()
    {
        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
    }

    /// <summary>
    /// Resumes the NavMeshAgent after being stopped.
    /// </summary>
    protected void ResumeAgent()
    {
        if (agent.enabled && agent.isOnNavMesh)
            agent.isStopped = false;
    }

    /// <summary>
    /// Called from MobHealth.TakeDamage() — reaction to taking damage.
    /// </summary>
    /// <summary>
    /// Вызывается оружием/снарядом игрока ПОСЛЕ нанесения урона: моб берёт атакующего как цель,
    /// даже если был в Patrol и не видел игрока. Игнорируется если уже мёртв или бежит.
    /// </summary>
    [Server]
    public void NotifyAttacked(Transform attacker)
    {
        if (!isServer) return;
        if (attacker == null) return;
        if (health.IsDead) return;
        if (state == State.Flee || state == State.Stagger) return;

        var hs = attacker.GetComponent<HeroStats>();
        if (hs == null || hs.IsDead || hs.IsDowned) return;

        // Если уже преследует кого-то — не меняем (чтобы не «дёргался» между целями).
        if (target != null && IsTargetAlive()) return;

        SetTarget(attacker);
        if (state == State.Patrol)
            state = State.Chase;
    }

    public void OnHit()
    {
        if (!isServer) return;
        if (health.IsDead) return;

        if (!canBeInterrupted) return;
        if (state == State.HitReaction || state == State.Stagger) return;
        if (state == State.Flee) return; // бегущего моба не остановить

        // Deactivate hitbox if attack was interrupted
        DisableHitbox();

        state = State.HitReaction;
        hitReactionTimer = hitReactionDuration;
        StopAgent();
    }

    /// <summary>
    /// Called from MobHealth when poise reaches 0 — enter stagger (stunned).
    /// </summary>
    public void OnStagger(float duration)
    {
        if (!isServer) return;
        if (health.IsDead) return;

        DisableHitbox();
        state = State.Stagger;
        staggerTimer = duration;
        StopAgent();
    }

    /// <summary>
    /// Called from MobHealth when stagger ends.
    /// </summary>
    public void OnStaggerEnd()
    {
        if (!isServer) return;
        if (health.IsDead) return;
        if (state != State.Stagger) return;

        ResumeAgent();
        if (target != null && IsTargetAlive())
            state = State.Chase;
        else
        {
            SetTarget(null);
            state = State.Patrol;
            SetPatrolDestination();
        }
    }

    private void UpdateStagger()
    {
        StopAgent();
        // Stagger управляется MobHealth через Invoke — здесь просто ждём
    }

    protected void EnterRecovery()
    {
        ReleaseSlot();
        state = State.Recovery;
        recoveryTimer = recoveryDuration;
    }

    private float fleeTimeoutTimer;
    private const float FleeMaxDuration = 8f; // принудительный destroy если моб застрял

    /// <summary>
    /// Запустить бегство в указанную точку (центр соседней комнаты). Моб бежит ×2 скорости,
    /// уничтожается когда добежал ИЛИ когда покинул исходную комнату И прошло достаточно времени.
    /// </summary>
    [Server]
    public void StartFleeing(Vector2 roomMin, Vector2 roomMax, Vector2 destination)
    {
        if (health.IsDead) return;
        if (state == State.Flee) return;

        fleeRoomMin = roomMin;
        fleeRoomMax = roomMax;
        fleeTarget = destination;
        fleeTimeoutTimer = FleeMaxDuration;

        SetTarget(null);
        ReleaseSlot();

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.speed = mobData.moveSpeed * 2f;
            agent.isStopped = false;
            agent.SetDestination(fleeTarget);
        }

        state = State.Flee;
    }

    private void UpdateFlee()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.SetDestination(fleeTarget);

        Vector2 p = transform.position;
        bool outsideOriginalRoom = p.x < fleeRoomMin.x || p.x > fleeRoomMax.x
                                   || p.y < fleeRoomMin.y || p.y > fleeRoomMax.y;

        // Близко к цели (центр соседней комнаты) — задание выполнено, исчезаем.
        if (Vector2.Distance(p, fleeTarget) < 1.5f)
        {
            NetworkServer.Destroy(gameObject);
            return;
        }

        // Тайм-аут на случай застревания (NavMesh не связан, физика заклинила).
        // Если моб уже покинул исходную комнату — точно вне поля зрения, можно уничтожать.
        // Если ещё внутри — ждём, потом форсируем.
        fleeTimeoutTimer -= Time.deltaTime;
        if (fleeTimeoutTimer <= 0f)
        {
            if (outsideOriginalRoom)
                NetworkServer.Destroy(gameObject);
            else
                fleeTimeoutTimer = 1f; // ещё секунду подождать, может выйдет
        }
    }

    private void ReleaseSlot()
    {
        if (groupManager != null)
            groupManager.ReleaseAttackSlot(this);
    }

    protected void FaceTarget()
    {
        if (target != null)
            syncFlipX = target.position.x < transform.position.x;
    }

    protected void SetPatrolDestination()
    {
        Vector2 offset = Random.insideUnitCircle * patrolRadius;
        Vector3 dest = new Vector3(roomCenter.x + offset.x, roomCenter.y + offset.y, 0f);

        if (NavMesh.SamplePosition(dest, out NavMeshHit hit, patrolRadius, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
    }

    /// <summary>
    /// Выбирает цель с учётом распределения по группе (если есть менеджер).
    /// Без менеджера — ближайший игрок.
    /// </summary>
    protected Transform FindTarget()
    {
        if (groupManager != null)
            return groupManager.AssignTarget(this, detectionRange);
        return FindNearestPlayer();
    }

    protected Transform FindNearestPlayer()
    {
        float bestDist = detectionRange;
        Transform best = null;

        foreach (var identity in NetworkServer.spawned.Values)
        {
            if (identity == null) continue;
            var heroStats = identity.GetComponent<HeroStats>();
            if (heroStats == null || heroStats.IsDead || heroStats.IsDowned) continue;

            float d = Vector2.Distance(transform.position, identity.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = identity.transform;
            }
        }

        return best;
    }

    /// <summary>
    /// Устанавливает новую цель и уведомляет менеджер группы о смене.
    /// </summary>
    protected void SetTarget(Transform newTarget)
    {
        if (target == newTarget) return;
        var oldTarget = target;
        target = newTarget;
        if (groupManager != null)
            groupManager.NotifyTargetChanged(this, oldTarget, newTarget);
    }

    protected bool IsTargetAlive()
    {
        var heroStats = target.GetComponent<HeroStats>();
        return heroStats != null && !heroStats.IsDead && !heroStats.IsDowned;
    }

    private void FlipSprite()
    {
        if (agent.velocity.x > 0.1f)  syncFlipX = false;
        else if (agent.velocity.x < -0.1f) syncFlipX = true;
    }

    // === Hitbox helpers ===

    /// <summary>
    /// Returns damage for attack index, using scaled attackDamages array.
    /// </summary>
    protected float GetAttackDamage(int index)
    {
        if (attackDamages == null || index < 0 || index >= attackDamages.Length)
            return 10f;
        return attackDamages[index];
    }

    protected float GetAttackStaggerDamage(int index)
    {
        if (attackStaggerDamages == null || index < 0 || index >= attackStaggerDamages.Length)
            return 5f;
        return attackStaggerDamages[index];
    }

    /// <summary>
    /// Имя триггера аниматора для атаки index. Берётся из MobData.attackTriggers,
    /// fallback на "Attack{index+1}".
    /// </summary>
    protected string GetAttackTrigger(int index)
    {
        if (mobData != null && mobData.attackTriggers != null
            && index >= 0 && index < mobData.attackTriggers.Length
            && !string.IsNullOrEmpty(mobData.attackTriggers[index]))
        {
            return mobData.attackTriggers[index];
        }
        return "Attack" + (index + 1);
    }

    /// <summary>
    /// Picks a random attack index based on attackWeights.
    /// Returns 0 if only one attack.
    /// </summary>
    protected int ChooseWeightedAttack()
    {
        if (attackDamages == null || attackDamages.Length <= 1)
            return 0;

        // If no weights defined, equal probability
        if (attackWeights == null || attackWeights.Length == 0)
            return Random.Range(0, attackDamages.Length);

        float total = 0f;
        for (int i = 0; i < attackWeights.Length; i++)
            total += attackWeights[i];

        float roll = Random.value * total;
        float cumulative = 0f;
        for (int i = 0; i < attackWeights.Length; i++)
        {
            cumulative += attackWeights[i];
            if (roll < cumulative)
                return i;
        }

        return 0;
    }

    protected void PrepareHitbox(int index, float damage, float staggerDmg = 0f)
    {
        pendingHitboxIndex = index;
        pendingDamage = damage;
        pendingStaggerDamage = staggerDmg;
    }

    private WeaponHitbox GetHitbox(int index)
    {
        if (weaponHitboxes == null || index < 0 || index >= weaponHitboxes.Length)
            return null;
        return weaponHitboxes[index];
    }

    public void EnableHitbox()
    {
        var hitbox = GetHitbox(pendingHitboxIndex);
        if (hitbox != null)
            hitbox.Activate(pendingDamage, 0f, pendingStaggerDamage);
    }

    public void DisableHitbox()
    {
        var hitbox = GetHitbox(pendingHitboxIndex);
        if (hitbox != null)
            hitbox.Deactivate();
    }

    // --- Ranged: projectile spawn via Animation Event ---

    private bool projectileReady;

    /// <summary>
    /// Called by PerformAttack() in ranged subclasses to defer projectile spawn to Animation Event.
    /// </summary>
    protected void PrepareProjectile()
    {
        projectileReady = true;
    }

    /// <summary>
    /// Animation Event: spawns projectile at the correct animation frame.
    /// Override ServerSpawnProjectile() in ranged subclasses.
    /// </summary>
    public void SpawnProjectile()
    {
        if (!NetworkServer.active) return;
        if (!projectileReady) return;
        projectileReady = false;

        ServerSpawnProjectile();
    }

    /// <summary>
    /// Override in ranged mobs to spawn the actual projectile.
    /// </summary>
    protected virtual void ServerSpawnProjectile() { }

    private void OnDrawGizmosSelected()
    {
        float det = mobData != null ? mobData.detectionRange : detectionRange;
        float atk = mobData != null ? mobData.attackRange : attackRange;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, det);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, atk);
    }
}
