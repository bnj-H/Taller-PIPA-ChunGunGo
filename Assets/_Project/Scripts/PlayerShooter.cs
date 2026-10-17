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
            nextShotTime = Time.time + 1f / fireRate;
        }
    }

    private void Shoot()
    {
        Instantiate(projectilePrefab, muzzle.position, muzzle.rotation);
        AudioManager.Instance.PlayShoot();
    }
}