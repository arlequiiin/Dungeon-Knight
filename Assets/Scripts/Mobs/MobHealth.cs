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

        // Hurt всегда проигрывается (даже при смерти)
        RpcPlayHurt();

        if (currentHealth <= 0f)
        {
            Die();
        }
        else
        {
            // Уведомляем AI о получении урона (HitReaction)
            var ai = GetComponent<MobAI>();
            if (ai != null) ai.OnHit();
        }
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

        var ai = GetComponent<MobAI>();
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

        // Ставим триггер Death — аниматор перейдёт Hurt → Death по transition
        var anim = GetComponent<Animator>();
        if (anim != null)
        {
            anim.ResetTrigger("Attack1");
            anim.ResetTrigger("Attack2");
            anim.ResetTrigger("Attack3");
            anim.SetBool("IsMoving", false);
            anim.SetTrigger("Death");
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

        anim.ResetTrigger("Hurt");
        anim.SetTrigger("Hurt");
    }

    private void DestroyMob() => NetworkServer.Destroy(gameObject);

    private void OnHealthChanged(float oldVal, float newVal) { }
}
