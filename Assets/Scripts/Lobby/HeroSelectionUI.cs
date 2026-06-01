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

    [Header("Кнопка действия")]
    [Tooltip("Единая контекстная кнопка: «Купить» для залоченного героя, иначе «Готов»/«Не готов». " +
             "Сам выбор героя происходит по клику на карточку.")]
    [SerializeField] private Button actionButton;
    [SerializeField] private TMP_Text actionButtonText;

    [Header("Плашка информации")]
    [Tooltip("Корень плашки. Скрывается, если ничего не выбрано.")]
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private Image infoIcon;
    [SerializeField] private TMP_Text infoName;
    [SerializeField] private TMP_Text infoDescription;
    [SerializeField] private TMP_Text infoHealth;
    [SerializeField] private TMP_Text infoEnergy;
    [SerializeField] private TMP_Text infoDamage;
    [Tooltip("Опционально — текст урона по устойчивости/доп. защиты. Можно оставить пустым.")]
    [SerializeField] private TMP_Text infoExtra;
    [SerializeField] private Image infoAbilityIcon;
    [SerializeField] private TMP_Text infoAbilityName;
    [SerializeField] private TMP_Text infoAbilityDescription;

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

        // По умолчанию активируем в плашке текущего выбранного героя, если он есть.
        var currentHero = LobbyManager.Instance != null ? LobbyManager.Instance.GetLocalPlayerHero() : null;
        pendingSelection = currentHero;

        if (PlayerHUD.LocalInstance != null)
            PlayerHUD.LocalInstance.SetTopLeftPanelVisible(false);

        // Прячем активную туториал-подсказку, чтобы она не перекрывала окно выбора героя.
        if (TutorialHintUI.Instance != null)
            TutorialHintUI.Instance.Hide();

        if (LobbyManager.Instance != null)
            LobbyManager.Instance.OnSelectionsUpdated += RefreshCards;

        CurrencyManager.OnMetaCoinsChanged += OnCoinsChanged;
        HeroUnlockManager.OnUnlocksChanged += RefreshCards;

        UpdateCoinText();
        BuildCards();
        RefreshCards();

        if (actionButton != null)
            actionButton.onClick.AddListener(OnActionClicked);
    }

    public void Close()
    {
        if (PlayerHUD.LocalInstance != null)
            PlayerHUD.LocalInstance.SetTopLeftPanelVisible(true);

        if (LobbyManager.Instance != null)
            LobbyManager.Instance.OnSelectionsUpdated -= RefreshCards;

        CurrencyManager.OnMetaCoinsChanged -= OnCoinsChanged;
        HeroUnlockManager.OnUnlocksChanged -= RefreshCards;

        if (actionButton != null)
            actionButton.onClick.RemoveListener(OnActionClicked);

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
            coinText.text = CurrencyManager.MetaCoins.ToString();
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
                    slot.PriceText.text = cost.ToString();
                    slot.PriceText.color = CurrencyManager.CanAffordMeta(cost) ? Color.white : Color.red;
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

        UpdateActionButton(currentHero, localReady);
        UpdateInfoPanel();
    }

    /// <summary>
    /// Единая контекстная кнопка. Выбор героя теперь делается кликом по карточке,
    /// поэтому кнопке остаётся два режима:
    ///   • залоченный герой в плашке → «Купить (cost)»;
    ///   • иначе → «Готов» / «Не готов» (toggle готовности).
    /// </summary>
    private void UpdateActionButton(HeroType? currentHero, bool localReady)
    {
        if (actionButton == null) return;

        var pendingData = FindHeroData(pendingSelection);
        bool pendingLocked = pendingData != null && !HeroUnlockManager.IsUnlocked(pendingData);

        if (pendingLocked)
        {
            int cost = HeroUnlockManager.GetUnlockCost(pendingData);
            if (actionButtonText != null) actionButtonText.text = $"Купить ({cost})";
            actionButton.interactable = CurrencyManager.CanAffordMeta(cost);
            return;
        }

        // Готовым можно стать только выбрав героя.
        if (actionButtonText != null) actionButtonText.text = localReady ? "Не готов" : "Готов";
        actionButton.interactable = currentHero.HasValue;
    }

    private void UpdateInfoPanel()
    {
        var data = FindHeroData(pendingSelection);

        if (infoPanel != null)
            infoPanel.SetActive(data != null);

        if (data == null) return;

        if (infoIcon != null)
        {
            infoIcon.sprite = data.icon;
            infoIcon.enabled = data.icon != null;
            infoIcon.preserveAspect = true;
        }
        if (infoName != null) infoName.text = data.heroName;
        if (infoDescription != null) infoDescription.text = data.description;
        if (infoHealth != null) infoHealth.text = Mathf.RoundToInt(data.maxHealth).ToString();
        if (infoEnergy != null) infoEnergy.text = Mathf.RoundToInt(data.maxEnergy).ToString();
        if (infoDamage != null)
        {
            int dmg1 = Mathf.RoundToInt(data.attack1Damage);
            infoDamage.text = data.attackCount >= 2
                ? $"{dmg1}/{Mathf.RoundToInt(data.attack2Damage)}"
                : dmg1.ToString();
        }
        if (infoExtra != null) infoExtra.text = Mathf.RoundToInt(data.maxPoise).ToString();

        if (infoAbilityIcon != null)
        {
            infoAbilityIcon.sprite = data.abilityIcon;
            infoAbilityIcon.enabled = data.abilityIcon != null;
            infoAbilityIcon.preserveAspect = true;
        }
        if (infoAbilityName != null) infoAbilityName.text = data.abilityName;
        if (infoAbilityDescription != null) infoAbilityDescription.text = data.abilityDescription;
    }

    private HeroData FindHeroData(HeroType? type)
    {
        if (!type.HasValue) return null;
        var lobby = LobbyManager.Instance;
        if (lobby == null || lobby.AllHeroes == null) return null;
        foreach (var h in lobby.AllHeroes)
            if (h != null && h.heroType == type.Value) return h;
        return null;
    }


    private void OnCardClicked(HeroData data)
    {
        if (data == null) return;

        pendingSelection = data.heroType;

        // Залоченного героя нельзя выбрать — он только подсвечивается в плашке,
        // а покупка происходит через контекстную кнопку «Купить».
        if (!HeroUnlockManager.IsUnlocked(data))
        {
            RefreshCards();
            return;
        }

        // Занятого союзником героя выбирать нельзя (но плашку всё равно показываем).
        var lobby = LobbyManager.Instance;
        if (lobby != null && lobby.IsHeroTaken(data.heroType))
        {
            var currentHero = lobby.GetLocalPlayerHero();
            if (!currentHero.HasValue || currentHero.Value != data.heroType)
            {
                RefreshCards();
                return;
            }
        }

        // Клик по доступной карточке сразу выбирает героя (сервер валидирует повторно).
        NetworkClient.Send(new HeroSelectRequest { heroType = data.heroType });
        RefreshCards();
    }

    private void OnActionClicked()
    {
        var data = FindHeroData(pendingSelection);

        // Режим «Купить»: герой в плашке залочен.
        if (data != null && !HeroUnlockManager.IsUnlocked(data))
        {
            if (HeroUnlockManager.TryUnlock(data))
            {
                // Сообщаем серверу о новой разблокировке, иначе он отклонит выбор
                // только что купленного героя (clientUnlocks был бы устаревшим).
                var nm = NetworkManager.singleton as DungeonKnightNetworkManager;
                if (nm != null) nm.SendUnlocksToServer();
            }
            RefreshCards();
            return;
        }

        // Иначе — toggle готовности (сервер отклонит, если герой не выбран).
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
