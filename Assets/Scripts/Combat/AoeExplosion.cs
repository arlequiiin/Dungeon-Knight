using Mirror;
using UnityEngine;

/// <summary>
/// AoE explosion that deals damage in a radius on spawn.
/// Spawned by the server, synced to clients via NetworkServer.Spawn().
/// Destroys itself after the VFX duration.
/// </summary>
public class AoeExplosion : NetworkBehaviour
{
    public float radius = 1.5f;
    public float vfxDuration = 1f;

    private float damage;
    private bool hasExploded;

    /// <summary>
    /// Call on server before NetworkServer.Spawn().
    /// </summary>
    public void Init(float aoeDamage)
    {
        damage = aoeDamage;
    }

    public override void OnStartServer()
    {
        if (hasExploded) return;
        hasExploded = true;

        var hits = Physics2D.OverlapCircleAll(transform.position, radius);
        foreach (var hit in hits)
        {
            var mobHealth = hit.GetComponent<MobHealth>();
            if (mobHealth != null && !mobHealth.IsDead)
            {
                mobHealth.TakeDamage(damage);
                continue;
            }

            var heroStats = hit.GetComponent<HeroStats>();
            if (heroStats != null && !heroStats.IsDead)
                heroStats.TakeDamage(damage);
        }

        Invoke(nameof(DestroySelf), vfxDuration);
    }

    private void DestroySelf()
    {
        NetworkServer.Destroy(gameObject);
    }
}
