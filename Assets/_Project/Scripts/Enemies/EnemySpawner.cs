using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private GameObject enemyPrefab;
    [Header("Spawn seguro sobre chunks")]
    [FormerlySerializedAs("minimumPlayerDistance")]
    [SerializeField, Min(0f)] private float minSpawnDistance = 12f;
    [FormerlySerializedAs("spawnRadius")]
    [SerializeField, Min(0f)] private float maxSpawnDistance = 24f;
    [FormerlySerializedAs("spawnPositionAttempts")]
    [SerializeField, Min(1)] private int spawnAttempts = 32;
    [SerializeField] private LayerMask spawnGroundMask = 1 << 9;
    [SerializeField] private Collider islandGround;
    [FormerlySerializedAs("spawnHeight")]
    [SerializeField] private float spawnHeightOffset = 0.02f;
    [SerializeField, Min(0f)] private float groundY;
    [SerializeField, Min(1f)] private float groundRayStartHeight = 25f;
    [SerializeField, Min(0f)] private float groundHeightTolerance = 0.25f;
    [SerializeField] private Vector3 spawnBlockHalfExtents = new Vector3(0.8f, 1.05f, 0.8f);
    [SerializeField, Min(0f)] private float worldBoundaryPadding = 3f;
    [Header("Oleadas")]
    [SerializeField, Min(0.01f)] private float spawnInterval = 0.4f;
    [SerializeField, Min(0f)] private float timeBetweenWaves = 3f;
    [SerializeField, Min(1)] private int maxAliveEnemies = 16;
    [SerializeField, Min(1)] private int baseEnemiesPerWave = 10;
    [SerializeField, Min(1)] private int enemiesAddedPerWave = 5;
    [SerializeField] private Vector2 arenaHalfExtents = new Vector2(19f, 19f);

    [SerializeField] private int wave;
    private int alive;
    private int enemiesRemainingInWave;
    private int enemiesToSpawn;
    private float nextSpawnFailureLogTime;
    private string lastSpawnFailureSummary;
    private Transform player;
    private WorldChunkManager worldChunks;
    public event Action<Health> EnemyDied;
    public int Wave => wave;

    public void SetArenaHalfExtents(Vector2 extents)
    {
        arenaHalfExtents = new Vector2(Mathf.Max(0f, extents.x), Mathf.Max(0f, extents.y));
    }

    private void Start()
    {
        if (spawnGroundMask.value == 0)
            spawnGroundMask = LayerMask.GetMask("Ground");
        worldChunks = FindObjectOfType<WorldChunkManager>();

        if (islandGround == null)
        {
            GameObject island = GameObject.Find("Island");
            if (island != null) islandGround = island.GetComponent<Collider>();
        }
        if (islandGround != null)
        {
            spawnGroundMask |= 1 << islandGround.gameObject.layer;
            if (!islandGround.enabled || !islandGround.gameObject.activeInHierarchy)
                Debug.LogError("EnemySpawner: el collider de Island está desactivado; no puede usarse como suelo.", islandGround);
        }
        else
            Debug.LogError("EnemySpawner: no se encontró el collider del objeto Island para usarlo como suelo.", this);

        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null) player = playerObject.transform;
        else Debug.LogWarning("EnemySpawner: no se encontró un objeto con tag Player.", this);

        if (spawnGroundMask.value == 0)
            Debug.LogError("EnemySpawner: spawnGroundMask no incluye ninguna capa de suelo.", this);
        StartCoroutine(WaveRoutine());
    }

    private IEnumerator WaveRoutine()
    {
        yield return new WaitForSeconds(timeBetweenWaves);
        while (true)
        {
            wave++;
            enemiesRemainingInWave = baseEnemiesPerWave + enemiesAddedPerWave * wave;
            enemiesToSpawn = enemiesRemainingInWave;

            // Keep filling free slots until the wave quota is spawned. Falling
            // enemies are queued for replacement; actual kills advance the wave.
            while (enemiesRemainingInWave > 0)
            {
                yield return new WaitUntil(() => enemiesRemainingInWave <= 0 ||
                    (enemiesToSpawn > 0 && alive < maxAliveEnemies));
                if (enemiesRemainingInWave <= 0) break;

                if (SpawnEnemy()) enemiesToSpawn--;
                yield return new WaitForSeconds(spawnInterval);
            }

            yield return new WaitUntil(() => alive <= 0);
            yield return new WaitForSeconds(timeBetweenWaves);
        }
    }
    private bool SpawnEnemy()
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("EnemySpawner: falta asignar Enemy Prefab.", this);
            return false;
        }

        Vector3 center = player != null ? player.position : transform.position;
        if (!TryFindValidGround(center, out Vector3 groundPoint))
        {
            if (Time.unscaledTime >= nextSpawnFailureLogTime)
            {
                Debug.LogWarning($"EnemySpawner: no encontró suelo válido ({lastSpawnFailureSummary}); reintentará en el próximo intervalo.", this);
                nextSpawnFailureLogTime = Time.unscaledTime + 3f;
            }
            return false;
        }

        GameObject enemy = Instantiate(enemyPrefab, groundPoint, Quaternion.identity);
        Physics.SyncTransforms();
        if (!enemy.TryGetComponent(out Health health))
        {
            Debug.LogError("EnemySpawner: el prefab instanciado no tiene Health; se cancela y se reintentará.", enemy);
            Destroy(enemy);
            return false;
        }

        // Place the bottom of the real collider on the raycast surface. The prefab's
        // pivot and collider center may differ, so a fixed root offset is unreliable.
        Collider[] colliders = enemy.GetComponentsInChildren<Collider>();
        float lowestColliderY = float.PositiveInfinity;
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null || !colliders[i].enabled || colliders[i].isTrigger) continue;
            lowestColliderY = Mathf.Min(lowestColliderY, colliders[i].bounds.min.y);
        }

        float targetColliderBottom = groundPoint.y + spawnHeightOffset;
        if (float.IsPositiveInfinity(lowestColliderY))
            enemy.transform.position = groundPoint + Vector3.up * (spawnHeightOffset + 0.5f);
        else
            enemy.transform.position += Vector3.up * (targetColliderBottom - lowestColliderY);

        Physics.SyncTransforms();
        alive++;
        health.Died += OnEnemyDied;
        return true;
    }
    private bool TryFindValidGround(Vector3 center, out Vector3 groundPoint)
    {
        groundPoint = Vector3.zero;
        float minDistance = Mathf.Max(0f, minSpawnDistance);
        float maxDistance = Mathf.Max(minDistance, maxSpawnDistance);
        float maxX = Mathf.Max(0f, arenaHalfExtents.x - worldBoundaryPadding);
        float maxZ = Mathf.Max(0f, arenaHalfExtents.y - worldBoundaryPadding);
        float rayHeight = Mathf.Max(groundRayStartHeight, spawnHeightOffset + 5f);
        int rejectedBounds = 0;
        int rejectedChunks = 0;
        int rejectedGround = 0;
        int rejectedObstacles = 0;

        for (int attempt = 0; attempt < spawnAttempts; attempt++)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float distance = UnityEngine.Random.Range(minDistance, maxDistance);
            Vector3 candidate = center + new Vector3(Mathf.Cos(angle) * distance, 0f,
                Mathf.Sin(angle) * distance);
            if (Mathf.Abs(candidate.x) > maxX || Mathf.Abs(candidate.z) > maxZ)
            {
                rejectedBounds++;
                continue;
            }
            Ray ray = new Ray(new Vector3(candidate.x, groundY + rayHeight, candidate.z), Vector3.down);
            RaycastHit[] hits = Physics.RaycastAll(ray, rayHeight * 2f, spawnGroundMask,
                QueryTriggerInteraction.Ignore);
            bool foundGround = false;
            float closestHeightDifference = float.MaxValue;
            Vector3 candidateGround = Vector3.zero;
            bool rejectedForInactiveChunk = false;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null || hit.normal.y < 0.6f) continue;

                // Island is the continuous fallback floor under streamed chunks.
                // It remains valid even where decorative chunks are not loaded.
                bool isIslandFloor = hit.collider == islandGround;
                if (!isIslandFloor && worldChunks != null && !worldChunks.IsChunkLoadedAt(candidate))
                {
                    rejectedForInactiveChunk = true;
                    continue;
                }

                float heightDifference = Mathf.Abs(hit.point.y - groundY);
                if (heightDifference > groundHeightTolerance || heightDifference >= closestHeightDifference) continue;
                closestHeightDifference = heightDifference;
                candidateGround = hit.point;
                foundGround = true;
            }

            if (!foundGround)
            {
                if (rejectedForInactiveChunk) rejectedChunks++;
                else rejectedGround++;
                continue;
            }
            if (IsSpawnBlocked(candidateGround))
            {
                rejectedObstacles++;
                continue;
            }

            groundPoint = candidateGround;
            lastSpawnFailureSummary = null;
            return true;
        }

        lastSpawnFailureSummary = $"{rejectedBounds} fuera de límites, {rejectedChunks} en chunk inactivo, {rejectedGround} sin suelo raycasteable, {rejectedObstacles} bloqueados";
        return false;
    }
    private bool IsSpawnBlocked(Vector3 groundPoint)
    {
        Vector3 halfExtents = new Vector3(
            Mathf.Max(0.1f, spawnBlockHalfExtents.x),
            Mathf.Max(0.1f, spawnBlockHalfExtents.y),
            Mathf.Max(0.1f, spawnBlockHalfExtents.z));
        Vector3 center = groundPoint + Vector3.up * (spawnHeightOffset + 1f);
        Collider[] overlaps = Physics.OverlapBox(center, halfExtents, Quaternion.identity,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider obstacle = overlaps[i];
            if (obstacle == null) continue;
            int obstacleBit = 1 << obstacle.gameObject.layer;
            if ((spawnGroundMask.value & obstacleBit) != 0) continue;
            if (player != null && (obstacle.transform == player || obstacle.transform.IsChildOf(player))) continue;
            return true;
        }

        return false;
    }

    private void OnEnemyDied(Health health)
    {
        alive = Mathf.Max(0, alive - 1);
        if (health != null && !health.FellOff)
            enemiesRemainingInWave = Mathf.Max(0, enemiesRemainingInWave - 1);
        else if (health != null && health.FellOff)
        {
            enemiesToSpawn++;
            Debug.LogWarning("EnemySpawner: un enemigo cayó del mapa; se generará un reemplazo para completar la oleada.", this);
        }
        EnemyDied?.Invoke(health);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 center = player != null ? player.position : transform.position;
        Gizmos.DrawWireSphere(center, maxSpawnDistance);
        Gizmos.DrawWireSphere(center, minSpawnDistance);
    }
}