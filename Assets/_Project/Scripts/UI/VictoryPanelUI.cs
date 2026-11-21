using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Displays the victory celebration screen when Level 1 is cleared,
/// showing stats and a restart button to generate a new layout.
/// </summary>
public class VictoryPanelUI : MonoBehaviour
{
    [Header("UI Texts")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text statsSummaryText;
    [SerializeField] private TMP_Text finalScoreText;
    [SerializeField] private Button playAgainButton;

    private void Awake()
    {
        if (playAgainButton != null)
        {
            playAgainButton.onClick.AddListener(RestartGame);
        }
    }

    private void OnEnable()
    {
        UpdateStats();
    }

    public void UpdateStats()
    {
        if (DungeonManager.Instance == null) return;

        float elapsed = DungeonManager.Instance.ElapsedTime;
        int minutes = Mathf.FloorToInt(elapsed / 60f);
        int seconds = Mathf.FloorToInt(elapsed % 60f);

        int rooms = DungeonManager.Instance.ClearedRooms;
        int totalRooms = DungeonManager.Instance.TotalRooms;
        int score = GameManager.Instance != null ? GameManager.Instance.Score : 0;

        if (titleText != null)
        {
            titleText.text = "¡NIVEL 1 COMPLETADO!";
        }

        if (statsSummaryText != null)
        {
            statsSummaryText.text = $"Tiempo: {minutes:00}:{seconds:00}\nSalas despejadas: {rooms}/{totalRooms}\nEnemigos eliminados: {DungeonManager.Instance.TotalKills}";
        }

        if (finalScoreText != null)
        {
            finalScoreText.text = $"Puntaje final: {score}";
        }
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
