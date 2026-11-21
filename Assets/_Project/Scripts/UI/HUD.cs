using TMPro;
using UnityEngine;

public class HUD : MonoBehaviour
{
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private TMP_Text healthText;

    private void Update()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        if (scoreText != null)
            scoreText.text = "Puntaje: " + gm.Score;

        if (waveText != null)
        {
            if (DungeonManager.Instance != null)
            {
                int current = Mathf.Min(DungeonManager.Instance.ClearedRooms + 1, DungeonManager.Instance.TotalRooms);
                waveText.text = $"Nivel 1: Sala {current}/{DungeonManager.Instance.TotalRooms}";
            }
            else if (gm.Spawner != null)
            {
                int wave = gm.Spawner.Wave;
                waveText.text = wave == 0 ? "Prepárate..." : "Oleada " + wave;
            }
        }

        Health player = gm.PlayerHealth;
        float hp = player != null ? player.Current : 0f;
        if (healthText != null)
            healthText.text = "Vida: " + hp.ToString("0.#");
    }
}
