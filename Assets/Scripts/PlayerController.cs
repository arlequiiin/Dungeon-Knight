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

    [SyncVar(hook = nameof(OnHeroTypeChanged))]
    private HeroType syncHeroType = HeroType.None;

    private PlayerInputActions input;
    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private HeroStats stats;

    private HeroAbility Ability => GetComponent<HeroAbility>();

    private Vector2 moveInput;
    private bool canDodge = true;
    private bool canAttack = true;
    private bool isAttacking;
    private bool isDodging;

    // true когда игрок в подземелье (не в лобби) — разрешает боевые действия
    private bool inGame;
    private void RefreshInGame() =>
        inGame = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "LobbyScene";

    // Последнее направление движения (для dodge когда стоим)
    private Vector2 lastMoveDir = Vector2.right;

    [Header("UI")]
    [SerializeField] private GameObject hudPrefab;
    [SerializeField] private GameObject deathScreenPrefab;
    [SerializeField] private GameObject pauseMenuPrefab;
    private PlayerHUD hud;

    public event System.Action onInteract;

    [SyncVar]
    private Vector2 syncMoveInput;

    [SyncVar(hook = nameof(OnFlipChanged))]
    private bool syncFlipX;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        stats = GetComponent<HeroStats>();
    }

    private void OnHeroTypeChanged(HeroType oldType, HeroType newType)
    {
        if (newType == HeroType.None) return;

        // Клиент получил обновление типа героя — применяем визуал и компоненты
        var netManager = (DungeonKnightNetworkManager)Mirror.NetworkManager.singleton;
        if (netManager == null) return;

        var data = netManager.GetHeroData(newType);
        if (data == null) return;

        heroData = data;

        if (data.animatorController != null && animator != null)
            animator.runtimeAnimatorController = data.animatorController;

        // На клиенте (не на сервере) — создаём HeroAbility и хитбоксы,
        // т.к. InitHeroOnPlayer вызывается только на сервере
        if (!isServer)
        {
            // Удаляем старый ability если был
            var oldAbility = GetComponent<HeroAbility>();
            if (oldAbility != null) Destroy(oldAbility);

            // Удаляем старые хитбоксы
            foreach (var hitbox in GetComponentsInChildren<WeaponHitbox>())
                Destroy(hitbox.gameObject);

            // Добавляем ability
            switch (data.heroType)
            {
                case HeroType.Knight:    gameObject.AddComponent<KnightAbility>();    break;
                case HeroType.Soldier:   gameObject.AddComponent<SoldierAbility>();   break;
                case HeroType.Templar:   gameObject.AddComponent<TemplarAbility>();   break;
                case HeroType.Swordsman: gameObject.AddComponent<SwordsmanAbility>(); break;
                case HeroType.Archer:    gameObject.AddComponent<ArcherAbility>();    break;
                case HeroType.Wizard:    gameObject.AddComponent<WizardAbility>();    break;
                case HeroType.Priest:    gameObject.AddComponent<PriestAbility>();    break;
                default:                 gameObject.AddComponent<KnightAbility>();    break;
            }

            // Создаём хитбоксы
            if (data.weaponHitboxPrefabs != null)
            {
                foreach (var prefab in data.weaponHitboxPrefabs)
                {
                    if (prefab == null) continue;
                    var hitboxObj = Instantiate(prefab, transform);
                    hitboxObj.transform.localPosition = Vector3.zero;
                }
            }

            // Обновляем ссылки и кулдауны
            var ability = GetComponent<HeroAbility>();
            if (ability != null)
            {
                ability.ApplyHeroData(data);
                ability.RefreshHitboxes();
            }
        }
    }

    // Вызывается после спавна — применяет данные выбранного героя
    public void InitHero(HeroData data)
    {
        heroData = data;
        syncHeroType = data.heroType;

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
        {
            ability.ApplyHeroData(data);
            ability.RefreshHitboxes();
        }
    }

    private void EnsureInputInitialized()
    {
        if (input == null)
        {
            input = new PlayerInputActions();

            input.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
            input.Player.Move.canceled += ctx => moveInput = Vector2.zero;

            input.Player.Attack1.performed += _ => TryAttack1();
            input.Player.Attack2.performed += _ => TryAttack2();
            input.Player.Ability1.performed += _ => TryAbility1();
            input.Player.Ability2.performed += _ => TryAbility2();
            input.Player.Dodge.performed += _ => TryDodge();
            input.Player.Interaction.performed += _ => onInteract?.Invoke();
        }

        input.Player.Enable();
    }

    public override void OnStartLocalPlayer()
    {
        EnsureInputInitialized();

        RefreshInGame();
        StartCoroutine(LateBindCamera());

        // Создаём HUD для локального игрока
        if (hudPrefab != null)
        {
            var hudObj = Instantiate(hudPrefab);
            hud = hudObj.GetComponent<PlayerHUD>();
            hud?.Init(stats, Ability);
        }

        // Экран смерти — только в данже
        if (deathScreenPrefab != null && inGame)
        {
            var deathObj = Instantiate(deathScreenPrefab);
            var deathScreen = deathObj.GetComponent<DeathScreenUI>();
            deathScreen?.Init(stats);
        }

        // Меню паузы — в данже и в лобби
        if (pauseMenuPrefab != null)
        {
            Instantiate(pauseMenuPrefab);
        }
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

        if (isLocalPlayer)
        {
            EnsureInputInitialized();
            BindCamera();
            RefreshInGame();
        }
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (isLocalPlayer)
        {
            EnsureInputInitialized();
            BindCamera();
            RefreshInGame();
        }
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;

        if (isLocalPlayer && input != null)
            input.Player.Disable();
    }

    private void BindCamera()
    {
        // При наличии нескольких камер (например лобби + DontDestroyOnLoad)
        // оставляем только одну и привязываем к ней CameraFollow
        var allCams = Camera.allCameras;
        Camera chosen = Camera.main ?? (allCams.Length > 0 ? allCams[0] : null);
        if (chosen == null) return;

        // Отключаем все остальные камеры
        foreach (var c in allCams)
        {
            if (c != chosen)
                c.gameObject.SetActive(false);
        }

        var follow = chosen.GetComponent<CameraFollow>();
        if (follow == null)
            follow = chosen.gameObject.AddComponent<CameraFollow>();
        follow.SetTarget(transform);
    }

    private IEnumerator LateBindCamera()
    {
        float timeout = 1f;
        while (Camera.main == null && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
        BindCamera();
    }

    private void FixedUpdate()
    {
        // Клиент: читает input и отправляет на сервер
        if (isLocalPlayer)
        {
            if (moveInput != syncMoveInput)
                CmdSetMoveInput(moveInput);

            // Flip — отправляем на сервер
            if (!isAttacking && moveInput.x != 0f)
            {
                bool flip = moveInput.x < 0f;
                if (flip != syncFlipX)
                    CmdSetFlip(flip);
            }

            // Запоминаем последнее направление движения (локально для dodge)
            if (moveInput != Vector2.zero)
                lastMoveDir = moveInput.normalized;
        }

        // Сервер: двигает персонажа по syncMoveInput
        if (isServer)
        {
            bool canMove = (stats == null || !stats.IsDead) && !isDodging;
            if (canMove)
            {
                float speed = heroData != null ? heroData.moveSpeed : 5f;

                if (isAttacking)
                    speed *= currentAttackSlowMultiplier;

                rb.linearVelocity = syncMoveInput * speed;
            }
        }

        // Все: анимация и flip (на основе SyncVar)
        animator.SetBool("IsMoving", syncMoveInput != Vector2.zero);

        if (spriteRenderer != null)
            spriteRenderer.flipX = syncFlipX;
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

        if (dirX != 0f)
        {
            bool flip = dirX < 0f;
            if (spriteRenderer != null)
                spriteRenderer.flipX = flip;
            // Синхронизируем с сервером, чтобы хитбоксы зеркалились правильно
            if (flip != syncFlipX)
                CmdSetFlip(flip);
        }
    }

    public void StartAttackSlow(float multiplier)
    {
        if (isServer)
        {
            isAttacking = true;
            currentAttackSlowMultiplier = multiplier;
        }
        else if (isLocalPlayer)
        {
            CmdSetAttackSlow(multiplier);
        }
    }

    public void EndAttackSlow()
    {
        if (isServer)
        {
            isAttacking = false;
            currentAttackSlowMultiplier = 1f;
        }
        else if (isLocalPlayer)
        {
            CmdSetAttackSlow(-1f);
        }
    }

    [Command]
    private void CmdSetAttackSlow(float multiplier)
    {
        if (multiplier < 0f)
        {
            isAttacking = false;
            currentAttackSlowMultiplier = 1f;
        }
        else
        {
            isAttacking = true;
            currentAttackSlowMultiplier = multiplier;
        }
    }

    private float currentAttackSlowMultiplier = 1f;

    // --- Атаки ---

    private void TryAttack1()
    {
        var ab = Ability;
        if (!isLocalPlayer || !inGame || !canAttack || ab == null) return;
        canAttack = false;

        FaceAttackDirection();
        ab.Attack1();
        CmdAttack(0);
        float cooldown = heroData != null ? heroData.attackCooldown : 0.5f;
        Invoke(nameof(ResetAttack), cooldown);
    }

    private void TryAttack2()
    {
        var ab = Ability;
        if (!isLocalPlayer || !inGame || !canAttack || ab == null) return;
        if (heroData != null && heroData.attackCount < 2) return;
        canAttack = false;

        FaceAttackDirection();
        ab.Attack2();
        CmdAttack(1);
        float cooldown = heroData != null ? heroData.attackCooldown : 0.5f;
        Invoke(nameof(ResetAttack), cooldown);
    }

    // --- Способности ---

    private void TryAbility1()
    {
        var ab = Ability;
        if (!isLocalPlayer || !inGame || ab == null) return;
        if (!ab.CanUseAbility1) return;

        FaceAttackDirection();
        ab.UseAbility1();
        CmdAbilityAttack(ab.PendingHitboxIndex, ab.PendingDamage, ab.LastTriggerName);
    }

    private void TryAbility2()
    {

    }

    /// Атака с хитбоксом — сервер готовит урон и проигрывает анимацию,
    /// чтобы Animation Event EnableHitbox() сработал на сервере.
    [Command]
    private void CmdAttack(int attackIndex)
    {
        var ab = Ability;
        if (ab == null) return;

        // Получаем урон, энергию и stagger из HeroData
        float damage;
        float energyGain;
        float staggerDamage;
        string triggerName;
        if (attackIndex == 0)
        {
            damage = heroData != null ? heroData.attack1Damage : 15f;
            energyGain = heroData != null ? heroData.attack1EnergyGain : 5f;
            staggerDamage = heroData != null ? heroData.attack1StaggerDamage : 5f;
            triggerName = "Attack1";
        }
        else
        {
            damage = heroData != null ? heroData.attack2Damage : 25f;
            energyGain = heroData != null ? heroData.attack2EnergyGain : 8f;
            staggerDamage = heroData != null ? heroData.attack2StaggerDamage : 10f;
            triggerName = "Attack2";
        }

        // Melee: подготавливаем хитбокс для Animation Event (EnableHitbox)
        ab.PrepareHitboxPublic(attackIndex, damage, energyGain, staggerDamage);

        // Ranged: подготавливаем данные для Animation Event (SpawnProjectile)
        ab.PrepareProjectile(attackIndex, damage, energyGain, syncFlipX, false);

        // Для выделенного клиента (не хост) — запускаем анимацию на сервере
        if (!isLocalPlayer)
            animator.SetTrigger(triggerName);

        // Рассылаем анимацию другим клиентам
        RpcPlayAttack(triggerName);
    }

    // --- Уклонение ---

    private void TryDodge()
    {
        if (!isLocalPlayer || !inGame || !canDodge || isDodging) return;

        // Проверяем энергию на сервере через CmdDodge, но предварительно
        // блокируем повторное нажатие на клиенте
        canDodge = false;

        // Прерываем текущую атаку
        if (isAttacking)
        {
            ResetAttackState();
            animator.Play("Idle", 0, 0f);
            canAttack = true;
            CancelInvoke(nameof(ResetAttack));
        }

        Vector2 dodgeDir = moveInput != Vector2.zero ? moveInput.normalized : lastMoveDir;
        float duration = heroData != null ? heroData.dodgeDuration : 0.25f;
        float cooldown = heroData != null ? heroData.dodgeCooldown : 1f;

        // Запускаем dodge на сервере (физика) и локально (визуал + кулдаун)
        CmdDodge(dodgeDir);
        StartCoroutine(DodgeLocalRoutine(duration, cooldown));
    }

    /// <summary>
    /// Локальная часть dodge: визуальный эффект (прозрачность) и кулдаун.
    /// Физика выполняется на сервере через CmdDodge.
    /// </summary>
    private IEnumerator DodgeLocalRoutine(float duration, float cooldown)
    {
        isDodging = true;

        // Ghost trail effect
        var ghost = GetComponent<DodgeGhostEffect>();
        if (ghost != null)
            ghost.SpawnGhosts(duration);

        // Визуальный эффект — полупрозрачность во время i-frames
        if (spriteRenderer != null)
        {
            var c = spriteRenderer.color;
            c.a = 0.4f;
            spriteRenderer.color = c;
        }

        yield return new WaitForSeconds(duration);

        isDodging = false;

        // Возвращаем непрозрачность
        if (spriteRenderer != null)
        {
            var c = spriteRenderer.color;
            c.a = 1f;
            spriteRenderer.color = c;
        }

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
    private void CmdSetFlip(bool flip)
    {
        syncFlipX = flip;
    }

    /// <summary>
    /// Ability-атака — сервер получает данные хитбокса и триггер от клиента.
    /// </summary>
    [Command]
    private void CmdAbilityAttack(int hitboxIndex, float damage, string triggerName)
    {
        // Тратим энергию на ability
        float abilityCost = heroData != null ? heroData.ability1EnergyCost : 25f;
        if (stats != null && !stats.SpendEnergy(abilityCost)) return;

        // Гиперброня — анимация абилки не прерывается
        if (stats != null)
            stats.hasHyperArmor = true;

        var ab = Ability;
        if (ab != null)
        {
            ab.PrepareHitboxPublic(hitboxIndex, damage);
            ab.PrepareProjectile(hitboxIndex, damage, 0f, syncFlipX, true);
        }

        // Для выделенного клиента — запускаем анимацию на сервере
        if (!isLocalPlayer)
            animator.SetTrigger(triggerName);

        RpcPlayAttack(triggerName);
    }

    [ClientRpc(includeOwner = false)]
    private void RpcPlayAttack(string triggerName)
    {
        var anim = GetComponent<Animator>();
        if (anim != null)
            anim.SetTrigger(triggerName);
    }

    private void OnFlipChanged(bool oldVal, bool newVal)
    {
        if (spriteRenderer != null)
            spriteRenderer.flipX = newVal;
    }

    /// <summary>
    /// Dodge — выполняется на сервере: импульс + неуязвимость.
    /// </summary>
    [Command]
    private void CmdDodge(Vector2 direction)
    {
        if (stats == null || stats.IsDead) return;

        // Тратим энергию на dodge
        float dodgeCost = heroData != null ? heroData.dodgeEnergyCost : 15f;
        if (!stats.SpendEnergy(dodgeCost)) return;

        float force = heroData != null ? heroData.dodgeForce : 8f;
        float duration = heroData != null ? heroData.dodgeDuration : 0.25f;

        StartCoroutine(ServerDodgeRoutine(direction, force, duration));
    }

    private static readonly int PlayerLayer = 6;  // "Player" layer
    private static readonly int EnemyLayer = 7;   // "Enemy" layer

    private IEnumerator ServerDodgeRoutine(Vector2 direction, float force, float duration)
    {
        isDodging = true;
        if (stats != null) stats.IsDodging = true;

        // Ignore Player↔Enemy collisions (walls and decorations still block)
        Physics2D.IgnoreLayerCollision(PlayerLayer, EnemyLayer, true);

        rb.linearVelocity = Vector2.zero;
        rb.AddForce(direction * force, ForceMode2D.Impulse);

        yield return new WaitForSeconds(duration);

        Physics2D.IgnoreLayerCollision(PlayerLayer, EnemyLayer, false);

        isDodging = false;
        if (stats != null) stats.IsDodging = false;
    }

    // --- Сброс кулдаунов ---

    private void ResetAttack() => canAttack = true;

    public void SpawnProjectile()
    {
        var ab = Ability;
        if (ab != null)
            ab.SpawnProjectile();
    }

    public void ResetAttackState()
    {
        var ab = Ability;
        if (ab != null)
        {
            foreach (var hitbox in GetComponentsInChildren<WeaponHitbox>(true))
            {
                if (hitbox != null)
                    hitbox.Deactivate();
            }
        }

        if (isServer)
        {
            isAttacking = false;
            currentAttackSlowMultiplier = 1f;
            if (stats != null)
                stats.hasHyperArmor = false;
        }
        else if (isLocalPlayer)
        {
            CmdSetAttackSlow(-1f);
        }
    }
}
