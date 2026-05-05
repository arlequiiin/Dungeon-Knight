using UnityEngine;

/// <summary>
/// Привязывает громкость AudioSource к настройкам игрока.
/// Вешается на любой AudioSource (музыкальный или SFX). В инспекторе выбираете
/// channel — Music или Sfx — и базовую громкость; итоговая = base * setting.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioVolumeBinder : MonoBehaviour
{
    public enum Channel { Music, Sfx }

    [SerializeField] private Channel channel = Channel.Sfx;
    [Range(0f, 1f)]
    [SerializeField] private float baseVolume = 1f;

    private AudioSource source;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        Apply();
        SettingsManager.OnMusicVolumeChanged += OnMusicChanged;
        SettingsManager.OnSfxVolumeChanged += OnSfxChanged;
    }

    private void OnDisable()
    {
        SettingsManager.OnMusicVolumeChanged -= OnMusicChanged;
        SettingsManager.OnSfxVolumeChanged -= OnSfxChanged;
    }

    private void OnMusicChanged(float _) { if (channel == Channel.Music) Apply(); }
    private void OnSfxChanged(float _) { if (channel == Channel.Sfx) Apply(); }

    private void Apply()
    {
        if (source == null) return;
        float setting = channel == Channel.Music
            ? SettingsManager.MusicVolume
            : SettingsManager.SfxVolume;
        source.volume = baseVolume * setting;
    }
}
