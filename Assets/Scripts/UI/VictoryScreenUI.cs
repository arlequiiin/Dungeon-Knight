using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;

/// <summary>
/// Экран победы. Показывается сразу при зачистке боссовой комнаты —
/// триггерится из RoomController через статический метод TriggerVictory()
/// (синхронизировано с RoomStateMessage Cleared, приходит всем клиентам одновременно).
/// </summary>
public class VictoryScreenUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private TextMeshProUGUI victoryTitle;
    [SerializeField] private TextMeshProUGUI victorySubtitle;
    [SerializeField] private Button returnToLobbyButton;
    [SerializeField] private EndScreenStats stats;

    private static VictoryScreenUI instance;
    private bool triggered;

    private void Awake()
    {
        instance = this;

        if (victoryPanel != null)
            victoryPanel.SetActive(false);

        if (returnToLobbyButton != null)
            returnToLobbyButton.onClick.AddListener(OnReturnToLobbyClicked);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// <summary>
    /// Вызывается из RoomController при Cleared-событии для боссовой комнаты.
    /// </summary>
    public static void TriggerVictory()
    {
        if (instance == null) return;
        instance.ShowVictory();
    }

    private void ShowVictory()
    {
        if (triggered) return;
        triggered = true;

        if (victoryPanel != null)
            victoryPanel.SetActive(true);

        Time.timeScale = 0f;

        if (victoryTitle != null)
            victoryTitle.text = "Победа!";

        if (victorySubtitle != null)
        {
            string biome = null;
            var netManager = NetworkManager.singleton as DungeonKnightNetworkManager;
            if (netManager != null && netManager.LevelConfig != null)
            {
                biome = !string.IsNullOrEmpty(netManager.LevelConfig.displayName)
                    ? netManager.LevelConfig.displayName
                    : netManager.LevelConfig.name;
            }
            victorySubtitle.text = string.IsNullOrEmpty(biome)
                ? "Локация — зачищена!"
                : $"{biome} — зачищен!";
        }

        if (returnToLobbyButton != null)
            returnToLobbyButton.gameObject.SetActive(NetworkServer.active);

        if (stats != null) stats.Populate();
    }

    private void OnReturnToLobbyClicked()
    {
        Time.timeScale = 1f;
        var netManager = (DungeonKnightNetworkManager)NetworkManager.singleton;
        if (netManager != null)
            netManager.ReturnToLobby();
    }
}
