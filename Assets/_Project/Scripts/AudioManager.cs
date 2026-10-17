using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

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
        if (musicSource != null && musicSource.clip != null)
            musicSource.Play();
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