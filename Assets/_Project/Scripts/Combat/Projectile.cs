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
            ContactPoint contact = collision.contactCount > 0
                ? collision.GetContact(0)
                : default;

            Vector3 hitPoint = collision.contactCount > 0
                ? contact.point
                : collision.collider.ClosestPoint(transform.position);

            Vector3 hitNormal = collision.contactCount > 0
                ? contact.normal
                : -transform.forward;

            DamageImpactVFX.Play(hitPoint, hitNormal);

            // Mantiene el sonido de impacto también cuando el golpe es letal.
            target.TakeDamage(damage);
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayHit();
        }

        Destroy(gameObject);
    }
}
