using UnityEngine;

[CreateAssetMenu(fileName = "HeroData", menuName = "Dungeon Knight/Hero Data")]
public class HeroData : ScriptableObject
{
    [Header("Основное")]
    public string heroName;
    public HeroType heroType;
    public bool unlockedByDefault;

    [Tooltip("Цена разблокировки в meta-монетах. Игнорируется если unlockedByDefault = true.")]
    public int unlockCost = 100;

    [Header("Характеристики")]
    public float maxHealth = 100f;
    public float maxEnergy = 100f;
    public float moveSpeed = 5f;

    [Header("Атаки")]
    public int attackCount = 2;         // 1 или 2
    public float attack1Damage = 15f;
    public float attack2Damage = 25f;   // игнорируется если attackCount == 1
    public float attackCooldown = 0.5f;

    [Header("Энергия за удар")]
    public float attack1EnergyGain = 5f;
    public float attack2EnergyGain = 8f;

    [Header("Урон по устойчивости (Poise)")]
    public float attack1StaggerDamage = 5f;
    public float attack2StaggerDamage = 10f;

    [Header("Устойчивость героя (Poise)")]
    public float maxPoise = 40f;
    public float staggerDuration = 1f;

    [Header("Щит")]
    [Tooltip("Может ли герой использовать щит (Knight, Templar)")]
    public bool hasShield = false;
    [Tooltip("Сколько энергии тратится на 1 ед. блокированного урона")]
    public float blockEnergyPerDamage = 0.5f;
    [Tooltip("Множитель скорости передвижения с поднятым щитом. 1 = без замедления, 0.5 = 50% скорости.")]
    [Range(0.1f, 1f)] public float blockMoveSpeedMultiplier = 0.5f;

    [Header("Стоимость энергии")]
    public float dodgeEnergyCost = 15f;
    public float ability1EnergyCost = 25f;

    [Header("Target Search")]
    public float targetSearchRadius = 5f;       // радиус поиска ближайшего врага

    [Header("Abilities")]
    public float ability1Cooldown = 5f;

    [Header("Уклонение")]
    public float dodgeForce = 8f;
    public float dodgeCooldown = 1f;
    public float dodgeDuration = 0.25f;         // длительность рывка (iframes)

    [Header("Коллайдеры оружия (мили)")]
    [Tooltip("Префабы хитбоксов для каждой атаки. [0]=Attack1, [1]=Attack2, [2]=Ability1 и т.д.")]
    public GameObject[] weaponHitboxPrefabs;   // каждый префаб — свой Collider2D + WeaponHitbox

    [Header("Снаряды (ranged)")]
    [Tooltip("Префабы снарядов. [0]=Attack1, [1]=Ability1. Для мили-героев оставить пустым.")]
    public GameObject[] projectilePrefabs;

    [Header("Визуал")]
    public RuntimeAnimatorController animatorController;
    public Sprite icon;
}

public enum HeroType
{
    None = -1,
    Soldier,
    Knight,
    Templar,
    Swordsman,
    Archer,
    Wizard,
    Priest
}
