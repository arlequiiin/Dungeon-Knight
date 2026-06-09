using UnityEngine;

/// <summary>
/// Алтарь «казино» в лобби. Игрок подходит в зону триггера, жмёт F — тратит мета-валюту
/// и получает случайный временный бафф на следующий забег (см. CasinoManager).
/// Результат показывается уведомлением HUD — отдельного UI нет (минимальная реализация).
/// Требует CircleCollider2D (isTrigger) на этом же объекте.
/// </summary>
[RequireComponent(typeof(CircleCollider2D))]
public class CasinoAltar : MonoBehaviour
{
    [Tooltip("\"Press F\" подсказка (опционально)")]
    [SerializeField] private GameObject interactPrompt;

    [Tooltip("Длительность уведомления о результате спина.")]
    [SerializeField] private float notificationDuration = 3.5f;

    [Tooltip("Размер шрифта уведомления о результате (чуть меньше обычных уведомлений, " +
             "т.к. текст в две строки). 0 = размер по умолчанию из HUD.")]
    [SerializeField] private float notificationFontSize = 40f;

    [Tooltip("Вертикальное смещение уведомления (выше базовой позиции). Только для этого уведомления.")]
    [SerializeField] private float notificationYOffset = 120f;

    private PlayerController localPlayer;

    private void OnTriggerEnter2D(Collider2D other)
    {
        var pc = other.GetComponent<PlayerController>();
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
    }

    private void OnDisable()
    {
        if (localPlayer != null)
        {
            localPlayer.onInteract -= OnPlayerInteract;
            localPlayer = null;
        }
    }

    private void OnPlayerInteract()
    {
        if (CasinoManager.TrySpin(out var deal))
        {
            // Сделка: бафф на одну характеристику + дебафф на другую. Не нравится — перекрути.
            // Перенос строки после второго модификатора + явные подписи, чтобы дебафф
            // не читался как второй бафф.
            Notify($"Бафф: {CasinoManager.DescribeModifier(deal.buff.type, deal.buff.magnitude)}\n" +
                   $"Дебафф: {CasinoManager.DescribeModifier(deal.debuff.type, deal.debuff.magnitude)}\n");
        }
        else
        {
            Notify($"Недостаточно душ (нужно {CasinoManager.SpinCost})");
        }
    }

    private void Notify(string text)
    {
        if (PlayerHUD.LocalInstance != null)
            PlayerHUD.LocalInstance.ShowNotification(text, notificationDuration, notificationFontSize, notificationYOffset);
    }
}
