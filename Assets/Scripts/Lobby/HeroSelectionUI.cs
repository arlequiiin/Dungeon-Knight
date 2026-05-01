using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI панель выбора героя. Создаёт карточки героев динамически.
/// Показывает занятых героев серым, текущий выбор — подсвеченным.
/// </summary>
public class HeroSelectionUI : MonoBehaviour
{
    [Header("Контейнер карточек")]
    [SerializeField] private Transform cardContainer;

    [Header("Кнопки")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button readyButton;
    [SerializeField] private TMP_Text readyButtonText;

    [Header("Префаб карточки")]
    [SerializeField] private GameObject heroCardPrefab;

    [Header("Валюта")]
    [SerializeField] private TMP_Text coinText;

    [Header("Цвета")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(0.4f, 1f, 0.4f);
    [SerializeField] private Color takenColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
    [SerializeField] private Color currentHeroColor = new Color(0.4f, 0.7f, 1f);
    [SerializeField] private Color lockedColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);

    private HeroType? pendingSelection;
    private HeroCardSlot[] cardSlots;
    private bool isOpen;

    public bool IsOpen => isOpen;

    public void Open()
    {
        gameObject.SetActive(true);
        isOpen = true;
        pendingSelection = null;

        if (LobbyManager.Instance != null)
            LobbyManager.Instance.OnSelectionsUpdated += RefreshCards;

        CurrencyManager.OnCoinsChanged += OnCoinsChanged;
        HeroUnlockManager.OnUnlocksChanged += RefreshCards;

        UpdateCoinText();
        BuildCards();
        RefreshCards();

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);
        if (readyButton != null)
            readyButton.onClick.AddListener(OnReadyClicked);
    }

    public void Close()
    {
        if (LobbyManager.Instance != null)
            LobbyManager.Instance.OnSelectionsUpdated -= RefreshCards;

        CurrencyManager.OnCoinsChanged -= OnCoinsChanged;
        HeroUnlockManager.OnUnlocksChanged -= RefreshCards;

        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(OnConfirmClicked);
        if (readyButton != null)
            readyButton.onClick.RemoveListener(OnReadyClicked);

        isOpen = false;
        gameObject.SetActive(false);
    }

    private void OnCoinsChanged(int amount)
    {
        UpdateCoinText();
        RefreshCards();
    }

    private void UpdateCoinText()
    {
        if (coinText != null)
            coinText.text = CurrencyManager.Coins.ToString();
    }

    private void BuildCards()
    {
        // Удаляем старые карточки
        if (cardSlots != null)
        {
            foreach (var slot in cardSlots)
                if (slot.Root != null) Destroy(slot.Root);
        }

        var lobby = LobbyManager.Instance;
        if (lobby == null || lobby.AllHeroes == null) return;

        var heroes = lobby.AllHeroes;
        cardSlots = new HeroCardSlot[heroes.Length];

        for (int i = 0; i < heroes.Length; i++)
        {
            var hero = heroes[i];
            var cardObj = Instantiate(heroCardPrefab, cardContainer);

            var slot = new HeroCardSlot
            {
                Root = cardObj,
                HeroType = hero.heroType,
                Data = hero,
                Icon = cardObj.transform.Find("Icon")?.GetComponent<Image>(),
                NameText = cardObj.transform.Find("Name")?.GetComponent<TMP_Text>(),
                PriceText = cardObj.transform.Find("Price")?.GetComponent<TMP_Text>(),
                LockOverlay = cardObj.transform.Find("Lock")?.gameObject,
                Background = cardObj.GetComponent<Image>(),
                Button = cardObj.GetComponent<Button>()
            };

            if (slot.Icon != null && hero.icon != null)
            {
                slot.Icon.sprite = hero.icon;
                slot.Icon.preserveAspect = true;
            }

            if (slot.NameText != null)
                slot.NameText.text = hero.heroName;

            // Захватываем значение для замыкания
            var heroData = hero;
            if (slot.Button != null)
                slot.Button.onClick.AddListener(() => OnCardClicked(heroData));

            cardSlots[i] = slot;
        }
    }

