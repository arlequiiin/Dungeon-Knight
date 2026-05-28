using Mirror;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Босс Dark Rider — активный мили-всадник биома Cursed Wilds.
/// Без блока. Три атаки + два режима ходьбы (IsMoving обычный, IsMoving2 — бег во время Attack3).
///   • Attack1 — стоит вплотную и тычет копьём (направленная, требует Y-выравнивания).
///   • Attack2 — на средней дистанции делает рывок и бьёт (направленная, требует Y-выравнивания).
///   • Attack3 — зацикленная атака в движении: бегает по карте, подбегая к игроку, фиксированное время.
///     Урон наносится только по Animation Event'ам (EnableHitbox/DisableHitbox), как обычные атаки.
/// Выбор атаки — случайно по весам среди атак, подходящих по дистанции.
/// </summary>
public class DarkRiderAI : MobAI
{
    [Header("Дистанции принятия решений")]
    [Tooltip("Игрок ближе — доступна Attack1 (копьё).")]
    public float closeRange = 1.4f;
    [Tooltip("Игрок в этой полосе — доступна Attack2 (рывок).")]
    public float midMinRange = 1.4f;
    [Tooltip("Игрок дальше midMaxRange — доступна Attack3 (бег с атакой).")]
    public float midMaxRange = 3.5f;

    [Header("Attack3 (бег с атакой)")]
    [Tooltip("Сколько секунд босс бегает с зацикленной атакой, затем выходит в решение.")]
    public float runAttackDuration = 3.5f;
    [Tooltip("Множитель скорости во время бега Attack3.")]
    public float runSpeedMultiplier = 1.6f;

    [Header("Pacing")]
    [Tooltip("Минимальная пауза между решениями.")]
    public float minDecisionInterval = 0.4f;
    [Tooltip("Максимальная пауза между решениями.")]
    public float maxDecisionInterval = 1.1f;

    [Header("Веса атак по зонам")]
    [Tooltip("Вес выбора Attack3 (бег), когда игрок далеко.")]
    [Range(0f, 1f)] public float runAttackChance = 0.7f;

    private enum BossAction { Idle, Reposition, RunAttack }
    private BossAction action = BossAction.Idle;

    private float decisionTimer;
    private float runAttackTimer;
    private float baseMoveSpeed;
    private int pendingAttackIndex;

