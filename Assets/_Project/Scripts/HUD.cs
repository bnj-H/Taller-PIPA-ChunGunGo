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

        scoreText.text = "Puntaje: " + gm.Score;

        int wave = gm.Spawner.Wave;
        waveText.text = wave == 0 ? "Prepárate..." : "Oleada " + wave;

        Health player = gm.PlayerHealth;
        int hp = player != null ? player.Current : 0;
        healthText.text = "Vida: " + hp;
    }
}