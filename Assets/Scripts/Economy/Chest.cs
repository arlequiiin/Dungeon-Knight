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

    [Tooltip("Подсказка \"Нажмите F\" — дочерний объект, включается когда локальный игрок в радиусе.")]
    [SerializeField] private GameObject interactPrompt;

    [Tooltip("Спрайт открытого сундука. Если не задан — используется только тонировка.")]
    [SerializeField] private Sprite openedSprite;

    [SyncVar(hook = nameof(OnOpenedChanged))]
    private bool isOpened;

    public bool IsOpened => isOpened;

    /// <summary>
    /// Может ли этот игрок открыть сундук (с учётом боссовой логики "один раз на игрока").
    /// </summary>
    public bool IsOpenedFor(PlayerController player)
    {
        if (player == null) return isOpened;
        if (!isBossChest) return isOpened;
        // Боссовый сундук — индивидуальный для каждого игрока.
        return openedBy.Contains(player.netId);
    }

    // Для боссового сундука — список netId игроков, которые уже забрали награду.
    // Не SyncVar'ится по сети, т.к. валидация важна только на сервере;
    // на клиенте используется локальный флаг localChosen в ChestInteractor.
    private readonly HashSet<uint> openedBy = new HashSet<uint>();

    // Закэшированные награды для этого сундука — ролл происходит один раз,
    // повторное открытие UI показывает тот же набор (защита от рерола закрытием).
    private List<RewardData> cachedRewards;

    private SpriteRenderer sr;

    /// <summary>
    /// Возвращает закэшированные награды или роллит их через rollFunc на первом обращении.
    /// </summary>
    public List<RewardData> GetOrRollRewards(PlayerController _, System.Func<List<RewardData>> rollFunc)
    {
        if (cachedRewards != null && cachedRewards.Count > 0)
            return cachedRewards;

        cachedRewards = rollFunc?.Invoke();
        return cachedRewards;
    }

    private void Awake()
    {
        sr = GetComponentInChildren<SpriteRenderer>();
        if (interactPrompt != null)
            interactPrompt.SetActive(false);
    }

    private void Update()
    {
        // Только клиент отображает prompt — для локального игрока, в зависимости от расстояния.
        if (interactPrompt == null) return;
        var localPlayer = NetworkClient.localPlayer;
        if (localPlayer == null)
        {
            interactPrompt.SetActive(false);
            return;
        }

        var pc = localPlayer.GetComponent<PlayerController>();
        bool show = pc != null
                    && !IsOpenedFor(pc)
                    && Vector2.Distance(transform.position, localPlayer.transform.position) <= interactRadius;
        if (interactPrompt.activeSelf != show)
            interactPrompt.SetActive(show);
    }

    private void OnOpenedChanged(bool oldVal, bool newVal)
    {
        if (!newVal || sr == null) return;
        if (openedSprite != null)
            sr.sprite = openedSprite;
        else
            sr.color = new Color(0.5f, 0.5f, 0.5f, 1f); // фолбэк — тонировка
    }

    /// <summary>
    /// Серверная попытка открыть сундук игроком.
    /// </summary>
    [Server]
    public void TryOpenServer(PlayerController player, int chosenIndex, RewardData[] offered)
    {
        if (player == null) return;

        // Боссовый сундук — отдельная награда для каждого игрока (если ещё не брал);
        // обычный сундук — один на всех (если уже открыт — отказ).
        if (isBossChest)
        {
            if (openedBy.Contains(player.netId)) return;
        }
        else
        {
            if (isOpened) return;
        }

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

        if (isBossChest)
            openedBy.Add(player.netId);
        else
            isOpened = true;
    }
}
