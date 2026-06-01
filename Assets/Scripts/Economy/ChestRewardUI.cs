using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI выбора 3 наград из сундука. Создаётся ChestInteractor при взаимодействии.
/// Каждая карточка кликабельна, по клику возвращается индекс выбора.
/// </summary>
public class ChestRewardUI : MonoBehaviour
{
    [Header("Cards")]
    [Tooltip("Кнопки 3 карточек наград (по порядку)")]
    [SerializeField] private Button[] cardButtons;
    [SerializeField] private Image[] cardIcons;
    [SerializeField] private TMP_Text[] cardNames;
    [SerializeField] private TMP_Text[] cardDescriptions;
    [SerializeField] private TMP_Text[] cardPrices;

    [Header("Close")]
    [SerializeField] private Button closeButton;
    [Tooltip("Текст на кнопке закрытия. Если не назначен — заголовок не подменяется.")]
    [SerializeField] private TMP_Text closeButtonLabel;

    private Action<int> onChosen;

    /// <summary>
    /// Открыто ли сейчас какое-либо окно выбора награды. Используется PauseMenuUI,
    /// чтобы ESC закрывал сундук, а не открывал меню паузы поверх него.
    /// </summary>
    public static bool AnyOpen { get; private set; }

    public void Show(RewardData[] rewards, Action<int> callback, string closeLabel = "Close")
    {
        onChosen = callback;
        AnyOpen = true;

        if (closeButtonLabel != null)
            closeButtonLabel.text = closeLabel;

        // НЕ останавливаем время — иначе в кооперативе один игрок откроет сундук
        // и заморозит всю игру у других. Локальная блокировка движения/способностей
        // обеспечена через PlayerController.IsInputBlocked в ChestInteractor.

        for (int i = 0; i < cardButtons.Length; i++)
        {
            int idx = i;
            bool hasReward = i < rewards.Length && rewards[i] != null;

            cardButtons[i].gameObject.SetActive(hasReward);
            if (!hasReward) continue;

            var r = rewards[i];
            if (cardIcons.Length > i) cardIcons[i].sprite = r.icon;
            if (cardNames.Length > i) cardNames[i].text = r.rewardName;
            if (cardDescriptions.Length > i) cardDescriptions[i].text = r.description;
            if (cardPrices.Length > i) cardPrices[i].text = r.price.ToString();

            // Подсветка цены если не хватает
            if (cardPrices.Length > i)
                cardPrices[i].color = CurrencyManager.CanAffordRun(r.price) ? Color.white : Color.red;

            cardButtons[i].interactable = CurrencyManager.CanAffordRun(r.price);
            cardButtons[i].onClick.RemoveAllListeners();
            cardButtons[i].onClick.AddListener(() => Choose(idx));
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() => Choose(-1));
        }
    }

    private void Update()
    {
        // ESC закрывает окно награды (эквивалент кнопки «Закрыть»). Меню паузы при этом
        // не открывается — PauseMenuUI проверяет AnyOpen.
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
            Choose(-1);
    }

    private void Choose(int index)
    {
        AnyOpen = false;
        onChosen?.Invoke(index);
    }

    private void OnDestroy()
    {
        // Подстраховка: если объект уничтожили в обход Choose (например при смене сцены),
        // флаг не должен остаться «залипшим».
        AnyOpen = false;
    }
}
