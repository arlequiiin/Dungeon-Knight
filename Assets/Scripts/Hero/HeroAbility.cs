using UnityEngine;

/// <summary>
/// Базовый класс для всех способностей героев.
/// Добавляется как компонент на префаб игрока при выборе героя.
/// PlayerController вызывает методы этого класса, не зная конкретную реализацию.
/// </summary>
public abstract class HeroAbility : MonoBehaviour
{
    protected PlayerController player;
    protected Animator animator;
    protected WeaponHitbox[] weaponHitboxes;

    public float ability1Cooldown = 5f;

    private float ability1Timer;

    public bool CanUseAbility1 => ability1Timer <= 0f;

    protected virtual void Awake()
    {
        player = GetComponent<PlayerController>();
        animator = GetComponent<Animator>();
    }

    /// <summary>
    /// Applies cooldowns from HeroData. Called after InitHero.
    /// </summary>
    public virtual void ApplyHeroData(HeroData data)
    {
        if (data == null) return;
        ability1Cooldown = data.ability1Cooldown;
    }

    /// <summary>
    /// Обновляет массив хитбоксов. Вызывается после InitHero, когда дочерние объекты уже созданы.
    /// </summary>
    public void RefreshHitboxes()
    {
        weaponHitboxes = GetComponentsInChildren<WeaponHitbox>(true);
    }

    /// <summary>
    /// Возвращает хитбокс по индексу (0=Attack1, 1=Attack2, 2=Ability1 и т.д.)
    /// </summary>
    protected WeaponHitbox GetHitbox(int index)
    {
        if (weaponHitboxes == null || index < 0 || index >= weaponHitboxes.Length)
            return null;
        return weaponHitboxes[index];
    }

    protected virtual void Update()
    {
        if (ability1Timer > 0f) ability1Timer -= Time.deltaTime;
    }

    // Атака 1 — есть у всех героев (вызывается на клиенте для анимации)
    public abstract void Attack1();

    // Атака 2 — только если HeroData.attackCount == 2
    public virtual void Attack2() { }

    /// <summary>
    /// Server-side attack logic. Called from CmdAttack on the server.
    /// Melee heroes: PrepareHitbox (default). Ranged heroes: override to spawn projectiles.
    /// </summary>
    public virtual void ServerAttack(int attackIndex, float damage, bool flipX) { }

    // Способность 1 — уникальная активная способность
    public void UseAbility1()
    {
        if (!CanUseAbility1) return;
        OnAbility1();
        ability1Timer = ability1Cooldown;
    }

    protected abstract void OnAbility1();

    /// <summary>
    /// Server-side ability logic. Called from CmdAbilityAttack on the server.
    /// Override for abilities that need server spawning (projectiles, AoE).
    /// </summary>
    public virtual void ServerAbility1(bool flipX) { }

    // --- Animation Event методы ---
    // Вызываются из анимации на кадре удара.
    // Melee: EnableHitbox / DisableHitbox
    // Ranged: SpawnProjectile (спавн снаряда в нужный кадр анимации)

    // Урон для текущей атаки — задаётся наследником перед запуском анимации
    protected float pendingDamage;
    protected int pendingHitboxIndex;

    // Pending data for ranged attacks (set in CmdAttack, used by SpawnProjectile event)
    private bool pendingIsAbility;
    private bool pendingFlipX;
    private bool projectileReady;

    /// <summary>
    /// Stores data for deferred projectile spawn via Animation Event.
    /// </summary>
    public void PrepareProjectile(int attackIndex, float damage, bool flipX, bool isAbility)
    {
        pendingHitboxIndex = attackIndex;
        pendingDamage = damage;
        pendingFlipX = flipX;
        pendingIsAbility = isAbility;
        projectileReady = true;
    }

    /// <summary>
    /// Animation Event: spawns projectile/AoE at the correct frame.
    /// Add this event on the cast frame of ranged attack animations.
    /// </summary>
    public void SpawnProjectile()
    {
        if (!Mirror.NetworkServer.active) return;
        if (!projectileReady) return;
        projectileReady = false;

        if (pendingIsAbility)
            ServerAbility1(pendingFlipX);
        else
            ServerAttack(pendingHitboxIndex, pendingDamage, pendingFlipX);
    }

    // Последний использованный триггер — нужен для синхронизации на сервере
    private string lastTriggerName;

    // Публичные геттеры для PlayerController (отправка данных на сервер)
    public int PendingHitboxIndex => pendingHitboxIndex;
    public float PendingDamage => pendingDamage;
    public string LastTriggerName => lastTriggerName ?? "";

    /// <summary>
    /// Вызывается из Animation Event на кадре, когда оружие должно начать наносить урон.
    /// </summary>
    public void EnableHitbox()
    {
        var hitbox = GetHitbox(pendingHitboxIndex);
        if (hitbox != null)
            hitbox.Activate(pendingDamage);
    }

    /// <summary>
    /// Вызывается из Animation Event когда замах закончился.
    /// </summary>
    public void DisableHitbox()
    {
        var hitbox = GetHitbox(pendingHitboxIndex);
        if (hitbox != null)
            hitbox.Deactivate();
    }

    /// <summary>
    /// Подготавливает данные для Animation Event. Вызывается перед SetTrigger.
    /// </summary>
    protected void PrepareHitbox(int index, float damage)
    {
        pendingHitboxIndex = index;
        pendingDamage = damage;
    }

    /// <summary>
    /// Обёртка над animator.SetTrigger — запоминает имя триггера для сетевой синхронизации.
    /// </summary>
    protected void PlayTrigger(string triggerName)
    {
        lastTriggerName = triggerName;
        animator.SetTrigger(triggerName);
    }

    /// <summary>
    /// Публичный доступ для PrepareHitbox — используется PlayerController
    /// при выполнении атаки на сервере (Command).
    /// </summary>
    public void PrepareHitboxPublic(int index, float damage)
    {
        PrepareHitbox(index, damage);
    }

    // Возвращает текущий кулдаун для UI
    public float GetAbility1CooldownNormalized() =>
        ability1Cooldown > 0 ? Mathf.Clamp01(ability1Timer / ability1Cooldown) : 0f;
}
