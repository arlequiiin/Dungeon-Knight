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

    // Кулдауны задаются в наследниках
    public float ability1Cooldown = 5f;
    public float ability2Cooldown = 10f;

    private float ability1Timer;
    private float ability2Timer;

    public bool CanUseAbility1 => ability1Timer <= 0f;
    public bool CanUseAbility2 => ability2Timer <= 0f;

    protected virtual void Awake()
    {
        player = GetComponent<PlayerController>();
        animator = GetComponent<Animator>();
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
        if (ability2Timer > 0f) ability2Timer -= Time.deltaTime;
    }

    // Атака 1 — есть у всех героев
    public abstract void Attack1();

    // Атака 2 — только если HeroData.attackCount == 2
    public virtual void Attack2() { }

    // Способность 1 — уникальная активная способность
    public void UseAbility1()
    {
        if (!CanUseAbility1) return;
        OnAbility1();
        ability1Timer = ability1Cooldown;
    }

    // Способность 2 — вторая уникальная способность (у тех, у кого есть)
    public void UseAbility2()
    {
        if (!CanUseAbility2) return;
        OnAbility2();
        ability2Timer = ability2Cooldown;
    }

    protected abstract void OnAbility1();
    protected virtual void OnAbility2() { }

    // --- Animation Event методы ---
    // Вызываются из анимации на кадре удара.
    // В окне Animation: Add Event → Function = "EnableHitbox" / "DisableHitbox"

    // Урон для текущей атаки — задаётся наследником перед запуском анимации
    protected float pendingDamage;
    protected int pendingHitboxIndex;

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

    // Возвращает текущий кулдаун для UI
    public float GetAbility1CooldownNormalized() =>
        ability1Cooldown > 0 ? Mathf.Clamp01(ability1Timer / ability1Cooldown) : 0f;

    public float GetAbility2CooldownNormalized() =>
        ability2Cooldown > 0 ? Mathf.Clamp01(ability2Timer / ability2Cooldown) : 0f;
}
