using Mirror;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Абстрактный базовый класс для AI всех мобов.
/// Содержит общую логику FSM, патрулирования, преследования, хитбоксов.
/// Наследники переопределяют атаку и параметры.
/// Работает только на сервере — NavMeshAgent и физика управляются сервером.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(MobHealth))]
[RequireComponent(typeof(Animator))]
public abstract class MobAI : NetworkBehaviour
{
    [Header("Обнаружение")]
    public float detectionRange = 5f;
    public float loseRange = 8f;

    [Header("Атака")]
    public float attackRange = 0.8f;
    public float attackCooldown = 1.2f;

    [Header("Патруль")]
    public float patrolRadius = 2.5f;
    public float patrolWaitMin = 1f;
    public float patrolWaitMax = 3f;

    // --- Компоненты ---
    protected NavMeshAgent agent;
    protected Animator animator;
    protected MobHealth health;
    protected SpriteRenderer spriteRenderer;

    // --- FSM ---
    protected enum State { Patrol, Chase, CircleWait, Attack, HitReaction, Recovery }
    protected State state = State.Patrol;

    // --- Таргет ---
    protected Transform target;
    protected Vector2 roomCenter;

    // --- Group AI ---
    private MobGroupManager groupManager;

    // --- Таймеры ---
    protected float attackTimer;
    private float patrolTimer;
    private bool isWaitingAtPatrolPoint;
    private float hitReactionTimer;
    private float recoveryTimer;

    // --- SyncVar для анимации на клиентах ---
    [SyncVar] private bool syncIsMoving;

    // --- Хитбоксы (аналогично HeroAbility) ---
    private WeaponHitbox[] weaponHitboxes;
    protected float pendingDamage;
    protected int pendingHitboxIndex;

    // === Виртуальные параметры для наследников ===

    /// <summary>Длительность flinch при получении урона.</summary>
    protected virtual float HitReactionDuration => 0.3f;

    /// <summary>Окно уязвимости после атаки.</summary>
    protected virtual float RecoveryDuration => 0.4f;

    /// <summary>Может ли моб быть прерван ударом (false = суперармор во время атаки).</summary>
    protected virtual bool CanBeInterrupted => true;

    // === Инициализация ===

    public void Init(Vector2 center, MobGroupManager group = null)
    {
        roomCenter = center;
        groupManager = group;
    }

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        health = GetComponent<MobHealth>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // NavMeshAgent для 2D
        agent.updateRotation = false;
        agent.updateUpAxis = false;

        attackTimer = attackCooldown;
        weaponHitboxes = GetComponentsInChildren<WeaponHitbox>(true);
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
        // Анимации применяются на всех клиентах через SyncVar
        animator.SetBool("IsMoving", syncIsMoving);

        if (!isServer) return;
        if (health.IsDead) return;

        if (attackTimer > 0f) attackTimer -= Time.deltaTime;

        switch (state)
        {
            case State.Patrol:      UpdatePatrol();      break;
            case State.Chase:       UpdateChase();       break;
            case State.CircleWait:  UpdateCircleWait();  break;
            case State.Attack:      UpdateAttack();      break;
            case State.HitReaction: UpdateHitReaction(); break;
            case State.Recovery:    UpdateRecovery();    break;
        }

        // Обновляем SyncVar для анимации
        bool moving = agent.velocity.sqrMagnitude > 0.01f;
        if (syncIsMoving != moving) syncIsMoving = moving;

