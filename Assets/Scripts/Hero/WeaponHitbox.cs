using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// Коллайдер оружия ближнего боя.
/// Размещается на дочернем объекте героя с любым Collider2D (isTrigger).
/// Форма коллайдера задаётся на префабе хитбокса (Box, Polygon, Circle и т.д.).
/// Зеркалирование при flipX происходит через localScale.x.
/// </summary>
public class WeaponHitbox : MonoBehaviour
{
    [Header("Knockback")]
    public float knockbackForce = 3f;

    [Header("Hitstop")]
    public float hitstopDuration = 0.04f; // ~2-3 кадра при 60fps

    private float damage;
    private GameObject owner;
    private Collider2D hitboxCollider;
    private SpriteRenderer ownerSprite;
    private bool ownerIsMob;
    private readonly HashSet<GameObject> hitTargets = new();

    private void Awake()
    {
        hitboxCollider = GetComponent<Collider2D>();
        if (hitboxCollider != null)
        {
            hitboxCollider.isTrigger = true;
            hitboxCollider.enabled = false;
        }

        // Владелец — родительский объект (персонаж)
        owner = transform.root.gameObject;
        ownerSprite = owner.GetComponent<SpriteRenderer>();
        ownerIsMob = owner.GetComponent<MobHealth>() != null;
    }

    /// <summary>
    /// Активирует коллайдер оружия с указанным уроном.
    /// Зеркалит по X в зависимости от flipX персонажа.
    /// </summary>
    public void Activate(float attackDamage)
    {
        damage = attackDamage;
        hitTargets.Clear();

        // Зеркалим весь объект хитбокса если персонаж смотрит влево
        float flipSign = (ownerSprite != null && ownerSprite.flipX) ? -1f : 1f;
        var ls = transform.localScale;
        ls.x = Mathf.Abs(ls.x) * flipSign;
        transform.localScale = ls;

        if (hitboxCollider != null)
            hitboxCollider.enabled = true;
    }

    /// <summary>
    /// Деактивирует коллайдер оружия.
    /// </summary>
    public void Deactivate()
    {
        if (hitboxCollider != null)
            hitboxCollider.enabled = false;
        hitTargets.Clear();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var root = other.transform.root.gameObject;
        if (root == owner) return;
        if (hitTargets.Contains(root)) return;

        hitTargets.Add(root);

        if (!NetworkServer.active) return;

        // Направление knockback: от атакующего к цели
        Vector2 knockbackDir = (root.transform.position - owner.transform.position).normalized;

        // Урон по мобам (мобы не бьют друг друга)
        var mobHealth = other.GetComponentInParent<MobHealth>();
        if (mobHealth != null)
        {
            if (ownerIsMob) return;
            mobHealth.TakeDamage(damage);
            ApplyHitEffects(mobHealth.gameObject, knockbackDir);
            return;
        }

        // Урон по игрокам
        var heroStats = other.GetComponentInParent<HeroStats>();
        if (heroStats != null)
        {
            heroStats.TakeDamage(damage);
            ApplyHitEffects(heroStats.gameObject, knockbackDir);
        }
    }

    private void ApplyHitEffects(GameObject target, Vector2 direction)
    {
        // Knockback на цели
        var targetHitEffect = target.GetComponent<HitEffect>();
        if (targetHitEffect != null)
            targetHitEffect.ApplyKnockback(direction, knockbackForce);

        // Hitstop на цели и на атакующем
        if (hitstopDuration > 0f)
        {
            targetHitEffect?.ApplyHitstop(hitstopDuration);

            var ownerHitEffect = owner.GetComponent<HitEffect>();
            ownerHitEffect?.ApplyHitstop(hitstopDuration);
        }
    }

    private void OnDrawGizmos()
    {
        var col = GetComponent<Collider2D>();
        if (col == null) return;

        Gizmos.color = col.enabled ? new Color(1f, 0f, 0f, 0.4f) : new Color(1f, 1f, 0f, 0.2f);
        Gizmos.matrix = transform.localToWorldMatrix;

        if (col is BoxCollider2D box)
        {
            Gizmos.DrawCube(box.offset, box.size);
        }
        else if (col is CircleCollider2D circle)
        {
            Gizmos.DrawSphere(circle.offset, circle.radius);
        }
    }
}
