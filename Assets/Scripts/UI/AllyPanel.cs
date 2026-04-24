using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Плашка союзника (слева на экране). Отображает иконку, имя, HP.
/// При downed — полоска HP заменяется downed-полоской другого цвета.
/// </summary>
public class AllyPanel : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image healthFill;
    [SerializeField] private Image downedFill;
    [SerializeField] private GameObject healthBarRoot;
    [SerializeField] private GameObject downedBarRoot;

    private HeroStats stats;

    public HeroStats Stats => stats;

    public void Bind(HeroStats heroStats, HeroData data)
    {
        Unbind();
        stats = heroStats;

        if (iconImage != null)
            iconImage.sprite = data != null ? data.icon : null;

        if (nameText != null)
            nameText.text = data != null ? data.heroName : "";

        stats.onHealthChanged.AddListener(OnHealthChanged);
        stats.onDownedHealthChanged.AddListener(OnDownedHealthChanged);
        stats.onDowned.AddListener(Refresh);
        stats.onRevived.AddListener(Refresh);

        Refresh();
    }

    public void Unbind()
    {
        if (stats != null)
        {
            stats.onHealthChanged.RemoveListener(OnHealthChanged);
            stats.onDownedHealthChanged.RemoveListener(OnDownedHealthChanged);
            stats.onDowned.RemoveListener(Refresh);
            stats.onRevived.RemoveListener(Refresh);
        }
        stats = null;
    }

    private void OnDestroy() => Unbind();

    private void OnHealthChanged(float current, float max)
    {
        if (healthFill != null)
            healthFill.fillAmount = max > 0 ? current / max : 0f;
    }

    private void OnDownedHealthChanged(float current, float max)
    {
        if (downedFill != null)
            downedFill.fillAmount = max > 0 ? current / max : 0f;
    }

    private void Refresh()
    {
        if (stats == null) return;

        bool downed = stats.IsDowned;
        if (healthBarRoot != null) healthBarRoot.SetActive(!downed);
        if (downedBarRoot != null) downedBarRoot.SetActive(downed);

        if (healthFill != null)
            healthFill.fillAmount = stats.HealthNormalized;
        if (downedFill != null)
            downedFill.fillAmount = stats.DownedHealthNormalized;
    }
}
