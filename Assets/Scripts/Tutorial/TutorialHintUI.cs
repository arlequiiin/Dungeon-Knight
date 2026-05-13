using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Тост-подсказка туториала. Один экземпляр на сцену, обычно в верхней или нижней
/// части Canvas. Принимает TutorialHint, показывает текст + иконку с fade-in/out
/// на duration секунд. Параллельные вызовы Show заменяют предыдущую подсказку.
///
/// Размещение: добавь Canvas → панель с CanvasGroup, TMP_Text, опционально Image,
/// и компонент TutorialHintUI с ссылками. Сохраняй активным — fade управляется
/// CanvasGroup.alpha.
/// </summary>
public class TutorialHintUI : MonoBehaviour
{
    public static TutorialHintUI Instance { get; private set; }

    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image iconImage;

    [Tooltip("Длительность fade-in/out.")]
    [SerializeField] private float fadeDuration = 0.25f;

    private Coroutine activeRoutine;

    private void Awake()
    {
        Instance = this;
        if (group != null) group.alpha = 0f;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Show(TutorialHint hint)
    {
        if (hint == null || label == null || group == null) return;
        if (activeRoutine != null) StopCoroutine(activeRoutine);

        label.text = hint.text;
        if (iconImage != null)
        {
            iconImage.sprite = hint.icon;
            iconImage.enabled = hint.icon != null;
        }

        activeRoutine = StartCoroutine(ShowRoutine(hint.duration));
    }

    private IEnumerator ShowRoutine(float duration)
    {
        yield return FadeTo(1f);
        yield return new WaitForSeconds(Mathf.Max(0.1f, duration - fadeDuration * 2f));
        yield return FadeTo(0f);
        activeRoutine = null;
    }

    private IEnumerator FadeTo(float target)
    {
        float start = group.alpha;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(start, target, t / fadeDuration);
            yield return null;
        }
        group.alpha = target;
    }
}
