using System.Collections;
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
    public UnityEvent<float, float> onDownedHealthChanged;  // (current, max)
    public UnityEvent onDeath;
    public UnityEvent onDowned;
    public UnityEvent onRevived;

    private bool isDead;

    // === Downed (упавший герой) ===
    public const float DOWNED_MAX_HEALTH = 50f;
    private const float DOWNED_REGEN_DELAY = 3f;   // сек бездействия → регенерация
    private const float DOWNED_REGEN_DURATION = 3f; // сек на полное восстановление
    private const float REVIVE_HP_PERCENT = 0.3f;
    private const float REVIVE_ENERGY_PERCENT = 0.3f;

    [SyncVar(hook = nameof(OnDownedChanged))]
    private bool isDowned;

    [SyncVar(hook = nameof(OnDownedHealthChanged))]
    private float downedHealth;

    private float lastReviveHitTime;

    public bool IsDowned => isDowned;
    public float DownedHealth => downedHealth;
    public float DownedMaxHealth => DOWNED_MAX_HEALTH;
    public float DownedHealthNormalized => downedHealth / DOWNED_MAX_HEALTH;

    // Неуязвимость во время рывка (устанавливается через Command с клиента)
    [SyncVar]
    public bool isDodging;
    public bool IsDodging { get => isDodging; set => isDodging = value; }

    // Гиперброня — анимация не прерывается при получении урона (во время абилок)
    [SyncVar]
    public bool hasHyperArmor;

    // Щит — игрок удерживает блок (расход энергии за каждый блокированный удар)
    [SyncVar(hook = nameof(OnBlockingChanged))]
    public bool isBlocking;
    public bool hasShield;
    private float blockEnergyPerDamage = 0.5f;

    private void OnBlockingChanged(bool oldVal, bool newVal)
    {
        var anim = GetComponent<Animator>();
        if (anim != null)
            anim.SetBool("IsBlocking", newVal);
    }

    // === Poise / Stagger ===
    private float maxPoise = 40f;
    private float currentPoise;
    private float staggerDuration = 1f;
    private bool isStaggered;

    public bool IsStaggered => isStaggered;


    public void Init(HeroData data)
    {
        maxHealth = data.maxHealth;
        maxEnergy = data.maxEnergy;
        currentHealth = maxHealth;
        currentEnergy = maxEnergy;
        maxPoise = data.maxPoise;
        currentPoise = maxPoise;
        staggerDuration = data.staggerDuration;
        hasShield = data.hasShield;
        blockEnergyPerDamage = data.blockEnergyPerDamage;
    }

    /// <summary>
    /// Пытается заблокировать удар. Если щит активен и хватает энергии:
    /// — урон HP=0, весь damage идёт в poise
    /// — расходуется энергия пропорционально урону
    /// Возвращает true если блок успешен.
    /// Вызывается только на сервере.
    /// </summary>
    [Server]
    public bool TryBlock(float incomingDamage, Vector2 attackerPos)
    {
        if (!hasShield || !isBlocking || isDead || isStaggered) return false;

        // Фронтальная проверка: щит блокирует только спереди
        var sr = GetComponent<SpriteRenderer>();
        bool facingLeft = sr != null && sr.flipX;
        bool attackerOnLeft = attackerPos.x < transform.position.x;
        bool isFrontalAttack = facingLeft == attackerOnLeft;
        if (!isFrontalAttack) return false;

        // Проверка/расход энергии
        float cost = incomingDamage * blockEnergyPerDamage;
        if (currentEnergy < cost)
        {
            // Энергии не хватает — блок не срабатывает (получаем полный урон)
            return false;
        }
        currentEnergy = Mathf.Max(0f, currentEnergy - cost);

        // Урон полностью в poise
        TakePoiseDamage(incomingDamage);
        return true;
    }

    [Server]
    public void SetBlocking(bool value)
    {
        if (!hasShield) { isBlocking = false; return; }
        isBlocking = value;
    }

    // Получение урона — вызывается только на сервере
    [Server]
    public void TakeDamage(float amount)
    {
        if (isDead) return;

        // Мобы не могут бить упавшего (WeaponHitbox/Projectile проверяют IsDowned, это защита)
        if (isDowned) return;

        // Неуязвимость во время рывка
        if (IsDodging) return;

        // Применяем сопротивление урону от наград
        var mods = GetComponent<RunModifiers>();
        if (mods != null)
            amount = mods.ModifyIncomingDamage(amount);

        currentHealth = Mathf.Max(0f, currentHealth - amount);

        if (!hasHyperArmor)
            RpcTriggerHurt();

        if (currentHealth <= 0f)
            EnterDowned();
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

    // === Poise / Stagger ===

    [Server]
    public void TakePoiseDamage(float amount)
    {
        if (isDead || isStaggered) return;
        if (IsDodging || hasHyperArmor) return;

        currentPoise = Mathf.Max(0f, currentPoise - amount);

        if (currentPoise <= 0f)
            EnterStagger();
    }

    [Server]
    private void EnterStagger()
    {
        isStaggered = true;
        isBlocking = false;

        // Прерываем атаку
        var controller = GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.ResetAttackState();
            controller.ServerStopMovement();
            controller.enabled = false;
        }

        RpcEnterStagger(staggerDuration);
        Invoke(nameof(ExitStagger), staggerDuration);
    }

    [Server]
    private void ExitStagger()
    {
        isStaggered = false;
        currentPoise = maxPoise;

        var controller = GetComponent<PlayerController>();
        if (controller != null)
            controller.enabled = true;

        RpcExitStagger();
    }

    [ClientRpc]
    private void RpcEnterStagger(float duration)
    {
        var anim = GetComponent<Animator>();
        if (anim != null) anim.speed = 0f;

        if (isLocalPlayer)
            GetComponent<PlayerController>()?.ClientClearLocalInput();

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

    private IEnumerator StaggerFlashCoroutine(float duration)
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null) yield break;

        Color staggerColor = new Color(1f, 1f, 0.3f);
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

    [ClientRpc]
    private void RpcTriggerHurt()
    {
        GetComponent<Animator>()?.SetTrigger("Hurt");

        // Reset attack state — deactivate hitboxes and clear attack slow
        var controller = GetComponent<PlayerController>();
        if (controller != null)
            controller.ResetAttackState();
    }

    // === Downed System ===

    [Server]
    private void EnterDowned()
    {
        if (isDowned || isDead) return;

        // Награда "Вторая жизнь" — автоматический revive с полным HP, без downed-фазы
        var mods = GetComponent<RunModifiers>();
        if (mods != null && mods.ConsumeExtraLife())
        {
            currentHealth = maxHealth;
            return;
        }

        isDowned = true;
        downedHealth = DOWNED_MAX_HEALTH;

        var pc = GetComponent<PlayerController>();
        Analytics.Event("player_downed",
            "hero", pc != null && pc.heroData != null ? pc.heroData.heroName : "?");
        lastReviveHitTime = Time.time;

        // Прерываем атаку и отключаем управление
        var controller = GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.ResetAttackState();
            controller.ServerStopMovement();
            controller.enabled = false;
        }

        // Останавливаем тело (kinematic чтобы мобы не толкали, но триггер для revive работал)
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        RpcOnDowned();

        // Проверка game over
        GameOverWatcher.CheckAllDowned();
    }

    /// <summary>
    /// Союзник бьёт упавшего игрока → снимает downed-HP. При 0 → Revive.
    /// </summary>
    [Server]
    public void ReviveDamage(float amount)
    {
        if (!isDowned || isDead) return;

        downedHealth = Mathf.Max(0f, downedHealth - amount);
        lastReviveHitTime = Time.time;

        if (downedHealth <= 0f)
            Revive();
    }

    [Server]
    private void Revive()
    {
        if (!isDowned) return;

        isDowned = false;
        downedHealth = DOWNED_MAX_HEALTH;
        currentHealth = maxHealth * REVIVE_HP_PERCENT;
        currentEnergy = maxEnergy * REVIVE_ENERGY_PERCENT;

        var controller = GetComponent<PlayerController>();
        if (controller != null)
            controller.enabled = true;

        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.bodyType = RigidbodyType2D.Dynamic;

        RpcOnRevived();
    }

    /// <summary>
    /// Вызывается RoomController при зачистке комнаты: воскрешает всех упавших с 30% HP/маны.
    /// </summary>
    [Server]
    public void ForceRevive()
    {
        if (!isDowned || isDead) return;
        Revive();
    }

    private void Update()
    {
        if (!isServer) return;
        if (isDead) return;

        // Пассивная регенерация энергии (награда EnergyRegen)
        var mods = GetComponent<RunModifiers>();
        if (!isDowned && mods != null && mods.energyRegenPerSecond > 0f && currentEnergy < maxEnergy)
        {
            currentEnergy = Mathf.Min(maxEnergy, currentEnergy + mods.energyRegenPerSecond * Time.deltaTime);
        }

        if (!isDowned) return;

        // Регенерация downed-HP если нет ударов 3+ сек
        if (downedHealth < DOWNED_MAX_HEALTH && Time.time - lastReviveHitTime >= DOWNED_REGEN_DELAY)
        {
            float regenPerSec = DOWNED_MAX_HEALTH / DOWNED_REGEN_DURATION;
            downedHealth = Mathf.Min(DOWNED_MAX_HEALTH, downedHealth + regenPerSec * Time.deltaTime);
        }
    }

    [ClientRpc]
    private void RpcOnDowned()
    {
        // Анимация/визуал
        var anim = GetComponent<Animator>();
        if (anim != null)
        {
            anim.ResetTrigger("Attack1");
            anim.ResetTrigger("Attack2");
            anim.ResetTrigger("Hurt");
            anim.SetBool("IsMoving", false);
            // Если в аниматоре есть триггер "Downed" — проиграем его, иначе fallback на Death
            foreach (var p in anim.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Trigger && p.name == "Downed")
                {
                    anim.SetTrigger("Downed");
                    goto animDone;
                }
            }
            anim.SetTrigger("Death");
            animDone:;
        }

        var controller = GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.ResetAttackState();
            if (isLocalPlayer)
                controller.ClientClearLocalInput();
            controller.enabled = false;
        }

        onDowned?.Invoke();
    }

    [ClientRpc]
    private void RpcOnRevived()
    {
        var anim = GetComponent<Animator>();
        if (anim != null)
        {
            anim.ResetTrigger("Death");
            anim.ResetTrigger("Downed");
            anim.Play("Idle", 0, 0f);
        }

        var controller = GetComponent<PlayerController>();
        if (controller != null)
            controller.enabled = true;

        onRevived?.Invoke();
    }

    private void OnDownedChanged(bool oldVal, bool newVal)
    {
        if (newVal)
            onDowned?.Invoke();
        else
            onRevived?.Invoke();
    }

    private void OnDownedHealthChanged(float oldVal, float newVal)
    {
        onDownedHealthChanged?.Invoke(newVal, DOWNED_MAX_HEALTH);
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
            if (isLocalPlayer)
                controller.ClientClearLocalInput();
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
