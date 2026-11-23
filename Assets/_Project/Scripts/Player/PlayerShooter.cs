using UnityEngine;

public class PlayerShooter : MonoBehaviour
{
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private Transform muzzle;
    [SerializeField] private float fireRate = 4f;

    private float nextShotTime;
    private PlayerController playerController;
    private Transform weaponVisual;

    /// <summary>The actual world-space direction the next projectile will use.</summary>
    public Vector3 CurrentShotDirection => ResolveShotDirection();

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        if (muzzle != null)
            weaponVisual = muzzle.parent;
    }

    private void LateUpdate()
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsPlaying) return;
        Vector3 direction = ResolveShotDirection();
        if (direction.sqrMagnitude < 0.001f) return;

        Quaternion aimRotation = Quaternion.LookRotation(direction, Vector3.up);
        // Character_Gun's barrel axis is local -Y; turn that axis toward the aim ray.
        if (weaponVisual != null)
            weaponVisual.rotation = aimRotation * Quaternion.Euler(-90f, 0f, 0f);
        // FirePoint always has +Z aligned with the gameplay aim direction.
        if (muzzle != null)
            muzzle.rotation = aimRotation;
    }

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

        Vector3 shotDirection = ResolveShotDirection();
        if (shotDirection.sqrMagnitude < 0.001f) return;
        Quaternion shotRotation = Quaternion.LookRotation(shotDirection, Vector3.up);
        Projectile projectile = Instantiate(projectilePrefab, muzzle.position, shotRotation);
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

    private Vector3 ResolveShotDirection()
    {
        Vector3 direction = playerController != null ? playerController.AimDirection : transform.forward;
        direction.y = 0f;
        return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
    }
}
