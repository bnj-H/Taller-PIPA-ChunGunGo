using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUD : MonoBehaviour
{
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private Image healthFill;

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
        float maxHp = player != null ? player.Max : 1f;
        if (healthText != null)
            healthText.text = hp.ToString("0.#") + " / " + maxHp.ToString("0.#");
        if (healthFill != null)
        {
            float normalized = maxHp > 0f ? Mathf.Clamp01(hp / maxHp) : 0f;
            healthFill.fillAmount = normalized;
            healthFill.color = Color.Lerp(new Color(0.95f, 0.17f, 0.12f), new Color(0.25f, 0.88f, 0.37f), normalized);
        }
    }
}
