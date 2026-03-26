using UnityEngine;
using Mirror;

/// <summary>
/// Меню паузы (Escape). Показывает кнопки "Продолжить" и "Выйти в меню".
/// Работает только в данже (не в лобби).
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;

    private bool isPaused;

    private bool IsSolo =>
        !NetworkServer.active || NetworkServer.connections.Count <= 1;

    private void Awake()
    {
        if (pausePanel != null)
            pausePanel.SetActive(false);
    }

    private void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Toggle();
        }
    }

    public void Toggle()
    {
        isPaused = !isPaused;

        if (pausePanel != null)
            pausePanel.SetActive(isPaused);

        if (IsSolo)
            Time.timeScale = isPaused ? 0f : 1f;
    }

    public void OnResumeClicked()
    {
        isPaused = false;
        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (IsSolo)
            Time.timeScale = 1f;
    }

    public void OnReturnToMenuClicked()
    {
        isPaused = false;
        if (pausePanel != null)
            pausePanel.SetActive(false);

        Time.timeScale = 1f;

        var netManager = (DungeonKnightNetworkManager)NetworkManager.singleton;
        if (netManager != null)
            netManager.ReturnToMainMenu();
    }
}
