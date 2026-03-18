using Mirror;
using UnityEngine;

// Солдат: 2 мили-атаки, способность — мощный выстрел из лука
public class SoldierAbility : HeroAbility
{
    [Header("Атака")]
    public float attack1Damage = 15f;
    public float attack2Damage = 25f;

    [Header("Мощный выстрел")]
    public float powerArrowDamage = 60f;
    public float powerArrowSpeed = 20f;
    public float shootOffset = 0.5f;

    // Assigned from HeroData.projectilePrefabs
    private GameObject powerArrowPrefab;

    protected override void Awake()
    {
        base.Awake();
    }

    public override void ApplyHeroData(HeroData data)
    {
        base.ApplyHeroData(data);
        if (data.projectilePrefabs != null && data.projectilePrefabs.Length > 0)
            powerArrowPrefab = data.projectilePrefabs[0];
    }

    public override void Attack1()
    {
        PrepareHitbox(0, attack1Damage);
        PlayTrigger("Attack1");
    }

    public override void Attack2()
    {
        PrepareHitbox(1, attack2Damage);
        PlayTrigger("Attack2");
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
