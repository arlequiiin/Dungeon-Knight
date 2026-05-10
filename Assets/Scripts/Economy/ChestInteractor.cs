using Mirror;
using UnityEngine;

/// <summary>
/// На локальном игроке. Слушает кнопку Interaction (E),
/// ищет ближайший сундук, открывает UI выбора наград.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class ChestInteractor : NetworkBehaviour
{
    [Tooltip("Префаб UI выбора наград (с компонентом ChestRewardUI)")]
    [SerializeField] private GameObject chestUIPrefab;

    private PlayerController player;
    private ChestRewardUI activeUI;

    // Локальный список сундуков, по которым этот клиент уже сделал выбор.
    // Нужен потому что серверный openedBy для боссового сундука не SyncVar'ится.
    private readonly System.Collections.Generic.HashSet<uint> locallyClosed = new();

    private void Awake()
    {
        player = GetComponent<PlayerController>();
    }

    private void OnEnable()
    {
        if (player != null)
            player.onInteract += OnInteract;
    }

    private void OnDisable()
    {
        if (player != null)
            player.onInteract -= OnInteract;
    }

    private void OnInteract()
    {
        if (!player.isLocalPlayer) return;
        if (activeUI != null) return; // уже открыт UI

        var chest = FindNearestChest();
        if (chest == null) return;

        // Локальный кэш "я уже открывал этот сундук" — для per-player режима
        // (boss + обычный с perPlayer=true), т.к. серверный openedBy не SyncVar'ится.
        if (locallyClosed.Contains(chest.netId)) return;

        // Legacy-режим (perPlayer=false, не boss) — общий сундук, проверяем глобальный isOpened.
        if (!chest.isBossChest && !chest.perPlayer && chest.IsOpened) return;

        TryOpenChest(chest);
    }

    private Chest FindNearestChest()
    {
        Chest best = null;
        float bestDist = float.MaxValue;

        foreach (var c in FindObjectsByType<Chest>(FindObjectsSortMode.None))
        {
            if (c == null) continue;
            float d = Vector2.Distance(transform.position, c.transform.position);
            if (d > c.interactRadius) continue;
            if (d < bestDist)
            {
                bestDist = d;
                best = c;
            }
        }

        return best;
    }

    private void TryOpenChest(Chest chest)
    {
        if (chestUIPrefab == null) return;
        if (chest.rewardPool == null) return;

        // Награды роллятся ОДИН РАЗ на сундук и кэшируются — иначе игрок может
        // закрывать/переоткрывать UI до тех пор, пока не выпадет нужное.
        // Сид детерминированный по netId сундука, чтобы один и тот же сундук
        // на всех клиентах показывал одинаковый набор.
        var rewards = chest.GetOrRollRewards(player, () =>
        {
            int seed = unchecked((int)chest.netId) * 73856093;
            var rng = new System.Random(seed);
            var mods = GetComponent<RunModifiers>();
            var heroType = player.heroData != null ? player.heroData.heroType : HeroType.None;
            return chest.rewardPool.RollChestRewards(chest.isBossChest, heroType, mods, rng);
        });

        if (rewards == null || rewards.Count == 0) return;

        // Создаём UI
        var uiObj = Instantiate(chestUIPrefab);
        activeUI = uiObj.GetComponent<ChestRewardUI>();
        if (activeUI == null)
        {
            Destroy(uiObj);
            return;
        }

        // Блокируем управление и способности игрока, пока открыто UI выбора награды
        player.IsInputBlocked = true;

        // Боссовый сундук — кнопка закрытия пишет "Continue", обычный — "Close".
        string closeLabel = chest.isBossChest ? "Continue" : "Close";
        activeUI.Show(rewards.ToArray(), chosenIndex => OnRewardChosen(chest, rewards.ToArray(), chosenIndex), closeLabel);
    }

    private void OnRewardChosen(Chest chest, RewardData[] offered, int idx)
    {
        bool rewardApplied = false;
        if (idx >= 0)
        {
            var reward = offered[idx];
            if (CurrencyManager.TrySpendRun(reward.price))
            {
                CmdApplyReward(chest.netIdentity, idx, GetRewardIds(offered));
                rewardApplied = true;
            }
        }

        if (activeUI != null)
        {
            Destroy(activeUI.gameObject);
            activeUI = null;
        }

        // Обычный сундук: помечаем закрытым ТОЛЬКО если игрок реально взял награду.
        // Если закрыл без выбора или денег не хватило — может вернуться позже.
        if (chest != null && !chest.isBossChest && chest.perPlayer && rewardApplied)
        {
            locallyClosed.Add(chest.netId);
            chest.MarkLocallyOpened();
        }

        // Боссовый сундук: всегда закрываем (Continue = «я готов идти дальше», без возврата),
        // и уведомляем сервер о готовности для переходного экрана.
        if (chest != null && chest.isBossChest)
        {
            locallyClosed.Add(chest.netId);
            chest.MarkLocallyOpened();
            BossRewardCoordinator.NotifyLocalPlayerDone();
        }

        // Снимаем блокировку управления — UI закрыто, игрок снова может двигаться/атаковать
        if (player != null)
            player.IsInputBlocked = false;
    }

    /// <summary>
    /// Передаём имена наград чтобы сервер мог найти их в RewardPool и валидировать выбор.
    /// </summary>
    private string[] GetRewardIds(RewardData[] rewards)
    {
        var ids = new string[rewards.Length];
        for (int i = 0; i < rewards.Length; i++)
            ids[i] = rewards[i] != null ? rewards[i].name : "";
        return ids;
    }

    [Command]
    private void CmdApplyReward(NetworkIdentity chestId, int chosenIndex, string[] rewardIds)
    {
        if (chestId == null) return;
        var chest = chestId.GetComponent<Chest>();
        if (chest == null) return;
        if (chest.rewardPool == null) return;
        // В per-player режиме (boss + обычный с perPlayer=true) сервер сам проверяет
        // в TryOpenServer через openedBy. В legacy-режиме блокируем тут.
        if (!chest.isBossChest && !chest.perPlayer && chest.IsOpened) return;

        // Восстанавливаем массив RewardData по именам — серверная сторона проверяет что они в пуле
        var offered = new RewardData[rewardIds.Length];
        for (int i = 0; i < rewardIds.Length; i++)
        {
            offered[i] = FindRewardByName(chest.rewardPool, rewardIds[i]);
        }

        chest.TryOpenServer(player, chosenIndex, offered);
    }

    private RewardData FindRewardByName(RewardPool pool, string name)
    {
        if (pool == null || pool.allRewards == null) return null;
        foreach (var r in pool.allRewards)
            if (r != null && r.name == name) return r;
        return null;
    }
}
