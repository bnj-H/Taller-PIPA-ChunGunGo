using UnityEngine;

namespace ShotFixPatch
{
/// <summary>
/// Dispara proyectiles en la dirección a la que apunta el jugador.
/// Evita depender de la rotación local del punto Muzzle.
/// </summary>
public class PlayerShooter : MonoBehaviour
{
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private Transform muzzle;
    [SerializeField, Min(0.1f)] private float fireRate = 4f;
    [SerializeField, Min(0f)] private float fallbackForwardOffset = 0.7f;
    [SerializeField, Min(0f)] private float fallbackHeightOffset = 1f;

    private float nextShotTime;
    private Collider[] shooterColliders;

    private void Awake()
    {
        shooterColliders = GetComponentsInChildren<Collider>();
    }

    private void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying)
            return;

        if (Input.GetMouseButton(0) && Time.time >= nextShotTime)
        {
            Shoot();
            nextShotTime = Time.time + 1f / Mathf.Max(0.1f, fireRate);
        }
    }

    private void Shoot()
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("PlayerShooter: falta asignar Projectile Prefab en el Inspector.", this);
            return;
        }

        // PlayerController rota el objeto raíz hacia el punto donde apunta el ratón.
        Vector3 direction = transform.forward;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            return;

        direction.Normalize();
        Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);

        // Usamos la posición de Muzzle, pero no su rotación local, que podría estar desalineada.
        Vector3 origin = muzzle != null
            ? muzzle.position
            : transform.position + Vector3.up * fallbackHeightOffset + direction * fallbackForwardOffset;

        Projectile projectile = Instantiate(projectilePrefab, origin, rotation);

        // Impide que el proyectil choque inmediatamente con el propio jugador.
        if (projectile != null)
        {
            Collider[] projectileColliders = projectile.GetComponentsInChildren<Collider>();
            foreach (Collider projectileCollider in projectileColliders)
            {
                if (projectileCollider == null) continue;

                foreach (Collider shooterCollider in shooterColliders)
                {
                    if (shooterCollider != null)
                        Physics.IgnoreCollision(projectileCollider, shooterCollider, true);
                }
            }
        }

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayShoot();
    }
}
}
