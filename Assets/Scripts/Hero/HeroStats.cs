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

    [SyncVar]
    private float maxHealth;

    [SyncVar]
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

        // Reset attack state — deactivate hitboxes and clear attack slow
        var controller = GetComponent<PlayerController>();
        if (controller != null)
            controller.ResetAttackState();
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

        // Reset attack state before disabling
        var controller = GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.ResetAttackState();
            controller.enabled = false;
        }

        // Останавливаем тело
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Static;
        }

        // Анимация смерти
        var anim = GetComponent<Animator>();
        if (anim != null)
        {
            anim.ResetTrigger("Attack1");
            anim.ResetTrigger("Attack2");
            anim.ResetTrigger("Hurt");
            anim.SetBool("IsMoving", false);
            anim.SetTrigger("Death");
        }

        onDeath?.Invoke();
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
