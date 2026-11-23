using System;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    private const string MasterVolumeKey = "ChunGunGo.Audio.Master";
    private const string MusicVolumeKey = "ChunGunGo.Audio.Music";
    private const string SfxVolumeKey = "ChunGunGo.Audio.SFX";
    private static bool volumesLoaded;
    private static float masterVolume = 1f;
    private static float musicVolume = 1f;
    private static float sfxVolume = 1f;
    private static AudioManager instance;
    public static AudioManager Instance
    {
        get
        {
            if (instance == null) instance = FindObjectOfType<AudioManager>();
            return instance;
        }
        private set { instance = value; }
    }

    [Header("Fuentes")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource musicSource;

    [Header("Efectos")]
    [SerializeField] private AudioClip shoot;
    [SerializeField] private AudioClip hit;
    [SerializeField] private AudioClip enemyDeath;
    [SerializeField] private AudioClip playerHurt;
    [SerializeField] private AudioClip fall;
    [SerializeField] private AudioClip gameOver;

    private void Awake()
    {
        Instance = this;
        ApplySavedVolumes();
        if (musicSource != null && musicSource.clip != null)
            musicSource.Play();
    }

    public static float MasterVolume { get { LoadSavedVolumes(); return masterVolume; } }
    public static float MusicVolume { get { LoadSavedVolumes(); return musicVolume; } }
    public static float SfxVolume { get { LoadSavedVolumes(); return sfxVolume; } }

    public static void SetMasterVolume(float value)
    {
        LoadSavedVolumes();
        masterVolume = Mathf.Clamp01(value);
        TryStoreVolume(MasterVolumeKey, masterVolume);
        AudioListener.volume = MasterVolume;
    }

    public static void SetMusicVolume(float value)
    {
        LoadSavedVolumes();
        musicVolume = Mathf.Clamp01(value);
        TryStoreVolume(MusicVolumeKey, musicVolume);
        if (Instance != null && Instance.musicSource != null)
            Instance.musicSource.volume = MusicVolume;
    }

    public static void SetSfxVolume(float value)
    {
        LoadSavedVolumes();
        sfxVolume = Mathf.Clamp01(value);
        TryStoreVolume(SfxVolumeKey, sfxVolume);
        if (Instance != null && Instance.sfxSource != null)
            Instance.sfxSource.volume = SfxVolume;
    }

    private static void LoadSavedVolumes()
    {
        if (volumesLoaded) return;
        volumesLoaded = true;
        try
        {
            masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
            musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
            sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
        }
        catch (Exception)
        {
            // Keep usable in-memory defaults if the OS denies preference storage.
        }
    }

    private static void TryStoreVolume(string key, float value)
    {
        try { PlayerPrefs.SetFloat(key, value); }
        catch (Exception)
        {
            // Sliders still apply through cached values/audio sources for this session.
        }
    }

    private void ApplySavedVolumes()
    {
        AudioListener.volume = MasterVolume;
        if (musicSource != null) musicSource.volume = MusicVolume;
        if (sfxSource != null) sfxSource.volume = SfxVolume;
    }

    private void OnApplicationQuit()
    {
        try { PlayerPrefs.Save(); }
        catch (Exception) { }
    }

    public void PlayShoot() => Play(shoot, 0.85f);
    public void PlayHit() => Play(hit, 0.45f);
    public void PlayEnemyDeath() => Play(enemyDeath, 0.6f);
    public void PlayPlayerHurt() => Play(playerHurt, 0.5f);
    public void PlayFall() => Play(fall, 0.8f);
    public void PlayGameOver() => Play(gameOver, 0.8f);

    public void StopMusic()
    {
        if (musicSource != null) musicSource.Stop();
    }

    private void Play(AudioClip clip, float volume)
    {
        if (clip != null && sfxSource != null)
            sfxSource.PlayOneShot(clip, volume);
    }
}
