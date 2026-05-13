using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// Серверная координация туториал-подсказок. Хранит список TutorialHint SO,
/// помнит какие id уже были показаны, и шлёт ShowHintMessage всем клиентам.
///
/// Клиентская часть: при OnStartClient регистрирует хендлер ShowHintMessage,
/// ищет SO по id и передаёт его в TutorialHintUI для отрисовки.
///
/// Размещается на отдельном scene-объекте в SampleScene.
/// </summary>
public class TutorialManager : NetworkBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [Tooltip("Все доступные подсказки. Сервер ищет по id, клиент — тоже по id.")]
    [SerializeField] private TutorialHint[] hints;

    [Tooltip("Если false — TriggerHint игнорирует все вызовы (для production-билдов).")]
    [SerializeField] private bool enabled = true;

    [Tooltip("Если true — туториал показывается только пока флаг TutorialCompleted в PlayerPrefs не выставлен. " +
             "Флаг выставляется при финальной победе. Сбрасывается через ResetProgress.")]
    [SerializeField] private bool onlyUntilCompleted = true;

    private const string PrefsKey = "TutorialCompleted";

    /// <summary>Туториал уже пройден (победа над финальным боссом в прошлом забеге)?</summary>
    public static bool IsCompleted => PlayerPrefs.GetInt(PrefsKey, 0) == 1;

    /// <summary>Помечает туториал пройденным. Вызывается сервером при финальной победе.</summary>
    public static void MarkCompleted()
    {
        PlayerPrefs.SetInt(PrefsKey, 1);
        PlayerPrefs.Save();
    }

    /// <summary>Сбросить флаг (вызывается из настроек "Сбросить прогресс").</summary>
    public static void ResetCompleted()
    {
        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();
    }

    // Server-side: какие id уже были показаны в этом забеге (сохраняется между биомами).
    private readonly HashSet<string> shownOnServer = new();

    // Client-side: каких id мы уже видели (защита от дублей при late-join / повторных RPC).
    private readonly HashSet<string> shownOnClient = new();

    private Dictionary<string, TutorialHint> hintsById;

    private void Awake()
    {
        Instance = this;
        BuildIndex();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void BuildIndex()
    {
        hintsById = new Dictionary<string, TutorialHint>();
        if (hints == null) return;
        foreach (var h in hints)
        {
            if (h == null || string.IsNullOrEmpty(h.id)) continue;
            hintsById[h.id] = h;
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        NetworkClient.RegisterHandler<ShowHintMessage>(OnShowHintReceived);
    }

    /// <summary>
    /// Сбросить состояние "показано" (например, перед новым забегом).
    /// Вызывать только на сервере.
    /// </summary>
    [Server]
    public void ResetShown()
    {
        shownOnServer.Clear();
    }

    /// <summary>
    /// Запросить показ подсказки. Идемпотентно: вторая попытка показать
    /// тот же id игнорируется. Вызывать только на сервере.
    /// </summary>
    [Server]
    public void TriggerHint(string id)
    {
        if (!enabled) return;
        // На хосте проверяем флаг локально — если хост уже прошёл туториал, не шлём
        // подсказки никому. Это допустимое упрощение: считаем, что демо-сценарий
        // ведётся хостом, и его статус "пройден" — общий для группы.
        if (onlyUntilCompleted && IsCompleted) return;
        if (string.IsNullOrEmpty(id)) return;
        if (!shownOnServer.Add(id)) return;
        if (hintsById == null || !hintsById.ContainsKey(id))
        {
            Debug.LogWarning($"[Tutorial] Hint id \"{id}\" не найден в TutorialManager.hints");
            return;
        }
        NetworkServer.SendToAll(new ShowHintMessage { hintId = id });
    }

    /// <summary>
    /// Безопасная обёртка: можно дёргать с клиента/сервера, проверит NetworkServer.active.
    /// </summary>
    public static void Trigger(string id)
    {
        if (Instance == null) return;
        if (!NetworkServer.active) return;
        Instance.TriggerHint(id);
    }

    private void OnShowHintReceived(ShowHintMessage msg)
    {
        if (string.IsNullOrEmpty(msg.hintId)) return;
        // Клиент-сайд: если игрок локально уже видел туториал в прошлом забеге, не показываем.
        // (Сервер обычно не пришлёт сообщение, но это страховка на случай, когда у хоста
        //  туториал ещё не пройден, а у конкретного клиента — уже да.)
        if (onlyUntilCompleted && IsCompleted) return;
        if (!shownOnClient.Add(msg.hintId)) return;

        if (hintsById == null) BuildIndex();
        if (!hintsById.TryGetValue(msg.hintId, out var hint)) return;

        if (TutorialHintUI.Instance != null)
            TutorialHintUI.Instance.Show(hint);
        else if (PlayerHUD.LocalInstance != null)
            PlayerHUD.LocalInstance.ShowNotification(hint.text, hint.duration);
    }
}
