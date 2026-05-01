using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// Сундук в комнате-сокровищнице.
/// Локальный игрок взаимодействует (E), открывается UI выбора 3 наград.
/// Награды применяются на сервере через Cmd на PlayerController.
/// Сундук одноразовый: после первого открытия — disabled.
/// </summary>
public class Chest : NetworkBehaviour
{
    [Tooltip("Пул наград — общий ScriptableObject для всех сундуков")]
    public RewardPool rewardPool;

    [Tooltip("Боссовый сундук? (2 Rare + 1 Epic вместо 2 Common + 1 Rare)")]
    public bool isBossChest;

    [Tooltip("Радиус взаимодействия")]
    public float interactRadius = 1.5f;

    [SyncVar(hook = nameof(OnOpenedChanged))]
    private bool isOpened;

    public bool IsOpened => isOpened;

    private SpriteRenderer sr;

    private void Awake()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
    }

    private void OnOpenedChanged(bool oldVal, bool newVal)
    {
        if (newVal && sr != null)
            sr.color = new Color(0.5f, 0.5f, 0.5f, 1f); // потускнел после открытия
    }

    /// <summary>
    /// Серверная попытка открыть сундук игроком.
    /// </summary>
    [Server]
    public void TryOpenServer(PlayerController player, int chosenIndex, RewardData[] offered)
    {
        if (isOpened) return;
        if (player == null) return;

        // Проверка дистанции
        float dist = Vector2.Distance(transform.position, player.transform.position);
        if (dist > interactRadius * 2f) return;

        // Валидация выбора
        if (chosenIndex < 0 || chosenIndex >= offered.Length) return;
        var reward = offered[chosenIndex];
        if (reward == null || reward.effect == null) return;

        // Применяем эффект на сервере
        var stats = player.GetComponent<HeroStats>();
        var mods = player.GetComponent<RunModifiers>();
        reward.effect.Apply(stats, mods);

        isOpened = true;
    }
}
