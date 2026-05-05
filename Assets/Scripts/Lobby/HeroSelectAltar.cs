using UnityEngine;

/// <summary>
/// Алтарь выбора героя в лобби.
/// Игрок подходит в зону триггера, нажимает F — открывается UI выбора героя.
/// Требует CircleCollider2D (isTrigger) на этом же объекте.
/// </summary>
[RequireComponent(typeof(CircleCollider2D))]
public class HeroSelectAltar : MonoBehaviour
{
    [SerializeField] private HeroSelectionUI selectionUI;
    [SerializeField] private GameObject interactPrompt; // "Press F" подсказка (опционально)

    private PlayerController localPlayer;

    private void OnTriggerEnter2D(Collider2D other)
    {
        var pc = other.GetComponent<PlayerController>();
        Debug.Log($"[Altar] OnTriggerEnter2D other={other.name} pc={pc != null} isLocal={(pc != null && pc.isLocalPlayer)}");
        if (pc == null || !pc.isLocalPlayer) return;

        localPlayer = pc;
        localPlayer.onInteract += OnPlayerInteract;

        if (interactPrompt != null)
            interactPrompt.SetActive(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var pc = other.GetComponent<PlayerController>();
        if (pc == null || pc != localPlayer) return;

        if (localPlayer != null)
            localPlayer.onInteract -= OnPlayerInteract;

        localPlayer = null;

        if (interactPrompt != null)
            interactPrompt.SetActive(false);

        if (selectionUI != null)
            selectionUI.Close();
    }

    private void OnPlayerInteract()
    {
        Debug.Log($"[Altar] OnPlayerInteract uiNull={selectionUI == null} isOpen={(selectionUI != null && selectionUI.IsOpen)}");
        if (selectionUI == null) return;

        if (selectionUI.IsOpen)
            selectionUI.Close();
        else
            selectionUI.Open();
    }

    private void OnDisable()
    {
        if (localPlayer != null)
        {
            localPlayer.onInteract -= OnPlayerInteract;
            localPlayer = null;
        }
    }
}
