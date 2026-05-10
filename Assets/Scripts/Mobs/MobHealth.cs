using Mirror;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

/// <summary>
/// Компонент здоровья моба. Управляется сервером.
/// </summary>
public class MobHealth : NetworkBehaviour
{
    [SyncVar]
    private float maxHealth = 40f;

    [SyncVar(hook = nameof(OnHealthChanged))]
    private float currentHealth;

    [SyncVar]
    private bool isBoss;

    private bool isDead;

    // === Poise / Stagger ===
    private float maxPoise = 30f;
    private float currentPoise;
    private float poiseRecoveryRate;
    private float staggerDuration = 1.5f;
    private bool isStaggered;

    /// <summary>
    /// Множитель урона во время стагера (x1.5)
    /// </summary>
    public bool IsStaggered => isStaggered;
    public float DamageMultiplier => isStaggered ? 1.5f : 1f;

    // === Shield (пассивный фронтальный блок для ArmoredSkeleton и т.п.) ===
    [Header("Shield")]
    public bool hasShield = false;

    /// <summary>
    /// Пытается заблокировать удар: фронтальный, моб не в стагере.
    /// Урон полностью идёт в poise. Возвращает true если блок успешен.
    /// </summary>
    [Server]
    public bool TryBlock(float incomingDamage, Vector2 attackerPos)
    {
        if (!hasShield || isDead || isStaggered) return false;

        var sr = GetComponent<SpriteRenderer>();
        bool facingLeft = sr != null && sr.flipX;
        bool attackerOnLeft = attackerPos.x < transform.position.x;
        bool isFrontalAttack = facingLeft == attackerOnLeft;
        if (!isFrontalAttack) return false;

        TakePoiseDamage(incomingDamage);
        RpcPlayBlockedAnim();
        return true;
    }

    [ClientRpc]
    private void RpcPlayBlockedAnim()
    {
        var anim = GetComponent<Animator>();
        if (anim != null) anim.SetTrigger("Blocked");
    }

    public UnityEvent onDeath;

    public bool IsDead => isDead;
    public bool IsBoss => isBoss;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float HealthNormalized => maxHealth > 0 ? currentHealth / maxHealth : 0f;

    public override void OnStartServer()
    {
        currentHealth = maxHealth;
        currentPoise = maxPoise;
    }

    /// <summary>
    /// Sets max health (from MobData + scaling). Call before NetworkServer.Spawn().
    /// </summary>
    public void SetMaxHealth(float value)
    {
        maxHealth = value;
        currentHealth = value;
    }

    /// <summary>
    /// Marks this mob as a boss (enables boss HP bar on clients).
    /// </summary>
    public void SetBoss(bool value) => isBoss = value;

    /// <summary>
    /// Sets poise params (from MobData). Call before NetworkServer.Spawn().
    /// </summary>
    public void SetPoise(float max, float recoveryRate, float stagDuration)
    {
        maxPoise = max;
        currentPoise = max;
        poiseRecoveryRate = recoveryRate;
        staggerDuration = stagDuration;
    }

    [Server]
    public void TakeDamage(float amount)
    {
        if (isDead) return;

        // Множитель урона при стагере (x1.5)
        float finalDamage = amount * DamageMultiplier;
        currentHealth = Mathf.Max(0f, currentHealth - finalDamage);

        if (currentHealth <= 0f)
        {
            Die();
        }
        else
        {
            var ai = GetComponent<MobAI>();

            // Hurt-анимация И AI hit-reaction срабатывают только если урон превысил порог
            // (% от maxHealth) из MobData. Мелкие тычки моб игнорирует — продолжает атаковать
            // без вздрагивания. Порог 0 = реагирует всегда (старое поведение).
            float threshold = ai != null && ai.mobData != null ? ai.mobData.hurtAnimDamageThreshold : 0f;
            bool exceedsThreshold = threshold <= 0f
                                    || (maxHealth > 0f && finalDamage / maxHealth >= threshold);
            if (exceedsThreshold)
            {
                RpcPlayHurt();
                if (ai != null) ai.OnHit();
            }
        }
    }

