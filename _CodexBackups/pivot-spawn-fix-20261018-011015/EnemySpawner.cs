using System;
using System.Collections;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private GameObject enemyPrefab;
    [Header("Camera-edge spawning")]
    [SerializeField] private Camera worldCamera;
    [SerializeField, Range(0.02f, 0.25f)] private float viewportEdgeInset = 0.08f;
    [SerializeField, Min(0f)] private float minimumPlayerDistance = 6f;
    [SerializeField, Min(0f)] private float spawnRadius = 14f;
    [SerializeField, Min(0f)] private float spawnHeight = 1.6f;
    [SerializeField, Min(0f)] private float groundY;
    [SerializeField, Min(1)] private int spawnPositionAttempts = 48;
    [SerializeField, Min(0f)] private float groundRayStartHeight = 30f;
    [SerializeField, Min(0f)] private float groundHeightTolerance = 0.25f;
    [SerializeField] private Vector3 spawnBlockHalfExtents = new Vector3(0.55f, 0.7f, 0.55f);
    [SerializeField, Min(0f)] private float worldBoundaryPadding = 2f;
    [Header("Waves")]
    [SerializeField] private float spawnInterval = 0.4f;
    [SerializeField] private float timeBetweenWaves = 3f;
    [SerializeField, Min(1)] private int maxAliveEnemies = 16;
    [SerializeField] private Vector2 arenaHalfExtents = new Vector2(19f, 19f);

    [SerializeField] private int wave;
    private int alive;
    private Transform player;
    public event Action<Health> EnemyDied;
    public int Wave => wave;

    public void SetArenaHalfExtents(Vector2 extents)
    {
        arenaHalfExtents = new Vector2(Mathf.Max(0f, extents.x), Mathf.Max(0f, extents.y));
    }

    private void Start()
    {
        if (worldCamera == null) worldCamera = Camera.main;
        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null) player = playerObject.transform;
        else Debug.LogWarning("EnemySpawner: no se encontró un objeto con tag Player.", this);

        if (worldCamera == null)
            Debug.LogWarning("EnemySpawner: no se encontró MainCamera; se usará un spawn circular de respaldo.", this);

        StartCoroutine(WaveRoutine());
    }

    private IEnumerator WaveRoutine()
    {
        yield return new WaitForSeconds(timeBetweenWaves);

        while (true)
        {
            wave++;
            int count = 3 + 2 * wave;

            for (int i = 0; i < count; i++)
            {
                // Limita el costo de IA y física aunque las oleadas sigan aumentando.
                yield return new WaitUntil(() => alive < maxAliveEnemies);
                SpawnEnemy();
                yield return new WaitForSeconds(spawnInterval);
            }

            yield return new WaitUntil(() => alive <= 0);
            yield return new WaitForSeconds(timeBetweenWaves);
        }
    }

    private void SpawnEnemy()
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("EnemySpawner: falta asignar Enemy Prefab.", this);
            return;
        }

        Vector3 center = player != null ? player.position : transform.position;
        if (!TryFindSpawnGround(center, out Vector3 groundPoint))
        {
            Debug.LogWarning("EnemySpawner: no se encontró suelo cargado y libre junto al borde de cámara; se omite esta aparición.", this);
            return;
        }

        Vector3 spawnPosition = groundPoint + Vector3.up * spawnHeight;
        GameObject enemy = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);

        if (enemy.TryGetComponent(out Health health))
        {
            alive++;
            health.Died += OnEnemyDied;
        }
        else
        {
            Debug.LogWarning("EnemySpawner: el prefab enemigo no tiene componente Health.", enemy);
        }
    }

    private bool TryFindSpawnGround(Vector3 center, out Vector3 groundPoint)
    {
        groundPoint = Vector3.zero;
        float minimumDistance = Mathf.Max(minimumPlayerDistance,
            worldCamera != null && worldCamera.orthographic ? worldCamera.orthographicSize * 0.65f : minimumPlayerDistance);
        float maxX = Mathf.Max(0f, arenaHalfExtents.x - worldBoundaryPadding);
        float maxZ = Mathf.Max(0f, arenaHalfExtents.y - worldBoundaryPadding);

        for (int attempt = 0; attempt < spawnPositionAttempts; attempt++)
        {
            Vector3 candidate;
            if (worldCamera != null)
            {
                if (!TryGetCameraEdgePoint(out candidate)) continue;
            }
            else
            {
                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float distance = Mathf.Max(minimumDistance, spawnRadius);
                candidate = center + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
            }

            // Cuando el encuadre alcanza el límite del mapa, trae el punto hacia el interior.
            candidate.x = Mathf.Clamp(candidate.x, -maxX, maxX);
            candidate.z = Mathf.Clamp(candidate.z, -maxZ, maxZ);
            if (player != null && HorizontalDistance(candidate, player.position) < minimumDistance)
                continue;
            if (!TryGetLoadedGround(candidate, out Vector3 surfacePoint)) continue;
            if (IsSpawnBlocked(surfacePoint)) continue;

            groundPoint = surfacePoint;
            return true;
        }

        return false;
    }

    private bool TryGetCameraEdgePoint(out Vector3 point)
    {
        point = Vector3.zero;
        float inset = Mathf.Clamp(viewportEdgeInset, 0.02f, 0.25f);
        float along = UnityEngine.Random.Range(inset, 1f - inset);
        int edge = UnityEngine.Random.Range(0, 4);
        Vector3 viewportPoint;
        switch (edge)
        {
            case 0: viewportPoint = new Vector3(inset, along, 0f); break;
            case 1: viewportPoint = new Vector3(1f - inset, along, 0f); break;
            case 2: viewportPoint = new Vector3(along, inset, 0f); break;
            default: viewportPoint = new Vector3(along, 1f - inset, 0f); break;
        }

        Ray ray = worldCamera.ViewportPointToRay(viewportPoint);
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, groundY, 0f));
        if (!groundPlane.Raycast(ray, out float distance) || distance < 0f) return false;
        point = ray.GetPoint(distance);
        return true;
    }

    private bool TryGetLoadedGround(Vector3 candidate, out Vector3 surfacePoint)
    {
        surfacePoint = Vector3.zero;
        float rayHeight = Mathf.Max(groundRayStartHeight, spawnHeight + 5f);
        Ray ray = new Ray(new Vector3(candidate.x, groundY + rayHeight, candidate.z), Vector3.down);
        RaycastHit[] hits = Physics.RaycastAll(ray, rayHeight * 2f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        float bestDifference = float.MaxValue;
        bool found = false;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null || hit.collider.gameObject.name != "Ground") continue;
            if (hit.normal.y < 0.6f) continue;
            float difference = Mathf.Abs(hit.point.y - groundY);
            if (difference > groundHeightTolerance || difference >= bestDifference) continue;

            bestDifference = difference;
            surfacePoint = hit.point;
            found = true;
        }

        return found;
    }

    private bool IsSpawnBlocked(Vector3 groundPoint)
    {
        Vector3 halfExtents = new Vector3(
            Mathf.Max(0.1f, spawnBlockHalfExtents.x),
            Mathf.Max(0.1f, spawnBlockHalfExtents.y),
            Mathf.Max(0.1f, spawnBlockHalfExtents.z));
        Vector3 center = groundPoint + Vector3.up * spawnHeight;
        Collider[] overlaps = Physics.OverlapBox(center, halfExtents, Quaternion.identity,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider obstacle = overlaps[i];
            if (obstacle == null || obstacle.gameObject.name == "Ground" || obstacle.gameObject.name == "Island") continue;
            if (player != null && (obstacle.transform == player || obstacle.transform.IsChildOf(player))) continue;
            return true;
        }

        return false;
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void OnEnemyDied(Health health)
    {
        alive = Mathf.Max(0, alive - 1);
        EnemyDied?.Invoke(health);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}