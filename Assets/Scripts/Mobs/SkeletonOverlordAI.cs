using Mirror;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Skeleton Overlord: boss of Undead Crypt.
/// 2 attacks (fast slash + heavy slam), periodically summons skeleton minions.
/// High poise, poise recovery, knockback resistance.
/// All stats come from MobData.
/// </summary>
public class SkeletonOverlordAI : MobAI
{
    [Header("Summon")]
    [Tooltip("Prefabs of minions to summon (SkeletonWarrior, etc.)")]
    public GameObject[] summonPrefabs;
    public int summonsPerWave = 2;
    public float summonCooldown = 12f;
    public float summonAnimDuration = 1f;

    [Header("Summon Limits")]
    public int maxActiveMinions = 6;

    private static readonly string[] TriggerNames = { "Attack1", "Attack2" };

    private float summonTimer;
    private bool isSummoning;
    private float summonAnimTimer;
    private readonly System.Collections.Generic.List<GameObject> activeMinions = new();

    // Boss doesn't patrol — always detects players in the room
    protected override void Awake()
    {
        base.Awake();
        summonTimer = summonCooldown * 0.5f; // First summon faster
    }

    private void LateUpdate()
    {
        if (!isServer) return;
        if (health.IsDead) return;

        // Summon cooldown runs independently of FSM
        if (!isSummoning)
        {
            summonTimer -= Time.deltaTime;
            if (summonTimer <= 0f && CanSummon())
            {
                StartSummon();
            }
        }
        else
        {
            summonAnimTimer -= Time.deltaTime;
            if (summonAnimTimer <= 0f)
            {
                FinishSummon();
            }
        }
    }

    protected override void PerformAttack()
    {
        FaceTarget();

        int attack = ChooseWeightedAttack();
        float damage = GetAttackDamage(attack);
        float stagger = GetAttackStaggerDamage(attack);
        string trigger = attack < TriggerNames.Length ? TriggerNames[attack] : "Attack1";

        PrepareHitbox(attack, damage, stagger);
        animator.SetTrigger(trigger);
        RpcPlayTrigger(trigger);
    }

    // === Summon Logic ===

    private bool CanSummon()
    {
        // Clean up dead/destroyed minions
        activeMinions.RemoveAll(m => m == null);
        return activeMinions.Count < maxActiveMinions && summonPrefabs != null && summonPrefabs.Length > 0;
    }

    private void StartSummon()
    {
        isSummoning = true;
        summonAnimTimer = summonAnimDuration;

        // Stop moving during summon
        StopAgent();

        // Play summon animation
        animator.SetTrigger("Summon");
        RpcPlayTrigger("Summon");
    }

    private void FinishSummon()
    {
        isSummoning = false;
        summonTimer = summonCooldown;

        ResumeAgent();

        // Spawn minions around the boss
        int toSpawn = Mathf.Min(summonsPerWave, maxActiveMinions - activeMinions.Count);
        for (int i = 0; i < toSpawn; i++)
        {
            SpawnMinion(i, toSpawn);
        }

        RpcSummonEffect();
    }

    [Server]
    private void SpawnMinion(int index, int total)
    {
        // Pick random prefab
        var prefab = summonPrefabs[Random.Range(0, summonPrefabs.Length)];

        // Position around boss in a circle
        float angle = (360f / total) * index * Mathf.Deg2Rad;
        float radius = 1.5f;
        Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
        Vector3 spawnPos = transform.position + offset;

        // Snap to NavMesh
        if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            spawnPos = hit.position;

        var mob = Instantiate(prefab, spawnPos, Quaternion.identity);
        var ai = mob.GetComponent<MobAI>();
        if (ai != null)
        {
            // Summoned minions use boss's room center, no group manager
            ai.Init(roomCenter);
        }

        NetworkServer.Spawn(mob);
        activeMinions.Add(mob);
    }

    [ClientRpc]
    private void RpcSummonEffect()
    {
        // Visual feedback: brief flash
        if (spriteRenderer != null)
        {
            StartCoroutine(SummonFlash());
        }
    }

    private System.Collections.IEnumerator SummonFlash()
    {
        Color original = spriteRenderer.color;
        spriteRenderer.color = new Color(0.6f, 0.2f, 1f); // purple flash
        yield return new WaitForSeconds(0.3f);
        spriteRenderer.color = original;
    }
}
