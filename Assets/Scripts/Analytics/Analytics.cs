using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Локальный JSON-логгер событий забега для балансового анализа.
/// Каждое событие — отдельная строка JSON (NDJSON формат), легко парсится Python/jq.
/// Файлы пишутся в Application.persistentDataPath/Analytics/run_<id>.ndjson.
/// На Windows это обычно %userprofile%/AppData/LocalLow/<company>/<game>/Analytics/.
///
/// Серверная сторона: события забега (start/end, room_enter, room_clear, ...).
/// Клиентская сторона: hero_picked, ability_used (дополнения по желанию).
///
/// Не привязано к Mirror — может вызываться из любого места.
/// </summary>
public static class Analytics
{
    private static StreamWriter writer;
    private static string runId;
    private static float runStartTime;
    private static bool enabled = true;

    /// <summary>Полностью отключить логирование (например, для production-билда).</summary>
    public static bool Enabled
    {
        get => enabled;
        set => enabled = value;
    }

    public static string CurrentRunId => runId;

    /// <summary>
    /// Стартовать новый файл-лог. Вызывать на сервере при старте забега.
    /// </summary>
    public static void StartRun()
    {
        if (!enabled) return;
        EndRun(); // на всякий случай закрыть предыдущий

        runId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + "_" + UnityEngine.Random.Range(1000, 9999);
        runStartTime = Time.time;

        string dir = Path.Combine(Application.persistentDataPath, "Analytics");
        try
        {
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, $"run_{runId}.ndjson");
            writer = new StreamWriter(path, append: false, Encoding.UTF8);
            Debug.Log($"[Analytics] Run log → {path}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Analytics] Failed to open log file: {e.Message}");
            writer = null;
        }
    }

    /// <summary>Закрыть текущий файл. Вызывать в конце забега.</summary>
    public static void EndRun()
    {
        if (writer == null) return;
        try { writer.Flush(); writer.Close(); }
        catch { }
        writer = null;
    }

    /// <summary>
    /// Записать событие. Поля передаются парами (key, value). Значения сериализуются примитивно.
    /// Пример: Analytics.Event("mob_killed", "mob", "Werewolf", "byHero", "Knight", "dmg", 35).
    /// </summary>
    public static void Event(string type, params object[] kvPairs)
    {
        if (!enabled || writer == null) return;
        try
        {
            var sb = new StringBuilder(128);
            sb.Append("{\"t\":").Append((Time.time - runStartTime).ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
            sb.Append(",\"event\":\"").Append(Escape(type)).Append('"');

            for (int i = 0; i + 1 < kvPairs.Length; i += 2)
            {
                string key = kvPairs[i] as string;
                if (string.IsNullOrEmpty(key)) continue;
                sb.Append(",\"").Append(Escape(key)).Append("\":");
                AppendValue(sb, kvPairs[i + 1]);
            }
            sb.Append('}');
            writer.WriteLine(sb.ToString());
            writer.Flush();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Analytics] Write failed: {e.Message}");
        }
    }

    private static void AppendValue(StringBuilder sb, object v)
    {
        if (v == null) { sb.Append("null"); return; }
        switch (v)
        {
            case bool b: sb.Append(b ? "true" : "false"); break;
            case int or long or float or double:
                sb.Append(Convert.ToDouble(v).ToString("G", System.Globalization.CultureInfo.InvariantCulture));
                break;
            case string s: sb.Append('"').Append(Escape(s)).Append('"'); break;
            default: sb.Append('"').Append(Escape(v.ToString())).Append('"'); break;
        }
    }

    private static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
    }
}