        FlipSprite();
    }

    // === Состояния ===

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

        // Запрашиваем слот заранее — на подходе, а не вплотную
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

        // Пробуем получить слот
        if (groupManager == null || groupManager.RequestAttackSlot(this, target))
        {
            state = State.Chase;
            return;
        }

        // Кружим вокруг цели
        Vector2 circlePos = groupManager.GetCirclePosition(this, target.position);
        agent.SetDestination(new Vector3(circlePos.x, circlePos.y, 0f));

        // Поворачиваемся к игроку
        FaceTarget();
    }

    /// <summary>
    /// Логика атаки. Наследники могут переопределить для кастомного поведения.
    /// По умолчанию: стоит в зоне атаки, бьёт по кулдауну, переходит в Recovery.
    /// </summary>
    protected virtual void UpdateAttack()
    {
        if (target == null || !IsTargetAlive())
        {
            target = null;
            state = State.Patrol;
            return;
        }

        float dist = Vector2.Distance(transform.position, target.position);

        if (dist > attackRange * 1.2f)
        {
            // Кулдаун ещё не готов — подходим к цели
            if (attackTimer > 0f)
            {
                agent.SetDestination(target.position);
                return;
            }
            // Кулдаун готов, но цель далеко — переход в Chase
            state = State.Chase;
            return;
        }

        // В зоне атаки — стоим на месте
        agent.ResetPath();
        FaceTarget();

        if (attackTimer <= 0f)
        {
            PerformAttack();
            attackTimer = attackCooldown;
            // Переход в Recovery после атаки
            EnterRecovery();
        }
    }

    private void UpdateHitReaction()
    {
        agent.ResetPath();

        hitReactionTimer -= Time.deltaTime;
        if (hitReactionTimer <= 0f)
        {
            // После flinch — возврат в Chase (или Patrol если нет цели)
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
        agent.ResetPath();

        recoveryTimer -= Time.deltaTime;
        if (recoveryTimer <= 0f)
        {
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

    // === Абстрактные методы для наследников ===

    /// <summary>
    /// Выполняет атаку: выбирает тип, вызывает PrepareHitbox + SetTrigger.
    /// </summary>
    protected abstract void PerformAttack();

    // === Общая логика ===

    /// <summary>
    /// Вызывается из MobHealth.TakeDamage() — реакция на получение урона.
    /// </summary>
    public void OnHit()
    {
        if (!isServer) return;
        if (health.IsDead) return;

        // Прерывание зависит от CanBeInterrupted и текущего состояния
        if (!CanBeInterrupted) return;
        if (state == State.HitReaction) return; // уже в flinch

        state = State.HitReaction;
        hitReactionTimer = HitReactionDuration;
        agent.ResetPath();
    }

    /// <summary>
    /// Переводит моба в состояние Recovery (окно уязвимости после атаки).
    /// </summary>
    protected void EnterRecovery()
    {
        ReleaseSlot();
        state = State.Recovery;
        recoveryTimer = RecoveryDuration;
    }

    private void ReleaseSlot()
    {
        if (groupManager != null)
            groupManager.ReleaseAttackSlot(this);
    }

    /// <summary>
    /// Поворачивает спрайт к цели перед атакой.
    /// </summary>
    protected void FaceTarget()
    {
        if (target != null && spriteRenderer != null)
            spriteRenderer.flipX = target.position.x < transform.position.x;
    }

    private void SetPatrolDestination()
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

    private bool IsTargetAlive()
    {
        var heroStats = target.GetComponent<HeroStats>();
        return heroStats != null && !heroStats.IsDead;
    }

    private void FlipSprite()
    {
        if (spriteRenderer == null) return;

        if (agent.velocity.x > 0.1f)  spriteRenderer.flipX = false;
        else if (agent.velocity.x < -0.1f) spriteRenderer.flipX = true;
    }

    // === Animation Event методы ===

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

    /// <summary>
    /// Animation Event: активирует хитбокс на кадре удара.
    /// </summary>
    public void EnableHitbox()
    {
        var hitbox = GetHitbox(pendingHitboxIndex);
        if (hitbox != null)
            hitbox.Activate(pendingDamage);
    }

    /// <summary>
    /// Animation Event: деактивирует хитбокс после удара.
    /// </summary>
    public void DisableHitbox()
    {
        var hitbox = GetHitbox(pendingHitboxIndex);
        if (hitbox != null)
            hitbox.Deactivate();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
