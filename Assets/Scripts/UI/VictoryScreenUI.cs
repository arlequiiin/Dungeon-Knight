using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;

/// <summary>
/// Victory screen. Shown to all players when the boss dies.
/// Server detects boss death and notifies all clients via ClientRpc on DungeonKnightNetworkManager.
/// This component polls for boss death (simpler than event wiring across network).
/// </summary>
public class VictoryScreenUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private TextMeshProUGUI victoryTitle;
    [SerializeField] private TextMeshProUGUI victorySubtitle;
    [SerializeField] private Button returnToLobbyButton;

    private bool triggered;
    private MobHealth bossHealth;
    private bool searching = true;

    private void Awake()
    {
        if (victoryPanel != null)
            victoryPanel.SetActive(false);

        if (returnToLobbyButton != null)
            returnToLobbyButton.onClick.AddListener(OnReturnToLobbyClicked);
    }

    private void Update()
    {
        if (triggered) return;

        // Find boss
        if (searching)
        {
            foreach (var mh in FindObjectsByType<MobHealth>(FindObjectsSortMode.None))
            {
                if (mh.IsBoss)
                {
                    bossHealth = mh;
                    searching = false;
                    break;
                }
            }
            return;
        }

        // Boss found — check if dead
        if (bossHealth == null || bossHealth.IsDead)
        {
            ShowVictory();
        }
    }

    private void ShowVictory()
    {
        triggered = true;

        if (victoryPanel != null)
            victoryPanel.SetActive(true);

        Time.timeScale = 0f;

        if (victoryTitle != null)
            victoryTitle.text = "VICTORY";

        if (victorySubtitle != null)
            victorySubtitle.text = "The Undead Crypt has been cleansed!";

        // Only show return button to host
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
