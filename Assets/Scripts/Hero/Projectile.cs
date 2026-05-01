using Mirror;
using UnityEngine;

/// <summary>
/// Снаряд (стрела, заклинание и т.д.).
/// Летит в заданном направлении, наносит урон при столкновении.
///
/// Два режима:
/// - explosionRadius == 0: прямое попадание (single target), уничтожается сразу
/// - explosionRadius > 0: AoE взрыв, останавливается, проигрывает триггер "Explode",
///   уничтожается после explosionDuration
///
/// Homing (optional): если homing = true, снаряд запоминает позицию ближайшего врага,
/// стартует с дугой вверх и плавно поворачивает к сохранённой точке.
/// Не отслеживает цель — летит туда, где враг был на момент выстрела.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Projectile : NetworkBehaviour
{
    [Header("Настройки по умолчанию")]
    public float defaultDamage = 10f;
    public float defaultSpeed = 10f;
    public float lifetime = 5f;

    [Header("Explosion (optional)")]
    public float explosionRadius = 0f;
    public float explosionDuration = 0.5f;

    [Header("Homing (optional)")]
    public bool homing = false;
    public float homingTurnSpeed = 200f;
    public float homingSearchRadius = 8f;
    [Range(0f, 45f)]
    public float arcAngle = 25f;

    // Sync for clients
    [SyncVar] private Vector2 syncVelocity;
    [SyncVar] private float syncAngle;

    private float damage;
    private float speed;
    private float energyGain;
    private Rigidbody2D rb;
    private Collider2D col;
    private bool initialized;
    private bool hasHit;
    private GameObject owner;
    private bool ownerIsMob;
    private float currentAngle;

    // Homing: saved target position (not tracking)
    private Vector2 targetPosition;
    private bool hasTarget;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;

        col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    public void Init(float projectileDamage, Vector2 direction, float projectileSpeed, GameObject projectileOwner = null, float projectileEnergyGain = 0f)
    {
        damage = projectileDamage;
        speed = projectileSpeed;
        owner = projectileOwner;
        ownerIsMob = owner != null && owner.GetComponent<MobHealth>() != null;
        energyGain = projectileEnergyGain;
        initialized = true;

        float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        if (homing)
        {
            // Find nearest enemy and save its position
            if (owner != null)
            {
                Transform enemy = FindNearestEnemy(owner.transform.position);
                if (enemy != null)
                {
                    targetPosition = enemy.position;
                    hasTarget = true;
                }
            }

            // Start with upward arc (0 to +arcAngle)
            float offset = Random.Range(0f, arcAngle);
            // Flip arc direction based on facing: right = up, left = down would look wrong
            // Always arc upward regardless of direction
            float sign = direction.x >= 0f ? 1f : -1f;
            currentAngle = baseAngle + offset * sign;
        }
        else
        {
            currentAngle = baseAngle;
        }

        ApplyVelocity();
        Invoke(nameof(DestroySelf), lifetime);
    }

    public override void OnStartClient()
    {
        if (!isServer)
        {
            rb.linearVelocity = syncVelocity;
            transform.rotation = Quaternion.Euler(0f, 0f, syncAngle);
        }
    }

    private void Start()
    {
        if (!initialized && isServer)
        {
            damage = defaultDamage;
            speed = defaultSpeed;
            currentAngle = 0f;
            ApplyVelocity();
            Invoke(nameof(DestroySelf), lifetime);
        }
    }

    private void FixedUpdate()
    {
        if (!isServer) return;
        if (hasHit) return;
        if (!homing) return;

        float desiredAngle = currentAngle;

        if (hasTarget)
        {
            // Turn towards saved target position (not tracking live position)
            Vector2 toTarget = targetPosition - (Vector2)transform.position;
            desiredAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
        }

        currentAngle = Mathf.MoveTowardsAngle(currentAngle, desiredAngle, homingTurnSpeed * Time.fixedDeltaTime);
        ApplyVelocity();
    }

    private void ApplyVelocity()
    {
        float rad = currentAngle * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        Vector2 vel = dir * speed;

        rb.linearVelocity = vel;
        transform.rotation = Quaternion.Euler(0f, 0f, currentAngle);

        syncVelocity = vel;
        syncAngle = currentAngle;
    }

    private Transform FindNearestEnemy(Vector3 from)
    {
        float bestDist = homingSearchRadius;
        Transform best = null;

        var hits = Physics2D.OverlapCircleAll(from, homingSearchRadius);
        foreach (var hit in hits)
        {
            if (owner != null && hit.gameObject == owner) continue;
            if (hit.gameObject == gameObject) continue;

            var mob = hit.GetComponent<MobHealth>();
            if (mob == null || mob.IsDead) continue;

            float d = Vector2.Distance(from, hit.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = hit.transform;
            }
        }

        return best;
    }

    private static int decorationLayer = -2; // -2 = ещё не инициализирован, -1 = слой не существует

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!NetworkServer.active) return;
        if (hasHit) return;

        // Пропускаем trigger-коллайдеры (RoomController, зоны детекта и т.д.)
        if (other.isTrigger) return;
        if (other.GetComponent<Projectile>() != null) return;
        var root = other.transform.root.gameObject;
        if (owner != null && root == owner) return;

        var mobHealth = other.GetComponentInParent<MobHealth>();
        var heroStats = other.GetComponentInParent<HeroStats>();

        // Снаряды пролетают сквозь всё, что не является целью или декорацией.
        // Декорации (деревья и т.п.) — на слое "Decoration", блокируют снаряды.
        if (mobHealth == null && heroStats == null)
        {
            if (decorationLayer == -2)
                decorationLayer = LayerMask.NameToLayer("Decoration");

            if (decorationLayer < 0 || other.gameObject.layer != decorationLayer)
                return; // не цель и не декорация — пропускаем
        }

        // Снаряды мобов не бьют других мобов (friendly fire)
        if (ownerIsMob && mobHealth != null)
            return;

        // Мобы не могут бить упавших игроков (пролетают мимо)
        if (ownerIsMob && heroStats != null && heroStats.IsDowned)
            return;

        // Снаряды союзника по упавшему → revive (без hit-эффектов)
        if (!ownerIsMob && heroStats != null && heroStats.IsDowned)
        {
            heroStats.ReviveDamage(damage);
            hasHit = true;
            NetworkServer.Destroy(gameObject);
            return;
        }

        // Декорация — снаряд останавливается
        if (mobHealth == null && heroStats == null)
        {
            hasHit = true;
            NetworkServer.Destroy(gameObject);
            return;
        }

        hasHit = true;

        if (explosionRadius > 0f)
        {
            bool hitAnyMob = false;
            var hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject) continue;
                var hitRoot = hit.transform.root.gameObject;
                if (owner != null && hitRoot == owner) continue;

                var mob = hit.GetComponentInParent<MobHealth>();
                if (mob != null && !mob.IsDead)
                {
                    if (ownerIsMob) continue;
                    if (mob.TryBlock(damage, transform.position)) continue;
                    mob.TakeDamage(damage);
                    hitAnyMob = true;
                    continue;
                }

                var hero = hit.GetComponentInParent<HeroStats>();
                if (hero != null && !hero.IsDead)
                {
                    if (hero.TryBlock(damage, transform.position)) continue;
                    hero.TakeDamage(damage);
                }
            }

            if (hitAnyMob)
                RestoreOwnerEnergy();

            RpcExplode();
            Invoke(nameof(DestroySelf), explosionDuration);
        }
        else
        {
            if (mobHealth != null)
            {
                if (mobHealth.TryBlock(damage, transform.position))
                {
                    NetworkServer.Destroy(gameObject);
                    return;
                }
                mobHealth.TakeDamage(damage);
                RestoreOwnerEnergy();
            }
            else if (heroStats != null)
            {
                if (!heroStats.TryBlock(damage, transform.position))
                    heroStats.TakeDamage(damage);
            }

            NetworkServer.Destroy(gameObject);
        }
    }

    [ClientRpc]
    private void RpcExplode()
    {
        rb.linearVelocity = Vector2.zero;
        if (col != null) col.enabled = false;

        transform.rotation = Quaternion.identity;

        var anim = GetComponent<Animator>();
        if (anim != null)
            anim.SetTrigger("Explode");
    }

    private void RestoreOwnerEnergy()
    {
        if (energyGain <= 0f || owner == null) return;
        var ownerStats = owner.GetComponent<HeroStats>();
        if (ownerStats != null)
            ownerStats.RestoreEnergy(energyGain);
    }

    private void DestroySelf()
    {
        NetworkServer.Destroy(gameObject);
    }
}
