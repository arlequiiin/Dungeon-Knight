using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD локального игрока: HP, энергия, кулдаун способности, монетки.
/// Создаётся автоматически при спавне локального игрока.
/// </summary>
public class PlayerHUD : MonoBehaviour
{
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
    private readonly System.Collections.Generic.List<AllyPanel> allyPanels = new();

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
    /// Показать большой текст по центру экрана на duration секунд.
    /// </summary>
    public void ShowNotification(string text, float duration = -1f)
    {
        Debug.Log($"[HUD] ShowNotification text='{text}' centerTextNull={centerNotificationText == null}");
        if (centerNotificationText == null) return;
        if (duration < 0f) duration = notificationDuration;

        StopCoroutine(nameof(NotificationRoutine));
        centerNotificationText.text = text;
        centerNotificationText.gameObject.SetActive(true);
        StartCoroutine(NotificationRoutine(duration));
    }

    private System.Collections.IEnumerator NotificationRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (centerNotificationText != null)
            centerNotificationText.gameObject.SetActive(false);
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
        var pc = stats.GetComponent<PlayerController>();
        heroData = pc != null ? pc.heroData : null;
        if (heroIcon != null)
            heroIcon.sprite = heroData != null ? heroData.icon : null;
        if (heroNameText != null)
            heroNameText.text = heroData != null ? heroData.heroName : "";

        // Начальные значения
        UpdateHealthBar(stats.HealthNormalized);
        ghostHealth = stats.HealthNormalized;
        UpdateEnergyBar(stats.EnergyNormalized);
        UpdateDownedOverlay();
    }

    private void OnDestroy()
    {
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
}
