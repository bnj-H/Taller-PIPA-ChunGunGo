using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Projectile : MonoBehaviour
{
    [SerializeField] private float speed = 20f;
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private int damage = 1;
    [SerializeField] private float knockbackForce = 8f;

    private void Start()
    {
        GetComponent<Rigidbody>().velocity = transform.forward * speed;
        Destroy(gameObject, lifetime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.TryGetComponent(out IKnockbackable pushable))
            pushable.ApplyKnockback(transform.forward, knockbackForce);

        if (collision.collider.TryGetComponent(out IDamageable target))
        {
            if (target.TakeDamage(damage))
                AudioManager.Instance.PlayHit();
        }

        Destroy(gameObject);
    }
}