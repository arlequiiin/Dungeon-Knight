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
            victoryTitle.text = "VICTORY";

        if (victorySubtitle != null)
            victorySubtitle.text = "The Undead Crypt has been cleansed!";

        if (returnToLobbyButton != null)
            returnToLobbyButton.gameObject.SetActive(NetworkServer.active);
    }

    private void OnReturnToLobbyClicked()
    {
        Time.timeScale = 1f;
        var netManager = (DungeonKnightNetworkManager)NetworkManager.singleton;
        if (netManager != null)
            netManager.ReturnToLobby();
    }
}
