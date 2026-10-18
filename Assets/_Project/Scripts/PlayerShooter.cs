using UnityEngine;

public class PlayerShooter : MonoBehaviour
{
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private Transform muzzle;
    [SerializeField] private float fireRate = 4f;

    private float nextShotTime;

    private void Update()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;
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

        if (muzzle == null)
        {
            Debug.LogWarning("PlayerShooter: falta asignar Muzzle en el Inspector.", this);
            return;
        }

        Projectile projectile = Instantiate(projectilePrefab, muzzle.position, muzzle.rotation);
        if (projectile == null) return;

        Collider[] projectileColliders = projectile.GetComponentsInChildren<Collider>();
        Collider[] shooterColliders = GetComponentsInChildren<Collider>();
        foreach (Collider projectileCollider in projectileColliders)
        {
            if (projectileCollider == null) continue;
            foreach (Collider shooterCollider in shooterColliders)
            {
                if (shooterCollider != null)
                    Physics.IgnoreCollision(projectileCollider, shooterCollider, true);
            }
        }

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayShoot();
    }
}