    // Второй режим ходьбы — бег во время Attack3. Синхронизируем на клиентов отдельно.
    [SyncVar] private bool syncIsMoving2;

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (agent != null) baseMoveSpeed = agent.speed;
    }

    // === Animation sync для второго режима ходьбы ===
    // Базовый MobAI.Update() уже пишет animator.SetBool("IsMoving", ...). Дублируем для IsMoving2
    // на всех клиентах. Используем LateUpdate чтобы значение применялось после базового Update.
    private void LateUpdate()
    {
        // На сервере: если бег Attack3 был прерван извне (стагер/hit-reaction/смена состояния
        // базовым классом), action остаётся RunAttack и скорость/флаг не сброшены — чиним.
        if (isServer && syncIsMoving2 && state != State.Attack)
            EndRunAttack();

        animator.SetBool("IsMoving2", syncIsMoving2);
    }

    // === Overrides ===

    protected override void PerformAttack()
    {
        FaceTarget();
        PrepareHitbox(pendingAttackIndex, GetAttackDamage(pendingAttackIndex), GetAttackStaggerDamage(pendingAttackIndex));

        string trigger = GetAttackTrigger(pendingAttackIndex);
        animator.SetTrigger(trigger);
        RpcPlayTrigger(trigger);
    }

    protected override void UpdateAttack()
    {
        if (target == null || !IsTargetAlive())
        {
            EndRunAttack();
            SetTarget(null);
            ResumeAgent();
            state = State.Patrol;
            SetPatrolDestination();
            return;
        }

        switch (action)
        {
            case BossAction.RunAttack:  UpdateRunAttack();  return;
            case BossAction.Reposition: UpdateReposition(); return;
        }

        // Idle: ждём паузу, затем решаем.
        if (decisionTimer > 0f)
        {
            decisionTimer -= Time.deltaTime;
            FaceTarget();
            StopAgent();
            return;
        }

        DecideNextAction();
    }

    private void DecideNextAction()
    {
        float dist = Vector2.Distance(transform.position, target.position);
        bool yAligned = IsYAligned();

        // Вплотную — Attack1 (копьё). Требует Y-выравнивания.
        if (dist < closeRange)
        {
            if (!yAligned) { BeginReposition(closeRange * 0.8f); return; }
            BeginDirectedAttack(0);
            return;
        }

        // Средняя дистанция — Attack2 (рывок-удар: рывок делает сама анимация). Требует Y-выравнивания.
        if (dist >= midMinRange && dist <= midMaxRange)
        {
            if (!yAligned) { BeginReposition(midMinRange); return; }
            BeginDirectedAttack(1);
            return;
        }

        // Далеко — Attack3 (бег с атакой) по шансу, иначе спокойно сближаемся.
        if (Random.value < runAttackChance)
            BeginRunAttack();
        else
            BeginReposition(midMaxRange * 0.7f);
    }

    private void BeginDirectedAttack(int index)
    {
        pendingAttackIndex = index;
        StopAgent();
        state = State.AttackWindup;
        windupTimer = attackWindupDuration;
        attackTimer = attackCooldown;
    }

    // === Attack3: зацикленный бег с атакой ===

    private void BeginRunAttack()
    {
        action = BossAction.RunAttack;
        runAttackTimer = runAttackDuration;
        pendingAttackIndex = 2;

        ResumeAgent();
        if (agent != null) agent.speed = baseMoveSpeed * runSpeedMultiplier;
        syncIsMoving2 = true;

        // Готовим хитбокс под Attack3, урон наносится по Animation Event'ам зацикленной анимации.
        PrepareHitbox(2, GetAttackDamage(2), GetAttackStaggerDamage(2));

        string trigger = GetAttackTrigger(2);
        animator.SetTrigger(trigger);
        RpcPlayTrigger(trigger);
    }

    private void UpdateRunAttack()
    {
        runAttackTimer -= Time.deltaTime;

        // Бежим к игроку, обновляя цель.
        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.SetDestination(target.position);
        FaceTarget();

        if (runAttackTimer <= 0f)
        {
            EndRunAttack();
            EnterRecovery();
            decisionTimer = NextDecisionDelay();
        }
    }

    private void EndRunAttack()
    {
        if (action != BossAction.RunAttack && !syncIsMoving2) return;
        syncIsMoving2 = false;
        DisableHitbox();
        if (agent != null) agent.speed = baseMoveSpeed;
        action = BossAction.Idle;
        // Сбрасываем триггер бега на клиентах, чтобы аниматор вышел из зацикленного стейта.
        animator.ResetTrigger(GetAttackTrigger(2));
    }

    // === Reposition (сближение / Y-выравнивание перед направленной атакой) ===

    private void BeginReposition(float desiredDist)
    {
        action = BossAction.Reposition;
        ResumeAgent();
        if (agent != null) agent.speed = baseMoveSpeed;

        Vector2 self = transform.position;
        Vector2 tgt = target.position;
        float sign = self.x >= tgt.x ? 1f : -1f;
        // Встаём на одну горизонталь с игроком, держа дистанцию desiredDist по X.
        Vector3 dest = new Vector3(tgt.x + sign * desiredDist, tgt.y, 0f);

        if (NavMesh.SamplePosition(dest, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);

        decisionTimer = NextDecisionDelay();
    }

    private void UpdateReposition()
    {
        FaceTarget();
        decisionTimer -= Time.deltaTime;
        if (decisionTimer <= 0f)
            action = BossAction.Idle;
    }

    private float NextDecisionDelay()
    {
        return Random.Range(minDecisionInterval, maxDecisionInterval);
    }
}
