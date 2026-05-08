using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;

public class MainMenuUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject titlePanel;
    [SerializeField] private GameObject playPanel;
    [SerializeField] private GameObject joinPanel;

    [Header("Join")]
    [SerializeField] private TMP_InputField ipInputField;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Version")]
    [SerializeField] private TextMeshProUGUI versionText;

    [Header("Network")]
    [SerializeField] private DungeonKnightNetworkManager networkManager;

    [Header("Settings")]
    [SerializeField] private SettingsUI settingsUI;

    private const string DefaultIP = "localhost";

    private void Start()
    {
        ShowTitlePanel();

        if (versionText != null)
            versionText.text = "v" + Application.version;
    }

    // ── Title Panel ──

    public void ShowTitlePanel()
    {
        titlePanel.SetActive(true);
        playPanel.SetActive(false);
        joinPanel.SetActive(false);
        ClearStatus();
    }

    // ── Play Panel ──

    public void ShowPlayPanel()
    {
        titlePanel.SetActive(false);
        playPanel.SetActive(true);
        joinPanel.SetActive(false);
    }

    public void OnHostClicked()
    {
        SetStatus("Creating server...");
        networkManager.maxConnections = 3;
        networkManager.StartHost();
    }

    public void OnSoloClicked()
    {
        SetStatus("Starting solo game...");
        networkManager.maxConnections = 1;
        networkManager.StartHost();
    }

    // ── Join Panel ──

    public void ShowJoinPanel()
    {
        titlePanel.SetActive(false);
        playPanel.SetActive(false);
        joinPanel.SetActive(true);

        if (ipInputField != null)
            ipInputField.text = DefaultIP;

        ClearStatus();
    }

    public void OnConnectClicked()
    {
        string ip = ipInputField != null ? ipInputField.text.Trim() : DefaultIP;

        if (string.IsNullOrEmpty(ip))
        {
            SetStatus("Enter IP address!");
            return;
        }

        SetStatus("Connecting to " + ip + "...");
        networkManager.networkAddress = ip;
        networkManager.StartClient();
    }

    public void OnBackToPlay()
    {
        ShowPlayPanel();
    }

    public void OnBackToTitle()
    {
        ShowTitlePanel();
    }

    // ── Quit ──

    public void OnSettingsClicked()
    {
        if (settingsUI != null)
            settingsUI.Open();
    }

    public void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ── Status ──

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    private void ClearStatus()
    {
        if (statusText != null)
            statusText.text = "";
    }

    /// <summary>
    /// Called from NetworkManager on disconnect — shows reason to the player.
    /// </summary>
    public void ShowDisconnectMessage(string message)
    {
        // Return to join panel so the player sees the status
        ShowJoinPanel();
        SetStatus(message);
    }
}
