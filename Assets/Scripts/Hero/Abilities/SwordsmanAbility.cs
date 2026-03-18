using UnityEngine;

// Мечник: 2 атаки, способность — многократная колющая атака
public class SwordsmanAbility : HeroAbility
{
    [Header("Атаки")]
    public float attack1Damage = 15f;
    public float attack2Damage = 25f;

    [Header("Многократная атака")]
    public float stabDamage = 12f;

    protected override void Awake()
    {
        base.Awake();
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
        PrepareHitbox(2, stabDamage);
        PlayTrigger("Ability1");
    }

    // Used by HeroStats for dodge damage check — no longer active
    public bool TryDodgeDamage() => false;
}
