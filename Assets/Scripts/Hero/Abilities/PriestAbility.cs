using System.Collections;
using Mirror;
using UnityEngine;

/// <summary>
/// Священник:
/// - Attack1 (ЛКМ): выбирает ближайшего врага в attackRange, наносит урон, спавнит AttackEffect на цели.
/// - Ability1 короткое нажатие Q: лечит ближайшего союзника на healAmount, спавнит HealEffect на нём.
/// - Ability1 удержание Q ≥ holdThreshold: лечит себя на healAmount/2, спавнит HealEffect на себе.
/// Эффекты привязаны к цели как дочерние объекты, чтобы следовать за ней.
/// </summary>
public class PriestAbility : HeroAbility
{
    [Header("Атака")]
    public float attackRange = 6f;

    [Header("Способность — лечение")]
    public float healAmount = 40f;
    public float allySearchRadius = 8f;

    // Префабы из HeroData.projectilePrefabs
    private GameObject attackEffectPrefab; // [0]
    private GameObject healEffectPrefab;   // [1]

    public override void ApplyHeroData(HeroData data)
    {
        base.ApplyHeroData(data);
        if (data.projectilePrefabs != null)
        {
            if (data.projectilePrefabs.Length > 0) attackEffectPrefab = data.projectilePrefabs[0];
            if (data.projectilePrefabs.Length > 1) healEffectPrefab = data.projectilePrefabs[1];
        }
    }

    // === Attack ===

    public override void Attack1()
    {
        PlayTrigger("Attack1");
    }

    public override void Attack2() { }

    public override void ServerAttack(int attackIndex, float damage, float energyGain, bool flipX)
    {
        Transform target = FindNearestEnemy();
        if (target == null) return;

        var mob = target.GetComponent<MobHealth>();
        if (mob == null || mob.IsDead) return;

        mob.TakeDamage(damage);

        // Энергия атакующему
        if (energyGain > 0f)
        {
            var stats = GetComponent<HeroStats>();
            if (stats != null) stats.RestoreEnergy(energyGain);
        }

        // VFX — на цели
        SpawnEffectOnTarget(attackEffectPrefab, target);
    }

    // === Ability1 — heal ===

    protected override void OnAbility1()
    {
        PlayTrigger("Ability1");
    }

    /// <summary>
    /// Сервер: heal ally или self в зависимости от holdSelf.
    /// Используем pendingFlipX как флаг "self heal" (передаётся из CmdAbilityAttack).
    /// </summary>
    public override void ServerAbility1(bool flipX)
    {
        bool selfCast = flipX; // переиспользуем bool-параметр как "selfCast"

        if (selfCast)
        {
            // Self-heal только если HP не полное
            var selfStats = GetComponent<HeroStats>();
            if (selfStats != null && selfStats.CurrentHealth < selfStats.MaxHealth)
                HealTarget(transform, healAmount * 0.5f);
        }
        else
        {
            Transform ally = FindNearestWoundedAlly();
            if (ally != null)
                HealTarget(ally, healAmount);
        }
    }

    /// <summary>
    /// Клиентская проверка — есть ли валидная цель для каста.
    /// Без этого Use забирает кулдаун и энергию даже если каст не сработает.
    /// </summary>
    public override bool CanCastAbility1(bool selfCast)
    {
        var selfStats = GetComponent<HeroStats>();

        if (selfCast)
            return selfStats != null && selfStats.CurrentHealth < selfStats.MaxHealth;

        return FindNearestWoundedAllyClient() != null;
    }

    [Server]
    private void HealTarget(Transform target, float amount)
    {
        var stats = target.GetComponent<HeroStats>();
        if (stats != null && !stats.IsDead)
            stats.Heal(amount);

        SpawnEffectOnTarget(healEffectPrefab, target);
    }

    // === Helpers ===

    [Server]
    private void SpawnEffectOnTarget(GameObject prefab, Transform target)
    {
        if (prefab == null || target == null) return;

        var ni = target.GetComponent<NetworkIdentity>();
        if (ni == null) return;

        // Индекс эффекта в HeroData.projectilePrefabs: 0 = attack, 1 = heal.
        int effectIndex = (prefab == healEffectPrefab) ? 1 : 0;
        var pc = GetComponent<PlayerController>();
        if (pc != null)
            pc.RpcShowEffectOnTarget(ni, effectIndex);
    }

    private Transform FindNearestEnemy()
    {
        float bestDist = attackRange;
        Transform best = null;

        var hits = Physics2D.OverlapCircleAll(transform.position, attackRange);
        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;
            var mob = hit.GetComponent<MobHealth>();
            if (mob == null || mob.IsDead) continue;

            float dist = Vector2.Distance(transform.position, hit.transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = hit.transform;
            }
        }
        return best;
    }

    [Server]
    private Transform FindNearestWoundedAlly()
    {
        float bestDist = allySearchRadius;
        Transform best = null;

        foreach (var identity in NetworkServer.spawned.Values)
        {
            if (identity == null) continue;
            if (identity.gameObject == gameObject) continue;

            var stats = identity.GetComponent<HeroStats>();
            if (stats == null || stats.IsDead) continue;
            if (stats.CurrentHealth >= stats.MaxHealth) continue; // фул HP — пропустить

            float dist = Vector2.Distance(transform.position, identity.transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = identity.transform;
            }
        }
        return best;
    }

    /// <summary>
    /// Клиентский поиск — использует FindObjectsByType, т.к. NetworkServer.spawned пуст на клиенте.
    /// </summary>
    private Transform FindNearestWoundedAllyClient()
    {
        float bestDist = allySearchRadius;
        Transform best = null;

        foreach (var hs in FindObjectsByType<HeroStats>(FindObjectsSortMode.None))
        {
            if (hs == null) continue;
            if (hs.gameObject == gameObject) continue;
            if (hs.IsDead) continue;
            if (hs.CurrentHealth >= hs.MaxHealth) continue;

            float dist = Vector2.Distance(transform.position, hs.transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = hs.transform;
            }
        }
        return best;
    }
}
