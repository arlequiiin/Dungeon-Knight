using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;

/// <summary>
/// Экран "Game Over". Показывается когда все игроки упали.
/// Компонент сидит на префабе, который инстанцируется в PlayerController.OnStartLocalPlayer.
/// Панель скрыта по умолчанию. GameOverUI.Show() включает её.
/// </summary>
public class GameOverUI : MonoBehaviour
{
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI title;
    [SerializeField] private TextMeshProUGUI subtitle;
    [SerializeField] private Button returnToLobbyButton;

    private static GameOverUI instance;

    private void Awake()
    {
        instance = this;
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        if (returnToLobbyButton != null)
            returnToLobbyButton.onClick.AddListener(OnReturnToLobbyClicked);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    public static void Show()
    {
        if (instance == null) return;
        instance.DoShow();
    }

    private void DoShow()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        if (title != null)
            title.text = "DEFEATED";

        if (subtitle != null)
            subtitle.text = "The party has fallen...";

        // Кнопка возврата — только у хоста
        if (returnToLobbyButton != null)
            returnToLobbyButton.gameObject.SetActive(NetworkServer.active);
    }

    private void OnReturnToLobbyClicked()
    {
        var netManager = (DungeonKnightNetworkManager)NetworkManager.singleton;
        if (netManager != null)
            netManager.ReturnToLobby();
    }
}
