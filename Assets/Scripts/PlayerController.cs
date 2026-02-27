using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class PlayerController : NetworkBehaviour
{
    [Header("Данные героя")]
    public HeroData heroData;

    private PlayerInputActions input;
    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private HeroStats stats;

    // Берётся лениво — KnightAbility добавляется после Awake через AddComponent
    private HeroAbility Ability => GetComponent<HeroAbility>();

    private Vector2 moveInput;
    private bool canDodge = true;
    private bool canAttack = true;
    private bool isAttacking;
    private bool isDodging;

    // Последнее направление движения (для dodge когда стоим)
    private Vector2 lastMoveDir = Vector2.right;

    [SyncVar]
    private Vector2 syncMoveInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        stats = GetComponent<HeroStats>();
    }

    // Вызывается после спавна — применяет данные выбранного героя
    public void InitHero(HeroData data)
    {
        heroData = data;

        if (data.animatorController != null)
            animator.runtimeAnimatorController = data.animatorController;

        stats?.Init(data);

        // Создаём коллайдеры оружия из префабов героя
        if (data.weaponHitboxPrefabs != null)
        {
            foreach (var prefab in data.weaponHitboxPrefabs)
            {
                if (prefab == null) continue;
                var hitboxObj = Instantiate(prefab, transform);
                hitboxObj.transform.localPosition = Vector3.zero;
            }
        }

        // Обновляем массив хитбоксов в HeroAbility после создания дочерних объектов
        var ability = GetComponent<HeroAbility>();
        if (ability != null)
            ability.RefreshHitboxes();
    }

    public override void OnStartLocalPlayer()
    {
        input = new PlayerInputActions();
        input.Player.Enable();

        input.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        input.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        input.Player.Attack1.performed += _ => TryAttack1();
        input.Player.Attack2.performed += _ => TryAttack2();
        input.Player.Ability1.performed += _ => TryAbility1();
        input.Player.Ability2.performed += _ => TryAbility2();
        input.Player.Dodge.performed += _ => TryDodge();

        var cam = Camera.main;
        if (cam != null)
        {
            var follow = cam.GetComponent<CameraFollow>();
            if (follow == null)
                follow = cam.gameObject.AddComponent<CameraFollow>();
            follow.SetTarget(transform);
        }
    }

    private void OnDisable()
    {
        if (isLocalPlayer && input != null)
            input.Player.Disable();
    }

    private void FixedUpdate()
    {
        if (!isLocalPlayer) return;

        if (stats != null && stats.IsDead) return;

        // Во время рывка — не управляем скоростью вручную
        if (isDodging) return;

        float speed = heroData != null ? heroData.moveSpeed : 5f;

        // Замедление при атаке
        if (isAttacking && heroData != null)
            speed *= heroData.attackSlowMultiplier;

        rb.linearVelocity = moveInput * speed;

        animator.SetBool("IsMoving", moveInput != Vector2.zero);

        // Запоминаем последнее направление движения
        if (moveInput != Vector2.zero)
            lastMoveDir = moveInput.normalized;

        // Flip спрайта по горизонтальному направлению движения (не во время атаки)
        if (!isAttacking && spriteRenderer != null && moveInput.x != 0f)
            spriteRenderer.flipX = moveInput.x < 0f;

        if (moveInput != syncMoveInput)
            CmdSetMoveInput(moveInput);
    }

    // --- Поворот к цели при атаке ---

    private void FaceAttackDirection()
    {
        float searchRadius = heroData != null ? heroData.targetSearchRadius : 5f;

        // Ищем ближайшего врага (моба) в радиусе — без LayerMask, по компоненту MobHealth
        var hits = Physics2D.OverlapCircleAll(transform.position, searchRadius);
        Transform nearest = null;
        float minDist = float.MaxValue;

        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            var mob = hit.GetComponent<MobHealth>();
            if (mob == null || mob.IsDead) continue;

            float dist = Vector2.Distance(transform.position, hit.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = hit.transform;
            }
        }

        float dirX;
        if (nearest != null)
        {
            // Поворот к ближайшему врагу
            dirX = nearest.position.x - transform.position.x;
        }
        else
        {
            // Нет врагов — поворот к позиции мыши
            var cam = Camera.main;
            if (cam != null)
            {
                Vector2 mouseScreen = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
                Vector3 mouseWorld = cam.ScreenToWorldPoint(mouseScreen);
                dirX = mouseWorld.x - transform.position.x;
            }
            else
            {
                dirX = lastMoveDir.x;
            }
        }

        if (spriteRenderer != null && dirX != 0f)
            spriteRenderer.flipX = dirX < 0f;
    }

    private void StartAttackSlow()
    {
        isAttacking = true;
        float duration = heroData != null ? heroData.attackSlowDuration : 0.4f;
        Invoke(nameof(EndAttackSlow), duration);
    }

    private void EndAttackSlow()
    {
        isAttacking = false;
    }

    // --- Атаки ---

    private void TryAttack1()
    {
        var ab = Ability;
        if (!isLocalPlayer || !canAttack || ab == null) return;
        canAttack = false;

        FaceAttackDirection();
        StartAttackSlow();

        ab.Attack1();
        float cooldown = heroData != null ? heroData.attackCooldown : 0.5f;
        Invoke(nameof(ResetAttack), cooldown);
    }

    private void TryAttack2()
    {
        var ab = Ability;
        if (!isLocalPlayer || !canAttack || ab == null) return;
        if (heroData != null && heroData.attackCount < 2) return;
        canAttack = false;

        FaceAttackDirection();
        StartAttackSlow();

        ab.Attack2();
        float cooldown = heroData != null ? heroData.attackCooldown : 0.5f;
        Invoke(nameof(ResetAttack), cooldown);
    }

    // --- Способности ---

    private void TryAbility1()
    {
        var ab = Ability;
        if (!isLocalPlayer || ab == null) return;
        if (!ab.CanUseAbility1) return;

        FaceAttackDirection();
        StartAttackSlow();

        ab.UseAbility1();
    }

    private void TryAbility2()
    {
        var ab = Ability;
        if (!isLocalPlayer || ab == null) return;
        if (!ab.CanUseAbility2) return;

        FaceAttackDirection();
        StartAttackSlow();

        ab.UseAbility2();
    }

    // --- Уклонение ---

    private void TryDodge()
    {
        if (!isLocalPlayer || !canDodge || isDodging) return;
        canDodge = false;

        Vector2 dodgeDir = moveInput != Vector2.zero ? moveInput.normalized : lastMoveDir;
        float force = heroData != null ? heroData.dodgeForce : 8f;
        float duration = heroData != null ? heroData.dodgeDuration : 0.25f;
        float cooldown = heroData != null ? heroData.dodgeCooldown : 1f;

        StartCoroutine(DodgeRoutine(dodgeDir, force, duration, cooldown));
    }

    private IEnumerator DodgeRoutine(Vector2 direction, float force, float duration, float cooldown)
    {
        isDodging = true;

        // Сообщаем серверу о неуязвимости
        CmdSetDodging(true);

        rb.linearVelocity = Vector2.zero;
        rb.AddForce(direction * force, ForceMode2D.Impulse);

        yield return new WaitForSeconds(duration);

        isDodging = false;
        CmdSetDodging(false);

        // Кулдаун начинается после окончания рывка
        yield return new WaitForSeconds(cooldown - duration);
        canDodge = true;
    }

    // --- Сеть ---

    [Command]
    private void CmdSetMoveInput(Vector2 input)
    {
        syncMoveInput = input;
    }

    [Command]
    private void CmdSetDodging(bool dodging)
    {
        if (stats != null)
            stats.IsDodging = dodging;
    }

    // --- Сброс кулдаунов ---

    private void ResetAttack() => canAttack = true;
}
