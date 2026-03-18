using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;

/// <summary>
/// Экран смерти. Показывается локальному игроку при гибели.
/// В мультиплеере: если все мертвы — Game Over, иначе — спектатор.
/// </summary>
public class DeathScreenUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject deathPanel;
    [SerializeField] private TextMeshProUGUI deathTitle;
    [SerializeField] private TextMeshProUGUI deathSubtitle;
    [SerializeField] private Button returnToLobbyButton;

    private HeroStats localStats;

    private void Awake()
    {
        if (deathPanel != null)
            deathPanel.SetActive(false);
    }

    /// <summary>
    /// Вызывается из PlayerController/PlayerHUD при инициализации локального игрока.
    /// </summary>
    public void Init(HeroStats stats)
    {
        localStats = stats;
        localStats.onDeath.AddListener(OnLocalPlayerDied);
    }

    private void OnDestroy()
    {
        if (localStats != null)
            localStats.onDeath.RemoveListener(OnLocalPlayerDied);
    }

    private void OnLocalPlayerDied()
    {
        if (deathPanel == null) return;

        deathPanel.SetActive(true);

        bool isMultiplayer = NetworkServer.active && NetworkServer.connections.Count > 1;

        if (isMultiplayer)
        {
            // Проверяем, все ли мертвы
            bool allDead = AreAllPlayersDead();

            if (allDead)
            {
                if (deathTitle != null) deathTitle.text = "DEFEAT";
                if (deathSubtitle != null) deathSubtitle.text = "All heroes have fallen...";
                if (returnToLobbyButton != null) returnToLobbyButton.gameObject.SetActive(true);
            }
            else
            {
                if (deathTitle != null) deathTitle.text = "YOU DIED";
                if (deathSubtitle != null) deathSubtitle.text = "Waiting for allies...";
                if (returnToLobbyButton != null) returnToLobbyButton.gameObject.SetActive(false);
            }
        }
        else
        {
            // Одиночная игра
            if (deathTitle != null) deathTitle.text = "YOU DIED";
            if (deathSubtitle != null) deathSubtitle.text = "";
            if (returnToLobbyButton != null) returnToLobbyButton.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Кнопка "Вернуться в лобби".
    /// </summary>
    public void OnReturnToLobbyClicked()
    {
        var netManager = (DungeonKnightNetworkManager)NetworkManager.singleton;
        if (netManager != null)
            netManager.ReturnToLobby();
    }

    private bool AreAllPlayersDead()
    {
        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn?.identity == null) continue;
            var stats = conn.identity.GetComponent<HeroStats>();
            if (stats != null && !stats.IsDead)
                return false;
        }
        return true;
    }
}
