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
        if (other.gameObject == owner) return;
        if (hitTargets.Contains(other.gameObject)) return;

        hitTargets.Add(other.gameObject);

        if (!NetworkServer.active) return;

        // Урон по мобам (мобы не бьют друг друга)
        var mobHealth = other.GetComponent<MobHealth>();
        if (mobHealth != null)
        {
            if (ownerIsMob) return; // дружественный огонь между мобами отключён
            mobHealth.TakeDamage(damage);
            return;
        }

        // Урон по игрокам
        var heroStats = other.GetComponent<HeroStats>();
        if (heroStats != null)
        {
            heroStats.TakeDamage(damage);
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