    private void RefreshCards()
    {
        if (cardSlots == null) return;

        var lobby = LobbyManager.Instance;
        if (lobby == null) return;

        var currentHero = lobby.GetLocalPlayerHero();
        bool localReady = lobby.IsLocalPlayerReady();

        foreach (var slot in cardSlots)
        {
            if (slot.Background == null) continue;

            bool taken = lobby.IsHeroTaken(slot.HeroType);
            bool isCurrentHero = currentHero.HasValue && currentHero.Value == slot.HeroType;
            bool isPending = pendingSelection.HasValue && pendingSelection.Value == slot.HeroType;
            bool unlocked = HeroUnlockManager.IsUnlocked(slot.Data);

            // Показ замочка и цены
            if (slot.LockOverlay != null) slot.LockOverlay.SetActive(!unlocked);
            if (slot.PriceText != null)
            {
                if (unlocked)
                {
                    slot.PriceText.gameObject.SetActive(false);
                }
                else
                {
                    int cost = HeroUnlockManager.GetUnlockCost(slot.Data);
                    slot.PriceText.gameObject.SetActive(true);
                    slot.PriceText.text = $"{cost} душ";
                    slot.PriceText.color = CurrencyManager.CanAfford(cost) ? Color.white : Color.red;
                }
            }

            if (!unlocked)
            {
                slot.Background.color = lockedColor;
                if (slot.Button != null) slot.Button.interactable = true; // клик = попытка купить
            }
            else if (taken)
            {
                slot.Background.color = takenColor;
                if (slot.Button != null) slot.Button.interactable = false;
            }
            else if (isPending)
            {
                slot.Background.color = selectedColor;
                if (slot.Button != null) slot.Button.interactable = true;
            }
            else if (isCurrentHero)
            {
                slot.Background.color = currentHeroColor;
                if (slot.Button != null) slot.Button.interactable = true;
            }
            else
            {
                slot.Background.color = normalColor;
                if (slot.Button != null) slot.Button.interactable = true;
            }
        }

        // Кнопка Confirm активна только если есть pending выбор
        if (confirmButton != null)
            confirmButton.interactable = pendingSelection.HasValue;

        // Текст кнопки Ready
        if (readyButtonText != null)
            readyButtonText.text = localReady ? "Not Ready" : "Ready";
    }


    private void OnCardClicked(HeroData data)
    {
        if (data == null) return;

        // Если герой залочен — попытка купить
        if (!HeroUnlockManager.IsUnlocked(data))
        {
            HeroUnlockManager.TryUnlock(data);
            return;
        }

        var lobby = LobbyManager.Instance;
        if (lobby != null && lobby.IsHeroTaken(data.heroType)) return;

        pendingSelection = data.heroType;
        RefreshCards();
    }

    private void OnConfirmClicked()
    {
        if (!pendingSelection.HasValue) return;

        // Защита: нельзя подтвердить выбор залоченного героя
        var lobby = LobbyManager.Instance;
        if (lobby != null)
        {
            foreach (var h in lobby.AllHeroes)
            {
                if (h != null && h.heroType == pendingSelection.Value && !HeroUnlockManager.IsUnlocked(h))
                    return;
            }
        }

        NetworkClient.Send(new HeroSelectRequest { heroType = pendingSelection.Value });
        pendingSelection = null;
        RefreshCards();
    }

    private void OnReadyClicked()
    {
        NetworkClient.Send(new PlayerReadyMessage());
    }

    private class HeroCardSlot
    {
        public GameObject Root;
        public HeroType HeroType;
        public HeroData Data;
        public Image Icon;
        public TMP_Text NameText;
        public TMP_Text PriceText;
        public GameObject LockOverlay;
        public Image Background;
        public Button Button;
    }
}
