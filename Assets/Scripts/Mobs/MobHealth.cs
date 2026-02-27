using Mirror;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

/// <summary>
/// Компонент здоровья моба. Управляется сервером.
/// </summary>
public class MobHealth : NetworkBehaviour
{
    [Header("Характеристики")]
    public float maxHealth = 40f;

    [SyncVar(hook = nameof(OnHealthChanged))]
    private float currentHealth;

    private bool isDead;

    public UnityEvent onDeath;

    public bool IsDead => isDead;
    public float CurrentHealth => currentHealth;

    public override void OnStartServer()
    {
        currentHealth = maxHealth;
    }

    [Server]
    public void TakeDamage(float amount)
    {
        if (isDead) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);

        RpcPlayHurt();

        if (currentHealth <= 0f)
            Die();
    }

    [Server]
    private void Die()
    {
        if (isDead) return;
        isDead = true;
        RpcOnDeath();
    }

    [ClientRpc]
    private void RpcOnDeath()
    {
        isDead = true;
        GetComponent<Animator>()?.SetTrigger("Death");
        var ai = GetComponent<SkeletonAI>();
        if (ai != null) ai.enabled = false;

        // Отключаем коллайдер чтобы не мешал
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // Останавливаем движение
        var agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Static;
        }

        onDeath?.Invoke();

        // Уничтожаем объект через 10 секунд
        if (isServer)
            Invoke(nameof(DestroyMob), 10f);
    }

    [ClientRpc]
    private void RpcPlayHurt()
    {
        var anim = GetComponent<Animator>();
        if (anim == null) return;

        // ResetTrigger + SetTrigger — гарантирует повторное срабатывание
        // даже если предыдущий Hurt ещё не завершился
        anim.ResetTrigger("Hurt");
        anim.SetTrigger("Hurt");
    }

    private void DestroyMob() => NetworkServer.Destroy(gameObject);

    private void OnHealthChanged(float oldVal, float newVal) { }
}
