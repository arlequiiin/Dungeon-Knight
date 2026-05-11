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

        // Если игрок сильно выше/ниже по Y и у моба есть area-атаки — выбираем только из них.
        // (До этого UpdateChase уже не пустил бы нас сюда без выравнивания, если area-атак нет.)
        bool requireArea = !IsYAligned() && HasAnyAreaAttack();
        int attack = ChooseWeightedAttack(requireArea);
        if (attack < 0) attack = 0;

        PrepareHitbox(attack, GetAttackDamage(attack), GetAttackStaggerDamage(attack));

        string trigger = GetAttackTrigger(attack);
        animator.SetTrigger(trigger);
        RpcPlayTrigger(trigger);
    }
}
