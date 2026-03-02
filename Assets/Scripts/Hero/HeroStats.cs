using Mirror;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(PlayerController))]
public class HeroStats : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnHealthChanged))]
    private float currentHealth;

    [SyncVar(hook = nameof(OnEnergyChanged))]
    private float currentEnergy;

    private float maxHealth;
    private float maxEnergy;

    // Для UI и эффектов
    public UnityEvent<float, float> onHealthChanged;   // (current, max)
    public UnityEvent<float, float> onEnergyChanged;   // (current, max)
    public UnityEvent onDeath;

    private bool isDead;

    // Неуязвимость во время рывка (устанавливается через Command с клиента)
    [SyncVar]
    public bool isDodging;
    public bool IsDodging { get => isDodging; set => isDodging = value; }

    // Компонент способностей — нужен для SwordsmanAbility.TryDodgeDamage()
    private HeroAbility ability;

    private void Awake()
    {
        ability = GetComponent<HeroAbility>();
    }

    public void Init(HeroData data)
    {
        maxHealth = data.maxHealth;
        maxEnergy = data.maxEnergy;
        currentHealth = maxHealth;
        currentEnergy = maxEnergy;
    }

    // Получение урона — вызывается только на сервере
    [Server]
    public void TakeDamage(float amount)
    {
        if (isDead) return;

        // Неуязвимость во время рывка
        if (IsDodging) return;

        // Шанс уклонения (Мечник)
        if (ability is SwordsmanAbility swordsman && swordsman.TryDodgeDamage())
            return;

        // Снижение урона от щита (Рыцарь, Тамплиер)
        if (ability is KnightAbility knight)
            amount *= knight.DamageMultiplier;

        currentHealth = Mathf.Max(0f, currentHealth - amount);

        RpcTriggerHurt();

        if (currentHealth <= 0f)
            Die();
    }

    // Восстановление здоровья — вызывается только на сервере
    [Server]
    public void Heal(float amount)
    {
        if (isDead) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }

    // Трата энергии — возвращает true если хватило энергии
    [Server]
    public bool SpendEnergy(float amount)
    {
        if (currentEnergy < amount) return false;
        currentEnergy = Mathf.Max(0f, currentEnergy - amount);
        return true;
    }

    [Server]
    public void RestoreEnergy(float amount)
    {
        currentEnergy = Mathf.Min(maxEnergy, currentEnergy + amount);
    }

    [ClientRpc]
    private void RpcTriggerHurt()
    {
        GetComponent<Animator>()?.SetTrigger("Hurt");
    }

    [Server]
    private void Die()
    {
        isDead = true;
        RpcOnDeath();
    }

    [ClientRpc]
    private void RpcOnDeath()
    {
        isDead = true;
        onDeath?.Invoke();

        var anim = GetComponent<Animator>();
        if (anim != null)
        {
            anim.SetBool("IsMoving", false);
            anim.SetTrigger("Death");
        }

        // Останавливаем тело и отключаем управление
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Static;
        }

        GetComponent<PlayerController>().enabled = false;
    }

    // SyncVar hooks — вызываются на всех клиентах при изменении значения
    private void OnHealthChanged(float oldVal, float newVal)
    {
        onHealthChanged?.Invoke(newVal, maxHealth);
    }

    private void OnEnergyChanged(float oldVal, float newVal)
    {
        onEnergyChanged?.Invoke(newVal, maxEnergy);
    }

    // Геттеры для UI
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float CurrentEnergy => currentEnergy;
    public float MaxEnergy => maxEnergy;
    public bool IsDead => isDead;

    public float HealthNormalized => maxHealth > 0 ? currentHealth / maxHealth : 0f;
    public float EnergyNormalized => maxEnergy > 0 ? currentEnergy / maxEnergy : 0f;
}
