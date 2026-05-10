using UnityEngine;

/// <summary>
/// Универсальный мили-моб. Подходит для всех ближних архетипов:
/// обычный, бронированный, с большим уроном, элитный — отличия задаются через MobData
/// (attackDamages/attackWeights/attackTriggers/canBeInterrupted/maxPoise/...).
/// Триггер аниматора и хитбокс берутся по выбранному индексу атаки.
/// </summary>
public class MeleeMobAI : MobAI
{
    protected override void PerformAttack()
    {
        FaceTarget();

        int attack = ChooseWeightedAttack();
        PrepareHitbox(attack, GetAttackDamage(attack), GetAttackStaggerDamage(attack));

        string trigger = GetAttackTrigger(attack);
        animator.SetTrigger(trigger);
        RpcPlayTrigger(trigger);
    }
}
