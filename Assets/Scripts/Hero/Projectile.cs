using Mirror;
using UnityEngine;

/// <summary>
/// Снаряд (стрела, заклинание и т.д.).
/// Летит в заданном направлении, наносит урон при столкновении и уничтожается.
/// Требует Rigidbody2D и Collider2D (isTrigger) на объекте.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Projectile : MonoBehaviour
{
    [Header("Настройки по умолчанию")]
    public float defaultDamage = 10f;
    public float defaultSpeed = 10f;
    public float lifetime = 5f;

    private float damage;
    private Rigidbody2D rb;
    private bool initialized;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;

        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    /// <summary>
    /// Инициализирует снаряд с заданным уроном, направлением и скоростью.
    /// </summary>
    public void Init(float projectileDamage, Vector2 direction, float speed)
    {
        damage = projectileDamage;
        rb.linearVelocity = direction.normalized * speed;
        initialized = true;

        // Поворот спрайта по направлению полёта
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        Destroy(gameObject, lifetime);
    }

    private void Start()
    {
        // Если Init не был вызван — используем значения по умолчанию (летит вправо)
        if (!initialized)
        {
            damage = defaultDamage;
            rb.linearVelocity = transform.right * defaultSpeed;
            Destroy(gameObject, lifetime);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!NetworkServer.active) return;

        // Урон по мобам
        var mobHealth = other.GetComponent<MobHealth>();
        if (mobHealth != null)
        {
            mobHealth.TakeDamage(damage);
            NetworkServer.Destroy(gameObject);
            return;
        }

        // Урон по игрокам
        var heroStats = other.GetComponent<HeroStats>();
        if (heroStats != null)
        {
            heroStats.TakeDamage(damage);
            NetworkServer.Destroy(gameObject);
            return;
        }
    }
}
