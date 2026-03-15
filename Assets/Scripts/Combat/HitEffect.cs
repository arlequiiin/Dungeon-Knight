using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Эффекты при попадании: knockback и hitstop.
/// Добавляется на любой объект с Rigidbody2D или NavMeshAgent.
/// </summary>
public class HitEffect : MonoBehaviour
{
    [Header("Knockback")]
    public float knockbackResistance = 0f; // 0 = полный knockback, 1 = иммунитет

    private NavMeshAgent navAgent;
    private Rigidbody2D rb;
    private Animator animator;

    private Coroutine knockbackCoroutine;

    private void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    /// <summary>
    /// Применяет knockback к цели.
    /// </summary>
    /// <param name="direction">Направление отталкивания (нормализуется внутри)</param>
    /// <param name="force">Сила отталкивания</param>
    public void ApplyKnockback(Vector2 direction, float force)
    {
        if (knockbackResistance >= 1f) return;

        float finalForce = force * (1f - knockbackResistance);
        Vector2 dir = direction.normalized;

        if (navAgent != null && navAgent.enabled)
        {
            // Мобы на NavMeshAgent — временно отключаем агент и двигаем корутиной
            if (knockbackCoroutine != null)
                StopCoroutine(knockbackCoroutine);
            knockbackCoroutine = StartCoroutine(KnockbackNavAgent(dir, finalForce));
        }
        else if (rb != null && rb.bodyType != RigidbodyType2D.Static)
        {
            // Игроки с Rigidbody2D
            rb.AddForce(dir * finalForce, ForceMode2D.Impulse);
        }
    }

    private IEnumerator KnockbackNavAgent(Vector2 direction, float force)
    {
        if (!navAgent.isOnNavMesh) yield break;

        navAgent.isStopped = true;
        navAgent.ResetPath();

        float duration = 0.15f;
        float elapsed = 0f;
        Vector3 velocity = (Vector3)(direction * force);

        while (elapsed < duration)
        {
            if (navAgent == null || !navAgent.enabled || !navAgent.isOnNavMesh)
                break;

            float t = 1f - (elapsed / duration);
            Vector3 delta = velocity * t * Time.deltaTime;

            Vector3 newPos = transform.position + delta;
            if (NavMesh.SamplePosition(newPos, out NavMeshHit hit, 0.5f, NavMesh.AllAreas))
                navAgent.Warp(hit.position);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
            navAgent.isStopped = false;

        knockbackCoroutine = null;
    }

    /// <summary>
    /// Применяет hitstop (микро-пауза анимации) к этому объекту.
    /// </summary>
    public void ApplyHitstop(float duration)
    {
        if (animator != null)
            StartCoroutine(HitstopCoroutine(duration));
    }

    private IEnumerator HitstopCoroutine(float duration)
    {
        animator.speed = 0f;
        yield return new WaitForSecondsRealtime(duration);
        animator.speed = 1f;
    }
}
