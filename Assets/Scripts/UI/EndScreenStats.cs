using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Общая логика заполнения итогов забега (победа/поражение) — колонки на игрока.
/// Используется обоими экранами: VictoryScreenUI и GameOverUI.
/// Считывает данные напрямую с RunStatsTracker каждого живого NetworkBehaviour'а
/// (SyncVar — клиент видит актуальные значения).
/// </summary>
public class EndScreenStats : MonoBehaviour
{
    [Tooltip("Контейнер для колонок (HorizontalLayoutGroup).")]
    [SerializeField] private RectTransform columnContainer;
    [Tooltip("Префаб колонки игрока. Должен содержать дочерние TMP_Text: Title, Kills, Downs.")]
    [SerializeField] private GameObject columnPrefab;
    [Tooltip("Текст общей суммы монет (опционально, если null — не показывается).")]
    [SerializeField] private TMP_Text totalCoinsText;
    [Tooltip("Иконка героя в колонке (опционально, найдётся как Image \"Icon\").")]
    [SerializeField] private bool showHeroIcon = true;

    public void Populate()
    {
        if (columnContainer == null || columnPrefab == null) return;

        // Очистка предыдущих колонок (если открыли экран повторно).
        for (int i = columnContainer.childCount - 1; i >= 0; i--)
            Destroy(columnContainer.GetChild(i).gameObject);

        var trackers = FindObjectsByType<RunStatsTracker>(FindObjectsSortMode.None);
        // Сортируем по netId, чтобы порядок колонок был стабильным между клиентами.
        System.Array.Sort(trackers, (a, b) => a.netId.CompareTo(b.netId));

        int totalCoins = 0;
        int index = 1;
        foreach (var tracker in trackers)
        {
            if (tracker == null) continue;

            var col = Instantiate(columnPrefab, columnContainer);

            var pc = tracker.GetComponent<PlayerController>();
            string heroName = pc != null && pc.heroData != null ? pc.heroData.heroName : $"Игрок {index}";

            SetText(col, "Title", heroName);
            SetText(col, "Kills", $"Убито мобов: {tracker.killedMobs}");
            SetText(col, "Downs", $"Падений: {tracker.downedCount}");
            SetText(col, "Coins", $"Монет: {tracker.collectedCoins}");

            if (showHeroIcon && pc != null && pc.heroData != null)
            {
                var iconImg = col.transform.Find("Icon")?.GetComponent<Image>();
                if (iconImg != null)
                {
                    iconImg.sprite = pc.heroData.icon;
                    iconImg.enabled = pc.heroData.icon != null;
                    iconImg.preserveAspect = true;
                }
            }

            totalCoins += tracker.collectedCoins;
            index++;
        }

        if (totalCoinsText != null)
            totalCoinsText.text = $"Собрано монет: {totalCoins}";
    }

    private static void SetText(GameObject root, string childName, string text)
    {
        var t = root.transform.Find(childName)?.GetComponent<TMP_Text>();
        if (t != null) t.text = text;
    }
}
