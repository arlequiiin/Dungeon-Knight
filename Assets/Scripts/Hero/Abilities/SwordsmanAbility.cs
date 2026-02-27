using UnityEngine;

// Мечник: 2 атаки, способности — многократная колющая атака и шанс не получить урон
public class SwordsmanAbility : HeroAbility
{
    [Header("Атаки")]
    public float attack1Damage = 15f;
    public float attack2Damage = 25f;

    [Header("Многократная атака")]
    public int stabCount = 4;
    public float stabDamage = 12f;
    public float stabHitboxDuration = 0.1f;
    public float timeBetweenStabs = 0.15f;
    private int stabsRemaining;

    [Header("Уклонение от урона")]
    [Range(0f, 1f)]
    public float dodgeChance = 0.25f;
    public float dodgeBuffDuration = 5f;
    private bool dodgeBuffActive;

    protected override void Awake()
    {
        base.Awake();
        ability1Cooldown = 7f;
        ability2Cooldown = 12f;
    }

    public override void Attack1()
    {
        PrepareHitbox(0, attack1Damage);
        animator.SetTrigger("Attack1");
    }

    public override void Attack2()
    {
        PrepareHitbox(1, attack2Damage);
        animator.SetTrigger("Attack2");
    }

    protected override void OnAbility1()
    {
        animator.SetTrigger("Ability1");
        stabsRemaining = stabCount;
        InvokeRepeating(nameof(DoStab), 0f, timeBetweenStabs);
    }

    private void DoStab()
    {
        // Способность использует хитбокс [2], но через Invoke (нет Animation Event для серии ударов)
        var hitbox = GetHitbox(2);
        if (hitbox != null)
        {
            hitbox.Activate(stabDamage);
            Invoke(nameof(DeactivateStabHitbox), stabHitboxDuration);
        }

        stabsRemaining--;
        if (stabsRemaining <= 0)
            CancelInvoke(nameof(DoStab));
    }

    private void DeactivateStabHitbox()
    {
        var hitbox = GetHitbox(2);
        if (hitbox != null)
            hitbox.Deactivate();
    }

    protected override void OnAbility2()
    {
        dodgeBuffActive = true;
        animator.SetTrigger("Ability2");
        Invoke(nameof(DeactivateDodgeBuff), dodgeBuffDuration);
    }

    private void DeactivateDodgeBuff()
    {
        dodgeBuffActive = false;
    }

    public bool TryDodgeDamage()
    {
        if (!dodgeBuffActive) return false;
        return Random.value < dodgeChance;
    }
}
