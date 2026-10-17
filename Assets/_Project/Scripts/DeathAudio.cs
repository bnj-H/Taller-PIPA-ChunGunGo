using UnityEngine;

[RequireComponent(typeof(Health))]
public class DeathAudio : MonoBehaviour
{
    [SerializeField] private bool isPlayer;

    private void Awake()
    {
        GetComponent<Health>().Died += OnDied;
    }

    private void OnDied(Health health)
    {
        if (health.FellOff)
            AudioManager.Instance.PlayFall();
        else if (!isPlayer)
            AudioManager.Instance.PlayEnemyDeath();
    }
}