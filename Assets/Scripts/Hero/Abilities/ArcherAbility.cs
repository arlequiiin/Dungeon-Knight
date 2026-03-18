using Mirror;
using UnityEngine;

// Лучник: 1 атака (стрела), способность — мощный выстрел
public class ArcherAbility : HeroAbility
{
    [Header("Обычный выстрел")]
    public float arrowSpeed = 15f;
    public float shootOffset = 0.5f;

    [Header("Мощный выстрел")]
    public float powerArrowDamage = 80f;
    public float powerArrowSpeed = 25f;

    // Assigned from HeroData.projectilePrefabs
    private GameObject arrowPrefab;
    private GameObject powerArrowPrefab;

    protected override void Awake()
    {
        base.Awake();
    }

    public override void ApplyHeroData(HeroData data)
    {
        base.ApplyHeroData(data);
        if (data.projectilePrefabs != null)
        {
            if (data.projectilePrefabs.Length > 0) arrowPrefab = data.projectilePrefabs[0];
            if (data.projectilePrefabs.Length > 1) powerArrowPrefab = data.projectilePrefabs[1];
        }
    }

    // Client-side: only play animation
    public override void Attack1()
    {
        PlayTrigger("Attack1");
    }

    public override void Attack2() { }

    // Server-side: spawn arrow
    public override void ServerAttack(int attackIndex, float damage, bool flipX)
    {
        if (arrowPrefab == null) return;

        Vector2 dir = flipX ? Vector2.left : Vector2.right;
        Vector3 spawnPos = transform.position + (Vector3)(dir * shootOffset);
        var arrow = Instantiate(arrowPrefab, spawnPos, Quaternion.identity);
        var proj = arrow.GetComponent<Projectile>();
        if (proj != null)
            proj.Init(damage, dir, arrowSpeed, gameObject);

        NetworkServer.Spawn(arrow);
    }

    protected override void OnAbility1()
    {
        PlayTrigger("Ability1");
    }

    // Server-side: spawn power arrow
    public override void ServerAbility1(bool flipX)
    {
        if (powerArrowPrefab == null) return;

        Vector2 dir = flipX ? Vector2.left : Vector2.right;
        Vector3 spawnPos = transform.position + (Vector3)(dir * shootOffset);
        var arrow = Instantiate(powerArrowPrefab, spawnPos, Quaternion.identity);
        var proj = arrow.GetComponent<Projectile>();
        if (proj != null)
            proj.Init(powerArrowDamage, dir, powerArrowSpeed, gameObject);

        NetworkServer.Spawn(arrow);
    }
}
