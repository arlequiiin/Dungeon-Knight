using UnityEngine;

[CreateAssetMenu(fileName = "MobData", menuName = "Dungeon Knight/Mob Data")]
public class MobData : ScriptableObject
{
    [Header("General")]
    public string mobName;
    public MobType mobType;

    [Header("Health")]
    public float maxHealth = 40f;

    [Header("Detection")]
    public float detectionRange = 5f;
    public float loseRange = 8f;

    [Header("Attack")]
    public float attackRange = 0.8f;
    public float attackCooldown = 1.2f;
    public float[] attackDamages = { 10f };

    [Header("Attack Weights")]
    [Tooltip("Weights for each attack (same order as attackDamages). If empty, equal probability.")]
    public float[] attackWeights;

    [Header("Patrol")]
    public float patrolRadius = 2.5f;
    public float patrolWaitMin = 1f;
    public float patrolWaitMax = 3f;

    [Header("Movement")]
    public float moveSpeed = 2f;

    [Header("Reaction")]
    public float hitReactionDuration = 0.3f;
    public float recoveryDuration = 0.4f;
    public bool canBeInterrupted = true;

    [Header("Hit Effects")]
    [Range(0f, 1f)]
    [Tooltip("0 = full knockback, 1 = immune")]
    public float knockbackResistance = 0f;

    [Header("Loot")]
    public int coinDropMin = 1;
    public int coinDropMax = 3;
}

public enum MobType
{
    SkeletonWarrior,
    ArmoredSkeleton,
    SkeletonGreatsword,
    SkeletonArcher,
    SkeletonOverlord
}