    /// <summary>
    /// Наносит урон по устойчивости (poise). При 0 — стагер.
    /// </summary>
    [Server]
    public void TakePoiseDamage(float amount)
    {
        if (isDead || isStaggered) return;

        currentPoise = Mathf.Max(0f, currentPoise - amount);

        if (currentPoise <= 0f)
            EnterStagger();
    }

    [Server]
    private void EnterStagger()
    {
        isStaggered = true;

        var ai = GetComponent<MobAI>();
        if (ai != null) ai.OnStagger(staggerDuration);

        RpcEnterStagger(staggerDuration);
        Invoke(nameof(ExitStagger), staggerDuration);
    }

    [Server]
    private void ExitStagger()
    {
        if (isDead) return;
        isStaggered = false;
        currentPoise = maxPoise;

        var ai = GetComponent<MobAI>();
        if (ai != null) ai.OnStaggerEnd();

        RpcExitStagger();
    }

    [ClientRpc]
    private void RpcEnterStagger(float duration)
    {
        var anim = GetComponent<Animator>();
        if (anim != null) anim.speed = 0f;

        StartCoroutine(StaggerFlashCoroutine(duration));
    }

    [ClientRpc]
    private void RpcExitStagger()
    {
        var anim = GetComponent<Animator>();
        if (anim != null) anim.speed = 1f;

        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = Color.white;
    }

    private System.Collections.IEnumerator StaggerFlashCoroutine(float duration)
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null) yield break;

        Color staggerColor = new Color(1f, 1f, 0.3f); // жёлтый
        float elapsed = 0f;
        float flashInterval = 0.15f;
        bool toggle = false;

        while (elapsed < duration)
        {
            sr.color = toggle ? Color.white : staggerColor;
            toggle = !toggle;
            yield return new WaitForSeconds(flashInterval);
            elapsed += flashInterval;
        }

        sr.color = Color.white;
    }

    private void Update()
    {
        if (!isServer) return;
        if (isDead || isStaggered) return;

        // Восстановление poise (для боссов)
        if (poiseRecoveryRate > 0f && currentPoise < maxPoise)
            currentPoise = Mathf.Min(maxPoise, currentPoise + poiseRecoveryRate * Time.deltaTime);
    }

    [Server]
    private void Die()
    {
        if (isDead) return;
        isDead = true;

        var data = GetComponent<MobAI>()?.mobData;
        Analytics.Event("mob_killed",
            "mob", data != null ? data.mobName : gameObject.name,
            "boss", isBoss);

        DropCoins();
        RpcOnDeath();
    }

    [Server]
    private void DropCoins()
    {
        var data = GetComponent<MobAI>()?.mobData;
        if (data == null) return;

        int amount = UnityEngine.Random.Range(data.coinDropMin, data.coinDropMax + 1);
        if (amount <= 0) return;

        NetworkServer.SendToAll(new CoinDropMessage { amount = amount });
    }

    [ClientRpc]
    private void RpcOnDeath()
    {
        isDead = true;

        var ai = GetComponent<MobAI>();
        if (ai != null)
        {
            ai.DisableHitbox();
            ai.enabled = false;
        }

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

        // Сброс скорости аниматора (может быть 0 после стагера)
        var anim = GetComponent<Animator>();
        if (anim != null)
        {
            anim.speed = 1f;
            foreach (var param in anim.parameters)
            {
                if (param.type == AnimatorControllerParameterType.Trigger && param.name != "Death")
                    anim.ResetTrigger(param.name);
            }
            anim.SetBool("IsMoving", false);
            anim.SetTrigger("Death");
        }

        onDeath?.Invoke();

        // Уничтожаем объект через 5 минут
        if (isServer)
            Invoke(nameof(DestroyMob), 300f);
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

    private void OnHealthChanged(float oldVal, float newVal)
    {
        onHealthChanged?.Invoke(newVal, maxHealth);
    }

    public UnityEvent<float, float> onHealthChanged;
}
