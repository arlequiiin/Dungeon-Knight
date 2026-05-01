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
        if (chest.IsOpened) return;

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

        // Roll наград локально с детерминированным сидом (id сундука + currency state)
        // — server валидирует выбор, не пул.
        var rng = new System.Random(chest.GetInstanceID() ^ Time.frameCount);
        var mods = GetComponent<RunModifiers>();
        var heroType = player.heroData != null ? player.heroData.heroType : HeroType.None;
        var rewards = chest.rewardPool.RollChestRewards(chest.isBossChest, heroType, mods, rng);

        if (rewards == null || rewards.Count == 0) return;

        // Создаём UI
        var uiObj = Instantiate(chestUIPrefab);
        activeUI = uiObj.GetComponent<ChestRewardUI>();
        if (activeUI == null)
        {
            Destroy(uiObj);
            return;
        }

        activeUI.Show(rewards.ToArray(), chosenIndex => OnRewardChosen(chest, rewards.ToArray(), chosenIndex));
    }

    private void OnRewardChosen(Chest chest, RewardData[] offered, int idx)
    {
        if (idx >= 0)
        {
            var reward = offered[idx];
            if (CurrencyManager.TrySpend(reward.price))
            {
                CmdApplyReward(chest.netIdentity, idx, GetRewardIds(offered));
            }
        }

        if (activeUI != null)
        {
            Destroy(activeUI.gameObject);
            activeUI = null;
        }
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
        if (chest == null || chest.IsOpened) return;
        if (chest.rewardPool == null) return;

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
