using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD локального игрока: HP, энергия, кулдаун способности, монетки.
/// Создаётся автоматически при спавне локального игрока.
/// </summary>
public class PlayerHUD : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private Image healthFill;
    [SerializeField] private Image healthGhostFill;

    [Header("Energy")]
    [SerializeField] private Image energyFill;

    [Header("Ability Cooldown")]
    [SerializeField] private Image ability1CooldownFill;

    [Header("Coins")]
    [SerializeField] private TMP_Text coinText;

    private HeroStats stats;
    private HeroAbility ability;

    // Ghost bar — плавно догоняет реальное HP
    private float ghostHealth = 1f;
    private float ghostDelay;
    private const float GhostDelayTime = 0.5f;
    private const float GhostLerpSpeed = 2f;

    private int displayedCoins;

    public void Init(HeroStats heroStats, HeroAbility heroAbility)
    {
        stats = heroStats;
        ability = heroAbility;

        // Подписка на SyncVar-хуки через UnityEvent
        stats.onHealthChanged.AddListener(OnHealthChanged);
        stats.onEnergyChanged.AddListener(OnEnergyChanged);

        // Начальные значения
        UpdateHealthBar(stats.HealthNormalized);
        ghostHealth = stats.HealthNormalized;
        UpdateEnergyBar(stats.EnergyNormalized);
    }

    private void OnDestroy()
    {
        if (stats != null)
        {
            stats.onHealthChanged.RemoveListener(OnHealthChanged);
            stats.onEnergyChanged.RemoveListener(OnEnergyChanged);
        }
    }

    private void Update()
    {
        if (stats == null) return;

        // Ghost bar для HP
        UpdateGhostBar();

        // Кулдаун способности
        if (ability != null && ability1CooldownFill != null)
            ability1CooldownFill.fillAmount = ability.GetAbility1CooldownNormalized();
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
