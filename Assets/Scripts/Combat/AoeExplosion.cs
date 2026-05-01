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
    private float energyGain;
    private GameObject owner;
    private bool hasExploded;

    /// <summary>
    /// Call on server before NetworkServer.Spawn().
    /// </summary>
    public void Init(float aoeDamage, GameObject aoeOwner = null, float aoeEnergyGain = 0f)
    {
        damage = aoeDamage;
        owner = aoeOwner;
        energyGain = aoeEnergyGain;
    }

    public override void OnStartServer()
    {
        if (hasExploded) return;
        hasExploded = true;

        bool ownerIsHero = owner != null && owner.GetComponent<HeroStats>() != null;
        bool ownerIsMob = owner != null && owner.GetComponent<MobHealth>() != null;

        bool hitAnyMob = false;
        var hits = Physics2D.OverlapCircleAll(transform.position, radius);
        foreach (var hit in hits)
        {
            var hitRoot = hit.transform.root.gameObject;
            if (owner != null && hitRoot == owner) continue;

            var mobHealth = hit.GetComponent<MobHealth>();
            if (mobHealth != null && !mobHealth.IsDead)
            {
                if (ownerIsMob) continue; // мобы не бьют друг друга
                mobHealth.TakeDamage(damage);
                hitAnyMob = true;
                continue;
            }

            var heroStats = hit.GetComponent<HeroStats>();
            if (heroStats != null && !heroStats.IsDead)
            {
                if (ownerIsHero) continue; // взрывы от игрока не бьют союзников
                heroStats.TakeDamage(damage);
            }
        }

        if (hitAnyMob && energyGain > 0f && owner != null)
        {
            var ownerStats = owner.GetComponent<HeroStats>();
            if (ownerStats != null)
                ownerStats.RestoreEnergy(energyGain);
        }

        Invoke(nameof(DestroySelf), vfxDuration);
    }

    private void DestroySelf()
    {
        NetworkServer.Destroy(gameObject);
    }
}
