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
    protected float[] attackWeights;

    // --- Components ---
    protected NavMeshAgent agent;
    protected Animator animator;
    protected MobHealth health;
    protected SpriteRenderer spriteRenderer;

    // --- FSM ---
    protected enum State { Patrol, Chase, CircleWait, AttackWindup, Attack, HitReaction, Recovery }
    protected State state = State.Patrol;

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

    // --- SyncVar for client animation ---
    [SyncVar] private bool syncIsMoving;
    [SyncVar] private bool syncFlipX;

    // --- Hitboxes (same pattern as HeroAbility) ---
    private WeaponHitbox[] weaponHitboxes;
    protected float pendingDamage;
    protected int pendingHitboxIndex;

    // === Scaling ===
    private float difficultyMultiplier = 1f;

    // === Init ===

    public void Init(Vector2 center, MobGroupManager group = null, int playerCount = 1, float difficulty = 1f)
    {
        roomCenter = center;
        groupManager = group;

        // Scale multiplier: +30% HP per extra player, difficulty scales everything
        difficultyMultiplier = difficulty;
        float hpScale = difficulty * (1f + 0.3f * (playerCount - 1));
        float dmgScale = difficulty;

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
        attackWeights = mobData.attackWeights;

        // Windup & Reaction
        attackWindupDuration = mobData.attackWindupDuration;
        hitReactionDuration = mobData.hitReactionDuration;
        recoveryDuration = mobData.recoveryDuration;
        canBeInterrupted = mobData.canBeInterrupted;

        // Movement
        if (agent != null)
            agent.speed = mobData.moveSpeed;

        // Health (scaled)
        health.SetMaxHealth(mobData.maxHealth * hpScale);

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
            groupManager.Unregister(this);
    }

    // === Update: FSM dispatch ===

    private void Update()
    {
        // Animation and flip on all clients via SyncVar
        animator.SetBool("IsMoving", syncIsMoving);
        if (spriteRenderer != null)
            spriteRenderer.flipX = syncFlipX;

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
        }

        bool moving = agent.velocity.sqrMagnitude > 0.01f;
        if (syncIsMoving != moving) syncIsMoving = moving;

        FlipSprite();
    }

    // === States ===

    private void UpdatePatrol()
    {
        var player = FindNearestPlayer();
        if (player != null)
        {
            target = player;
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
            target = null;
            ReleaseSlot();
            state = State.Patrol;
            SetPatrolDestination();
            return;
        }

        float dist = Vector2.Distance(transform.position, target.position);

        if (dist > loseRange)
        {
            target = null;
            ReleaseSlot();
            state = State.Patrol;
            SetPatrolDestination();
            return;
        }

        float engageRange = groupManager != null ? groupManager.circleRadius : attackRange;
        if (dist <= engageRange)
        {
            if (groupManager != null && !groupManager.RequestAttackSlot(this, target))
            {
                state = State.CircleWait;
                return;
            }
        }

        if (dist <= attackRange)
        {
            agent.ResetPath();
            state = State.Attack;
            return;
        }

        agent.SetDestination(target.position);
    }

    private void UpdateCircleWait()
    {
        if (target == null || !IsTargetAlive())
        {
            target = null;
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
            target = null;
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
            target = null;
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
                target = null;
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
                target = null;
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
    public void OnHit()
    {
        if (!isServer) return;
        if (health.IsDead) return;

        if (!canBeInterrupted) return;
        if (state == State.HitReaction) return;

        // Deactivate hitbox if attack was interrupted
        DisableHitbox();

        state = State.HitReaction;
        hitReactionTimer = hitReactionDuration;
        StopAgent();
    }

    protected void EnterRecovery()
    {
        ReleaseSlot();
        state = State.Recovery;
        recoveryTimer = recoveryDuration;
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

    protected Transform FindNearestPlayer()
    {
        float bestDist = detectionRange;
        Transform best = null;

        foreach (var identity in NetworkServer.spawned.Values)
        {
            if (identity == null) continue;
            var heroStats = identity.GetComponent<HeroStats>();
            if (heroStats == null || heroStats.IsDead) continue;

            float d = Vector2.Distance(transform.position, identity.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = identity.transform;
            }
        }

        return best;
    }

    protected bool IsTargetAlive()
    {
        var heroStats = target.GetComponent<HeroStats>();
        return heroStats != null && !heroStats.IsDead;
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

    protected void PrepareHitbox(int index, float damage)
    {
        pendingHitboxIndex = index;
        pendingDamage = damage;
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
            hitbox.Activate(pendingDamage);
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
