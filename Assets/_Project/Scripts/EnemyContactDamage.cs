using UnityEngine;

public class EnemyContactDamage : MonoBehaviour
{
    [SerializeField] private int damage = 1;
    [SerializeField] private float cooldown = 1f;

    private float nextHitTime;

    private void OnCollisionStay(Collision collision)
    {
        if (Time.time < nextHitTime) return;
        if (!collision.collider.CompareTag("Player")) return;

        if (collision.collider.TryGetComponent(out IDamageable target))
        {
            if (target.TakeDamage(damage))
                AudioManager.Instance.PlayPlayerHurt();
            nextHitTime = Time.time + cooldown;
        }
    }
}