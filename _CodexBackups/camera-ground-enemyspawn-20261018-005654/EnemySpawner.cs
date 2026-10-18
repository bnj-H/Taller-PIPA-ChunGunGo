using System;
using System.Collections;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private float spawnRadius = 14f;
    [SerializeField] private float spawnHeight = 1.6f;
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
        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null) player = playerObject.transform;
        else Debug.LogWarning("EnemySpawner: no se encontró un objeto con tag Player.", this);

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
        Vector3 pos = center;
        bool foundPosition = false;

        // Elige una posición dentro de la arena para evitar apariciones fuera del suelo.
        for (int attempt = 0; attempt < 16; attempt++)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            Vector3 candidate = center + new Vector3(
                Mathf.Cos(angle) * spawnRadius,
                0f,
                Mathf.Sin(angle) * spawnRadius);

            if (Mathf.Abs(candidate.x) <= arenaHalfExtents.x &&
                Mathf.Abs(candidate.z) <= arenaHalfExtents.y)
            {
                pos = candidate;
                foundPosition = true;
                break;
            }
        }

        if (!foundPosition)
        {
            Vector3 inward = new Vector3(-center.x, 0f, -center.z);
            if (inward.sqrMagnitude < 0.01f)
                inward = UnityEngine.Random.insideUnitSphere;
            inward.y = 0f;
            inward.Normalize();

            float fallbackDistance = Mathf.Min(spawnRadius, 6f);
            pos = center + inward * fallbackDistance;
            pos.x = Mathf.Clamp(pos.x, -arenaHalfExtents.x, arenaHalfExtents.x);
            pos.z = Mathf.Clamp(pos.z, -arenaHalfExtents.y, arenaHalfExtents.y);
        }

        pos.y = spawnHeight;
        GameObject enemy = Instantiate(enemyPrefab, pos, Quaternion.identity);

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
