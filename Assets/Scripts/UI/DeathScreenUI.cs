using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;

/// <summary>
/// Универсальный экран смерти/поражения.
/// — Локальный игрок упал и союзники живы → "YOU ARE DOWN" (спектатор, ждём revive)
/// — Все упали → "DEFEATED" + кнопка возврата (только хост)
/// — В соло смерть = поражение сразу
/// Подписывается на DeathScreenUI.ShowGameOver() через статику (вызывается из NetworkManager).
/// </summary>
public class DeathScreenUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject deathPanel;
    [SerializeField] private TextMeshProUGUI deathTitle;
    [SerializeField] private TextMeshProUGUI deathSubtitle;
    [SerializeField] private Button returnToLobbyButton;

    private HeroStats localStats;
    private bool gameOverShown;

    private static DeathScreenUI instance;

    private void Awake()
    {
        instance = this;
        if (deathPanel != null)
            deathPanel.SetActive(false);

        if (returnToLobbyButton != null)
            returnToLobbyButton.onClick.AddListener(OnReturnToLobbyClicked);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;

        if (localStats != null)
        {
            localStats.onDowned.RemoveListener(OnLocalPlayerDowned);
            localStats.onRevived.RemoveListener(OnLocalPlayerRevived);
        }
    }

    public void Init(HeroStats stats)
    {
        localStats = stats;
        localStats.onDowned.AddListener(OnLocalPlayerDowned);
        localStats.onRevived.AddListener(OnLocalPlayerRevived);
    }

    /// <summary>
    /// Вызывается из NetworkManager при получении GameOverMessage.
    /// </summary>
    public static void ShowGameOver()
    {
        if (instance == null) return;
        instance.DoShowGameOver();
    }

    private void OnLocalPlayerDowned()
    {
        if (gameOverShown) return;
        if (deathPanel == null) return;

        deathPanel.SetActive(true);

        if (deathTitle != null) deathTitle.text = "YOU ARE DOWN";
        if (deathSubtitle != null) deathSubtitle.text = "Waiting for allies to revive you...";
        if (returnToLobbyButton != null) returnToLobbyButton.gameObject.SetActive(false);
    }

    private void OnLocalPlayerRevived()
    {
        if (gameOverShown) return;
        if (deathPanel != null)
            deathPanel.SetActive(false);
    }

    private void DoShowGameOver()
    {
        gameOverShown = true;
        if (deathPanel == null) return;

        deathPanel.SetActive(true);

        if (deathTitle != null) deathTitle.text = "DEFEATED";
        if (deathSubtitle != null) deathSubtitle.text = "The party has fallen...";

        // Кнопка возврата — только у хоста
        if (returnToLobbyButton != null)
            returnToLobbyButton.gameObject.SetActive(NetworkServer.active);
    }

    private void OnReturnToLobbyClicked()
    {
        if (returnToLobbyButton != null)
            returnToLobbyButton.interactable = false;

        var netManager = (DungeonKnightNetworkManager)NetworkManager.singleton;
        if (netManager != null)
            netManager.ReturnToLobby();
    }
}
