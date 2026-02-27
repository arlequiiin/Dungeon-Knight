using Mirror;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// ИИ скелетона: патрулирует комнату, преследует игрока при обнаружении, атакует в ближнем бою.
/// Работает только на сервере — NavMeshAgent и физика управляются сервером.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(MobHealth))]
[RequireComponent(typeof(Animator))]
public class SkeletonAI : NetworkBehaviour
{
    [Header("Обнаружение")]
    public float detectionRange = 5f;
    public float loseRange = 8f;

    [Header("Атака")]
    public float attackRange = 0.8f;
    public float attackDamage = 10f;
    public float attackCooldown = 1.2f;

    [Header("Патруль")]
    public float patrolRadius = 2.5f;
    public float patrolWaitMin = 1f;
    public float patrolWaitMax = 3f;

    private NavMeshAgent agent;
    private Animator animator;
    private MobHealth health;
    private SpriteRenderer spriteRenderer;

    private Transform target;
    private Vector2 roomCenter;
    private float attackTimer;
    private float patrolTimer;

    private enum State { Patrol, Chase, Attack }
    private State state = State.Patrol;

    // SyncVar для анимации на клиентах
    [SyncVar] private bool isMoving;
    [SyncVar] private bool isAttacking;

    // --- Хитбоксы (аналогично HeroAbility) ---
    private WeaponHitbox[] weaponHitboxes;
    private float pendingDamage;
    private int pendingHitboxIndex;

    public void Init(Vector2 center)
    {
        roomCenter = center;
    }

    private void Awake()
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

    private void Update()
    {
        // Анимации применяются на всех клиентах через SyncVar
        animator.SetBool("IsMoving", isMoving);

        if (!isServer) return;
        if (health.IsDead) return;

        if (attackTimer > 0f) attackTimer -= Time.deltaTime;

        switch (state)
        {
            case State.Patrol: UpdatePatrol(); break;
            case State.Chase:  UpdateChase();  break;
            case State.Attack: UpdateAttack(); break;
        }

        // Обновляем SyncVar для анимации
        bool moving = agent.velocity.sqrMagnitude > 0.01f;
        if (isMoving != moving) isMoving = moving;

        // Флип спрайта по направлению движения (через SyncVar или отдельный хук)
        FlipSprite();
    }

    // --- Состояния ---

    private void UpdatePatrol()
    {
        // Ищем ближайшего игрока
        var player = FindNearestPlayer();
        if (player != null)
        {
            target = player;
            state = State.Chase;
            return;
        }

        // Ждём и переходим к новой точке патруля
        patrolTimer -= Time.deltaTime;
        if (patrolTimer <= 0f || !agent.hasPath || agent.remainingDistance < 0.3f)
        {
            SetPatrolDestination();
        }
    }

    private void UpdateChase()
    {
        if (target == null || !IsTargetAlive())
        {
            target = null;
            state = State.Patrol;
            SetPatrolDestination();
            return;
        }

        float dist = Vector2.Distance(transform.position, target.position);

        if (dist > loseRange)
        {
            target = null;
            state = State.Patrol;
            SetPatrolDestination();
            return;
        }

        if (dist <= attackRange)
        {
            agent.ResetPath();
            state = State.Attack;
            return;
        }

        agent.SetDestination(target.position);
    }

    private void UpdateAttack()
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
            state = State.Chase;
            return;
        }

        agent.ResetPath();

        if (attackTimer <= 0f)
        {
            PerformAttack();
            attackTimer = attackCooldown;
        }
    }

    // --- Логика ---

    private void PerformAttack()
    {
        // Поворачиваемся к цели перед атакой
        if (target != null && spriteRenderer != null)
            spriteRenderer.flipX = target.position.x < transform.position.x;

        PrepareHitbox(0, attackDamage);
        animator.SetTrigger("Attack1");
    }

    private void SetPatrolDestination()
    {
        patrolTimer = Random.Range(patrolWaitMin, patrolWaitMax);

        // Случайная точка внутри патрульного радиуса
        Vector2 offset = Random.insideUnitCircle * patrolRadius;
        Vector3 dest = new Vector3(roomCenter.x + offset.x, roomCenter.y + offset.y, 0f);

        if (NavMesh.SamplePosition(dest, out NavMeshHit hit, patrolRadius, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
    }

    private Transform FindNearestPlayer()
    {
        // Ищем среди NetworkIdentity игроков ближайшего в радиусе обнаружения
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

    // --- Animation Event методы (вызываются из анимации Attack1) ---

    private void PrepareHitbox(int index, float damage)
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
