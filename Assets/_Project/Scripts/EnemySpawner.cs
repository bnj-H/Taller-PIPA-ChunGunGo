using System.Collections;
using UnityEngine;
using System;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private float spawnRadius = 8f;
    [SerializeField] private float spawnHeight = 1.6f;
    [SerializeField] private float spawnInterval = 0.4f;
    [SerializeField] private float timeBetweenWaves = 3f;

    [SerializeField] private int wave;
    private int alive;
    public event Action<Health> EnemyDied;
    public int Wave => wave;

    private void Start()
    {
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
                SpawnEnemy();
                yield return new WaitForSeconds(spawnInterval);
            }

            yield return new WaitUntil(() => alive <= 0);
            yield return new WaitForSeconds(timeBetweenWaves);
        }
    }

    private void SpawnEnemy()
    {
        float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        Vector3 pos = transform.position + new Vector3(
            Mathf.Cos(angle) * spawnRadius,
            spawnHeight,
            Mathf.Sin(angle) * spawnRadius);

        GameObject enemy = Instantiate(enemyPrefab, pos, Quaternion.identity);

        if (enemy.TryGetComponent(out Health health))
        {
            alive++;
            health.Died += OnEnemyDied;
        }
    }

    private void OnEnemyDied(Health health)
    {
        alive--;
        EnemyDied?.Invoke(health);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}