using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD локального игрока: HP, энергия, кулдаун способности, монетки.
/// Создаётся автоматически при спавне локального игрока.
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    [Header("Top-Left Panel")]
    [Tooltip("Корень панели с иконкой героя, HP, энергией. Скрывается при открытии окна выбора героя в лобби.")]
    [SerializeField] private GameObject topLeftPanel;

    [Header("Hero Info")]
    [SerializeField] private Image heroIcon;
    [SerializeField] private TMP_Text heroNameText;

    [Header("Health")]
    [SerializeField] private Image healthFill;
    [SerializeField] private Image healthGhostFill;

    [Header("Downed")]
    [Tooltip("Полоска упавшего — перекрывает обычный HP-бар когда IsDowned")]
    [SerializeField] private Image downedFill;
    [SerializeField] private GameObject downedOverlay;

    [Header("Energy")]
    [SerializeField] private Image energyFill;

    [Header("Allies")]
    [Tooltip("Префаб плашки союзника (компонент AllyPanel)")]
    [SerializeField] private GameObject allyPanelPrefab;
    [Tooltip("Контейнер для плашек союзников (слева на экране)")]
    [SerializeField] private RectTransform alliesContainer;
    [Tooltip("Вертикальное смещение плашек от центра (px). При 2 союзниках — первый выше на это значение, второй ниже.")]
    [SerializeField] private float allySpacingY = 60f;

    [Header("Ability Cooldown")]
    [SerializeField] private Image ability1CooldownFill;

    [Header("Coins")]
    [SerializeField] private TMP_Text coinText;

    [Header("Center Notification")]
    [Tooltip("Большой текст по центру экрана (\"ROOM CLEARED\", \"BOSS DEFEATED\"). Может быть пустым.")]
    [SerializeField] private TMP_Text centerNotificationText;
    [Tooltip("Сколько секунд показывается текст уведомления")]
    [SerializeField] private float notificationDuration = 2.5f;

    [Header("Подобранные награды")]
    [Tooltip("Контейнер столбика подобранных наград (например, VerticalLayoutGroup в углу экрана).")]
    [SerializeField] private RectTransform rewardListContainer;
    [Tooltip("Префаб строки журнала наград. Должен содержать Image \"Icon\", TMP_Text \"Name\", TMP_Text \"Count\".")]
    [SerializeField] private GameObject rewardEntryPrefab;

    [Header("Лента событий")]
    [Tooltip("Контейнер для коротких уведомлений (\"Паладин повержен\" и т.п.).")]
    [SerializeField] private RectTransform eventFeedContainer;
    [Tooltip("Префаб строки события: корневой объект с TMP_Text. Создаётся при каждом событии, уничтожается через eventFeedDuration сек.")]
    [SerializeField] private GameObject eventFeedEntryPrefab;
    [Tooltip("Сколько секунд держится одно событие в ленте")]
    [SerializeField] private float eventFeedDuration = 5f;

    public static PlayerHUD LocalInstance { get; private set; }

    private HeroStats stats;
    private HeroAbility ability;

    // Ghost bar — плавно догоняет реальное HP
    private float ghostHealth = 1f;
    private float ghostDelay;
    private const float GhostDelayTime = 0.5f;
    private const float GhostLerpSpeed = 2f;

    private int displayedCoins;

    private HeroData heroData;
    private PlayerController localPC;
    private readonly System.Collections.Generic.List<AllyPanel> allyPanels = new();

    private RunRewardLog rewardLog;

    private bool ShowingMeta =>
        UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Contains("LobbyScene");

    private void Awake()
    {
        LocalInstance = this;

        // В лобби HUD показывает мета-валюту (постоянная, для разблокировки героев),
        // в забеге — RunCoins (накопленные за текущий забег, тратятся в сундуках).
        SetCoins(ShowingMeta ? CurrencyManager.MetaCoins : CurrencyManager.RunCoins);
        CurrencyManager.OnRunCoinsChanged += OnRunCoinsChanged;
        CurrencyManager.OnMetaCoinsChanged += OnMetaCoinsChanged;

        if (centerNotificationText != null)
            centerNotificationText.gameObject.SetActive(false);
    }

    /// <summary>
    /// Скрыть/показать верхне-левую панель (иконка героя, HP, энергия).
    /// Вызывается из HeroSelectionUI при открытии/закрытии окна выбора героя.
    /// </summary>
    public void SetTopLeftPanelVisible(bool visible)
    {
        if (topLeftPanel != null)
            topLeftPanel.SetActive(visible);
    }

    // Базовый размер шрифта уведомления (из инспектора). Запоминается при первом показе,
    // чтобы можно было временно уменьшать шрифт для длинных уведомлений и затем восстанавливать.
    private float baseNotificationFontSize = -1f;
    // Базовая anchoredPosition текста уведомления — чтобы можно было временно сместить по Y
    // (напр. уведомление казино выше) и затем вернуть на место.
    private Vector2 baseNotificationPos;
    private bool baseNotificationPosCaptured;

    /// <summary>
    /// Показать большой текст по центру экрана на duration секунд.
    /// fontSize > 0 задаёт временный размер шрифта (например для длинных многострочных
    /// уведомлений казино); иначе используется размер из инспектора.
    /// yOffset смещает текст по вертикали относительно базовой позиции (для конкретного
    /// уведомления, напр. казино — чуть выше); 0 = базовая позиция.
    /// </summary>
    public void ShowNotification(string text, float duration = -1f, float fontSize = -1f, float yOffset = 0f)
    {
        if (centerNotificationText == null) return;
        if (duration < 0f) duration = notificationDuration;

        if (baseNotificationFontSize < 0f)
            baseNotificationFontSize = centerNotificationText.fontSize;
        centerNotificationText.fontSize = fontSize > 0f ? fontSize : baseNotificationFontSize;

        var rt = centerNotificationText.rectTransform;
        if (!baseNotificationPosCaptured)
        {
            baseNotificationPos = rt.anchoredPosition;
            baseNotificationPosCaptured = true;
        }
        rt.anchoredPosition = baseNotificationPos + new Vector2(0f, yOffset);

        // Останавливаем предыдущую корутину по ХЭНДЛУ. StopCoroutine(nameof(...)) по строке
        // не останавливает корутину, запущенную по IEnumerator, — из-за этого таймер прошлого
        // уведомления досчитывал и прятал новый текст раньше времени.
        if (notificationRoutine != null) StopCoroutine(notificationRoutine);
        centerNotificationText.text = text;
        centerNotificationText.gameObject.SetActive(true);
        notificationRoutine = StartCoroutine(NotificationRoutine(duration));
    }

    private Coroutine notificationRoutine;

    private System.Collections.IEnumerator NotificationRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (centerNotificationText != null)
            centerNotificationText.gameObject.SetActive(false);
        notificationRoutine = null;
    }

    private void OnRunCoinsChanged(int amount)
    {
        if (!ShowingMeta) SetCoins(amount);
    }

    private void OnMetaCoinsChanged(int amount)
    {
        if (ShowingMeta) SetCoins(amount);
    }

    public void Init(HeroStats heroStats, HeroAbility heroAbility)
    {
        stats = heroStats;
        ability = heroAbility;

        // Подписка на SyncVar-хуки через UnityEvent
        stats.onHealthChanged.AddListener(OnHealthChanged);
        stats.onEnergyChanged.AddListener(OnEnergyChanged);
        stats.onDownedHealthChanged.AddListener(OnDownedHealthChanged);
        stats.onDowned.AddListener(OnDowned);
        stats.onRevived.AddListener(OnRevived);

        // Иконка и имя героя
        localPC = stats.GetComponent<PlayerController>();
        heroData = localPC != null ? localPC.heroData : null;
        ApplyHeroVisual(heroData);

        // Смена героя в лобби меняет heroData позже — обновляем имя/иконку по событию.
        if (localPC != null)
            localPC.onHeroDataChanged += ApplyHeroVisual;

        // Начальные значения
        UpdateHealthBar(stats.HealthNormalized);
        ghostHealth = stats.HealthNormalized;
        UpdateEnergyBar(stats.EnergyNormalized);
        UpdateDownedOverlay();

        // Журнал подобранных наград (для столбика в углу).
        rewardLog = stats.GetComponent<RunRewardLog>();
        if (rewardLog != null)
        {
            rewardLog.OnLogChanged += RefreshRewardList;
            RefreshRewardList();
        }
    }

    /// <summary>Обновляет иконку и имя героя в HUD. Вызывается при инициализации и при смене героя.</summary>
    private void ApplyHeroVisual(HeroData data)
    {
        heroData = data;
        if (heroIcon != null)
            heroIcon.sprite = data != null ? data.icon : null;
        if (heroNameText != null)
            heroNameText.text = data != null ? data.heroName : "";
    }

    private void OnDestroy()
    {
        if (localPC != null)
            localPC.onHeroDataChanged -= ApplyHeroVisual;

        if (stats != null)
        {
            stats.onHealthChanged.RemoveListener(OnHealthChanged);
            stats.onEnergyChanged.RemoveListener(OnEnergyChanged);
            stats.onDownedHealthChanged.RemoveListener(OnDownedHealthChanged);
            stats.onDowned.RemoveListener(OnDowned);
            stats.onRevived.RemoveListener(OnRevived);
        }
        CurrencyManager.OnRunCoinsChanged -= OnRunCoinsChanged;
        CurrencyManager.OnMetaCoinsChanged -= OnMetaCoinsChanged;

        if (rewardLog != null)
            rewardLog.OnLogChanged -= RefreshRewardList;

        if (LocalInstance == this) LocalInstance = null;
    }

    private void OnDowned() => UpdateDownedOverlay();
    private void OnRevived() => UpdateDownedOverlay();

    private void OnDownedHealthChanged(float current, float max)
    {
        if (downedFill != null)
            downedFill.fillAmount = max > 0 ? current / max : 0f;
    }

    private void UpdateDownedOverlay()
    {
        bool downed = stats != null && stats.IsDowned;
        if (downedOverlay != null)
            downedOverlay.SetActive(downed);
        if (downedFill != null)
            downedFill.fillAmount = stats != null ? stats.DownedHealthNormalized : 1f;
    }

    private float allyRescanTimer;
    private const float AllyRescanInterval = 1f;

    private void Update()
    {
        if (stats == null) return;

        // Ghost bar для HP
        UpdateGhostBar();

        // Кулдаун способности
        if (ability != null && ability1CooldownFill != null)
            ability1CooldownFill.fillAmount = ability.GetAbility1CooldownNormalized();

        // Рескан союзников раз в секунду (дёшево, ≤ 3 игрока)
        allyRescanTimer -= Time.deltaTime;
        if (allyRescanTimer <= 0f)
        {
            allyRescanTimer = AllyRescanInterval;
            RescanAllies();
        }
    }

    // === Allies ===

    private void RescanAllies()
    {
        if (allyPanelPrefab == null || alliesContainer == null) return;

        // Убираем панели, чей HeroStats исчез
        for (int i = allyPanels.Count - 1; i >= 0; i--)
        {
            if (allyPanels[i] == null || allyPanels[i].Stats == null)
            {
                if (allyPanels[i] != null) Destroy(allyPanels[i].gameObject);
                allyPanels.RemoveAt(i);
            }
        }

        // Находим всех других локальных HeroStats
        var all = FindObjectsByType<HeroStats>(FindObjectsSortMode.None);
        foreach (var hs in all)
        {
            if (hs == stats) continue;
            if (HasPanelFor(hs)) continue;
            if (allyPanels.Count >= 2) break; // максимум 2 союзника (до 3 игроков)

            var obj = Instantiate(allyPanelPrefab, alliesContainer);
            var panel = obj.GetComponent<AllyPanel>();
            if (panel == null)
            {
                Destroy(obj);
                continue;
            }

            var pc = hs.GetComponent<PlayerController>();
            var data = pc != null ? pc.heroData : null;
            panel.Bind(hs, data);
            allyPanels.Add(panel);
        }

        RepositionAllies();
    }

    private bool HasPanelFor(HeroStats hs)
    {
        foreach (var p in allyPanels)
            if (p != null && p.Stats == hs) return true;
        return false;
    }

    private void RepositionAllies()
    {
        // 1 союзник → y=0 (центр по Y контейнера).
        // 2 союзника → первый выше (+allySpacingY), второй ниже (-allySpacingY).
        int n = allyPanels.Count;
        for (int i = 0; i < n; i++)
        {
            if (allyPanels[i] == null) continue;
            var rt = allyPanels[i].transform as RectTransform;
            if (rt == null) continue;

            float y = n == 1 ? 0f : (i == 0 ? allySpacingY : -allySpacingY);
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
        }
    }

    // --- Health ---

    private void OnHealthChanged(float current, float max)
    {
        float normalized = max > 0 ? current / max : 0f;
        UpdateHealthBar(normalized);

        // Запускаем задержку ghost bar
        ghostDelay = GhostDelayTime;
    }

    private void UpdateHealthBar(float normalized)
    {
        if (healthFill != null)
            healthFill.fillAmount = normalized;
    }

    private void UpdateGhostBar()
    {
        if (healthGhostFill == null) return;

        float target = healthFill != null ? healthFill.fillAmount : 0f;

        if (ghostHealth > target)
        {
            ghostDelay -= Time.deltaTime;
            if (ghostDelay <= 0f)
                ghostHealth = Mathf.Lerp(ghostHealth, target, GhostLerpSpeed * Time.deltaTime);
        }
        else
        {
            // Хил — ghost мгновенно догоняет
            ghostHealth = target;
        }

        healthGhostFill.fillAmount = ghostHealth;
    }

    // --- Energy ---

    private void OnEnergyChanged(float current, float max)
    {
        float normalized = max > 0 ? current / max : 0f;
        UpdateEnergyBar(normalized);
    }

    private void UpdateEnergyBar(float normalized)
    {
        if (energyFill != null)
            energyFill.fillAmount = normalized;
    }

    // --- Coins ---

    public void SetCoins(int amount)
    {
        displayedCoins = amount;
        if (coinText != null)
            coinText.text = displayedCoins.ToString();
    }

    public void AddCoins(int amount)
    {
        displayedCoins += amount;
        if (coinText != null)
        {
            coinText.text = displayedCoins.ToString();
            // Punch-эффект при получении монет
            StartCoroutine(CoinPunchEffect());
        }
    }

    private System.Collections.IEnumerator CoinPunchEffect()
    {
        Vector3 original = coinText.transform.localScale;
        coinText.transform.localScale = original * 1.3f;

        float t = 0f;
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            coinText.transform.localScale = Vector3.Lerp(original * 1.3f, original, t / 0.2f);
            yield return null;
        }

        coinText.transform.localScale = original;
    }

    // --- Event Feed ---

    /// <summary>
    /// Добавить короткое сообщение в ленту событий. Вызывается из RunModifiers.RpcAddEvent.
    /// </summary>
    public void AddFeedEvent(string text)
    {
        if (eventFeedContainer == null || eventFeedEntryPrefab == null) return;
        var entry = Instantiate(eventFeedEntryPrefab, eventFeedContainer);
        var label = entry.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = text;
        Destroy(entry, eventFeedDuration);
    }

    // --- Reward Log ---

    private void RefreshRewardList()
    {
        if (rewardListContainer == null || rewardEntryPrefab == null || rewardLog == null) return;

        // Полная перерисовка: проще, чем диффить — наград за забег не больше десятка.
        for (int i = rewardListContainer.childCount - 1; i >= 0; i--)
            Destroy(rewardListContainer.GetChild(i).gameObject);

        var stacks = rewardLog.GetStackedRewards();
        foreach (var (reward, count) in stacks)
        {
            var entry = Instantiate(rewardEntryPrefab, rewardListContainer);
            var icon = entry.transform.Find("Icon")?.GetComponent<Image>();
            var name = entry.transform.Find("Name")?.GetComponent<TMP_Text>();
            var countText = entry.transform.Find("Count")?.GetComponent<TMP_Text>();

            if (icon != null)
            {
                icon.sprite = reward.icon;
                icon.enabled = reward.icon != null;
                icon.preserveAspect = true;
            }
            if (name != null) name.text = reward.rewardName;
            if (countText != null)
            {
                // Если эффект умеет показать суммарное числовое значение (урон/защита/КД/реген) —
                // показываем его вместо «xN». Иначе — привычный счётчик стаков.
                string total = reward.effect != null ? reward.effect.DescribeTotal(count) : null;
                if (!string.IsNullOrEmpty(total))
                {
                    countText.gameObject.SetActive(true);
                    countText.text = total;
                }
                else
                {
                    countText.gameObject.SetActive(count > 1);
                    countText.text = $"x{count}";
                }
            }
        }

        // Модификаторы казино — отдельные строки без иконки (готовый ScriptableObject отсутствует).
        foreach (var mod in rewardLog.CasinoModifiers)
        {
            if (string.IsNullOrEmpty(mod)) continue;
            var entry = Instantiate(rewardEntryPrefab, rewardListContainer);
            var icon = entry.transform.Find("Icon")?.GetComponent<Image>();
            var name = entry.transform.Find("Name")?.GetComponent<TMP_Text>();
            var countText = entry.transform.Find("Count")?.GetComponent<TMP_Text>();

            if (icon != null) icon.enabled = false;           // иконки у казино-записей нет
            if (name != null) name.text = mod;
            if (countText != null) countText.gameObject.SetActive(false);
        }
    }
}
