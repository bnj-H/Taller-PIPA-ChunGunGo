using UnityEngine;

public class EnemyContactDamage : MonoBehaviour
{
    [SerializeField] private int damage = 1;
    [SerializeField] private float cooldown = 1f;

    private float nextHitTime;

    public void SetDamage(int value) => damage = Mathf.Max(0, value);

    private void OnCollisionStay(Collision collision)
    {
        if (Time.time < nextHitTime) return;
        if (!collision.collider.CompareTag("Player")) return;

        if (collision.collider.TryGetComponent(out IDamageable target))
        {
            if (target.TakeDamage(damage))
            {
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayPlayerHurt();
            }
            nextHitTime = Time.time + cooldown;
        }
    }
}
