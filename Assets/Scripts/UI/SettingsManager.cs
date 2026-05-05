using System;
using UnityEngine;

/// <summary>
/// Глобальные настройки игры: громкость музыки, эффектов, полноэкранный режим.
/// Хранятся в PlayerPrefs, доступны через статический API.
///
/// Громкость применяется через AudioListener.volume (общая) с разделением по AudioSource:
/// SFX-источники должны иметь тег "SFX", музыкальные — "Music". Если архитектура звука
/// в проекте появится позже (AudioMixer), достаточно подменить применение.
/// </summary>
public static class SettingsManager
{
    private const string KeyMusic = "dk_music_volume";
    private const string KeySfx = "dk_sfx_volume";
    private const string KeyFullscreen = "dk_fullscreen";

    public static event Action<float> OnMusicVolumeChanged;
    public static event Action<float> OnSfxVolumeChanged;
    public static event Action<bool> OnFullscreenChanged;

    private static bool initialized;
    private static float musicVolume = 1f;
    private static float sfxVolume = 1f;
    private static bool fullscreen = true;

    private static void EnsureInit()
    {
        if (initialized) return;
        initialized = true;
        musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMusic, 1f));
        sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeySfx, 1f));
        fullscreen = PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) == 1;
        ApplyFullscreen(fullscreen);
    }

    /// <summary>Гарантирует загрузку настроек на старте сцены.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() => EnsureInit();

    public static float MusicVolume
    {
        get { EnsureInit(); return musicVolume; }
        set
        {
            EnsureInit();
            float v = Mathf.Clamp01(value);
            if (Mathf.Approximately(musicVolume, v)) return;
            musicVolume = v;
            PlayerPrefs.SetFloat(KeyMusic, v);
            PlayerPrefs.Save();
            OnMusicVolumeChanged?.Invoke(v);
        }
    }

    public static float SfxVolume
    {
        get { EnsureInit(); return sfxVolume; }
        set
        {
            EnsureInit();
            float v = Mathf.Clamp01(value);
            if (Mathf.Approximately(sfxVolume, v)) return;
            sfxVolume = v;
            PlayerPrefs.SetFloat(KeySfx, v);
            PlayerPrefs.Save();
            OnSfxVolumeChanged?.Invoke(v);
        }
    }

    public static bool Fullscreen
    {
        get { EnsureInit(); return fullscreen; }
        set
        {
            EnsureInit();
            if (fullscreen == value) return;
            fullscreen = value;
            PlayerPrefs.SetInt(KeyFullscreen, value ? 1 : 0);
            PlayerPrefs.Save();
            ApplyFullscreen(value);
            OnFullscreenChanged?.Invoke(value);
        }
    }

    private static void ApplyFullscreen(bool on)
    {
        Screen.fullScreen = on;
    }

    /// <summary>
    /// Полный сброс прогресса: монеты (мета), разблокировки героев и сами настройки оставляем,
    /// чтобы игрок не сбрасывал случайно громкость.
    /// </summary>
    public static void ResetProgress()
    {
        CurrencyManager.DebugResetMeta();
        HeroUnlockManager.DebugLockAll();
    }
}
