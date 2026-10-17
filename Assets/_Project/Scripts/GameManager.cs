using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Referencias")]
    [SerializeField] private EnemySpawner spawner;
    [SerializeField] private Health playerHealth;

    [Header("Puntaje")]
    [SerializeField] private int pointsPerKill = 10;
    [SerializeField] private int pointsPerFall = 15;
    [SerializeField] private int score;

    [Header("Game Over")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TMP_Text finalScoreText;
    [SerializeField] private Button restartButton;

    public int Score => score;
    public bool IsPlaying { get; private set; }
    public EnemySpawner Spawner => spawner;
    public Health PlayerHealth => playerHealth;

    private void Awake()
    {
        Instance = this;
        IsPlaying = true;
        Time.timeScale = 1f;

        gameOverPanel.SetActive(false);
        restartButton.onClick.AddListener(Restart);
    }

    private void OnEnable()
    {
        spawner.EnemyDied += OnEnemyDied;
        playerHealth.Died += OnPlayerDied;
    }

    private void OnDisable()
    {
        if (spawner != null) spawner.EnemyDied -= OnEnemyDied;
        if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
    }

    private void OnEnemyDied(Health enemy)
    {
        if (!IsPlaying) return;
        score += enemy.FellOff ? pointsPerFall : pointsPerKill;
    }

    private void OnPlayerDied(Health player)
    {
        if (!IsPlaying) return;
        IsPlaying = false;

        AudioManager.Instance.StopMusic();
        AudioManager.Instance.PlayGameOver();

        finalScoreText.text = "Puntaje final: " + score;
        gameOverPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}