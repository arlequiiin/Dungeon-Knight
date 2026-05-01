using UnityEngine;

public enum RewardRarity { Common = 0, Rare = 1, Epic = 2 }

/// <summary>
/// Описание награды из сундука сокровищницы.
/// Конкретный эффект — отдельный ScriptableObject (RewardEffect-наследник).
/// </summary>
[CreateAssetMenu(fileName = "RewardData", menuName = "Dungeon Knight/Reward")]
public class RewardData : ScriptableObject
{
    [Header("Display")]
    public string rewardName;
    [TextArea(2, 4)]
    public string description;
    public Sprite icon;

    [Header("Pricing")]
    public RewardRarity rarity = RewardRarity.Common;
    [Tooltip("Цена в душах (Common 25, Rare 50, Epic 100)")]
    public int price = 25;

    [Header("Filtering")]
    [Tooltip("Только для героев у которых есть вторая атака на ПКМ (Templar/Swordsman/Soldier/Knight)")]
    public bool requiresMeleeWithSecondAttack;

    [Tooltip("Не выпадает повторно если уже взята (вторая жизнь и т.п.)")]
    public bool unique;

    [Header("Effect")]
    public RewardEffect effect;
}
