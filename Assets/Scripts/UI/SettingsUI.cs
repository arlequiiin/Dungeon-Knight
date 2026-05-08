using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI окна настроек. Подключается и в главном меню, и в меню паузы.
/// Кнопка сброса прогресса показывается только если <see cref="showResetButton"/> = true
/// (по требованию — только из главного меню).
/// </summary>
public class SettingsUI : MonoBehaviour
{
    [Header("Корневая панель окна (включается/выключается при Open/Close)")]
    [Tooltip("Если не указано — будет использоваться gameObject самого скрипта")]
    [SerializeField] private GameObject panelRoot;

    [Header("Слайдеры")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private TMP_Text musicValueText;
    [SerializeField] private TMP_Text sfxValueText;

    [Header("Полноэкранный режим")]
    [SerializeField] private Toggle fullscreenToggle;

    [Header("Управление")]
    [Tooltip("Если включён — атаки игрока всегда направлены к курсору, без авто-наведения.")]
    [SerializeField] private Toggle aimToCursorToggle;

    [Header("Сброс прогресса (только из главного меню)")]
    [SerializeField] private bool showResetButton = false;
    [SerializeField] private Button resetProgressButton;
    [SerializeField] private GameObject resetConfirmPanel;
    [SerializeField] private Button resetConfirmButton;
    [SerializeField] private Button resetCancelButton;

    [Header("Закрытие окна")]
    [SerializeField] private Button closeButton;

    private void Awake()
    {
        BindUI();
    }

    private bool bound;
    private void BindUI()
    {
        if (bound) return;
        bound = true;
        // Инициализация значений
        if (musicSlider != null)
        {
            musicSlider.SetValueWithoutNotify(SettingsManager.MusicVolume);
            musicSlider.onValueChanged.AddListener(OnMusicChanged);
            UpdateMusicLabel(SettingsManager.MusicVolume);
        }

        if (sfxSlider != null)
        {
            sfxSlider.SetValueWithoutNotify(SettingsManager.SfxVolume);
            sfxSlider.onValueChanged.AddListener(OnSfxChanged);
            UpdateSfxLabel(SettingsManager.SfxVolume);
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.SetIsOnWithoutNotify(SettingsManager.Fullscreen);
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenToggled);
        }

        if (aimToCursorToggle != null)
        {
            aimToCursorToggle.SetIsOnWithoutNotify(SettingsManager.AimToCursor);
            aimToCursorToggle.onValueChanged.AddListener(OnAimToCursorToggled);
        }

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        // Кнопка сброса
        if (resetProgressButton != null)
        {
            resetProgressButton.gameObject.SetActive(showResetButton);
            if (showResetButton)
                resetProgressButton.onClick.AddListener(OnResetClicked);
        }

        if (resetConfirmPanel != null)
            resetConfirmPanel.SetActive(false);

        if (resetConfirmButton != null)
            resetConfirmButton.onClick.AddListener(OnResetConfirmed);

        if (resetCancelButton != null)
            resetCancelButton.onClick.AddListener(OnResetCancelled);
    }

    public void Open()
    {
        var target = panelRoot != null ? panelRoot : gameObject;
        target.SetActive(true);
    }

    public void Close()
    {
        var target = panelRoot != null ? panelRoot : gameObject;
        target.SetActive(false);
    }

    private void OnMusicChanged(float v)
    {
        SettingsManager.MusicVolume = v;
        UpdateMusicLabel(v);
    }

    private void OnSfxChanged(float v)
    {
        SettingsManager.SfxVolume = v;
        UpdateSfxLabel(v);
    }

    private void OnFullscreenToggled(bool on)
    {
        SettingsManager.Fullscreen = on;
    }

    private void OnAimToCursorToggled(bool on)
    {
        SettingsManager.AimToCursor = on;
    }

    private void UpdateMusicLabel(float v)
    {
        if (musicValueText != null)
            musicValueText.text = Mathf.RoundToInt(v * 100f) + "%";
    }

    private void UpdateSfxLabel(float v)
    {
        if (sfxValueText != null)
            sfxValueText.text = Mathf.RoundToInt(v * 100f) + "%";
    }

    // ── Сброс прогресса ──

    private void OnResetClicked()
    {
        if (resetConfirmPanel != null)
            resetConfirmPanel.SetActive(true);
    }

    private void OnResetConfirmed()
    {
        SettingsManager.ResetProgress();
        if (resetConfirmPanel != null)
            resetConfirmPanel.SetActive(false);
    }

    private void OnResetCancelled()
    {
        if (resetConfirmPanel != null)
            resetConfirmPanel.SetActive(false);
    }
}
