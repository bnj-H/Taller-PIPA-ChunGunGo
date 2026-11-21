using System;
using System.Collections.Generic;
using UnityEngine;

public enum RoomType
{
    Start,
    Combat,
    Final
}

/// <summary>
/// Controls a single dungeon room: combat triggers, doors, and enemy encounters.
/// </summary>
public class DungeonRoom : MonoBehaviour
{
    public Vector2Int GridPos { get; private set; }
    public RoomType Type { get; private set; }
    public bool IsCleared { get; private set; }
    public bool IsDiscovered { get; private set; }
    public bool IsActiveRoom { get; private set; }

    private readonly List<DungeonDoor> doors = new List<DungeonDoor>();
    private readonly List<Health> aliveEnemies = new List<Health>();
    private GameObject enemyPrefab;
    private GameObject portalObject;
    private int enemyCount = 3;
    private Vector2 roomSize = new Vector2(24f, 16f);
    private bool combatStarted;

    public int AliveEnemyCount => aliveEnemies.Count;

    public void Setup(Vector2Int gridPos, RoomType type, Vector2 size, GameObject enemyPref, int enemies)
    {
        GridPos = gridPos;
        Type = type;
        roomSize = size;
        enemyPrefab = enemyPref;
        enemyCount = enemies;

        if (Type == RoomType.Start)
        {
            IsCleared = true;
            IsDiscovered = true;
        }
    }

    public void AddDoor(DungeonDoor door)
    {
        if (door != null && !doors.Contains(door))
            doors.Add(door);
    }

    public void SetPortalObject(GameObject portal)
    {
        portalObject = portal;
        if (portalObject != null)
            portalObject.SetActive(false);
    }

    public void MarkDiscovered()
    {
        IsDiscovered = true;
    }

    public void OnPlayerEnter()
    {
        IsActiveRoom = true;
        IsDiscovered = true;

        if (DungeonManager.Instance != null)
            DungeonManager.Instance.NotifyRoomEntered(this);

        if (!IsCleared && !combatStarted)
        {
            if (Type == RoomType.Combat || Type == RoomType.Final)
            {
                StartCombat();
            }
        }
    }

    public void OnPlayerExit()
    {
        IsActiveRoom = false;
    }

    private void StartCombat()
    {
        combatStarted = true;
        LockAllDoors();
        SpawnRoomEnemies();
    }

    private void LockAllDoors()
    {
        for (int i = 0; i < doors.Count; i++)
        {
            if (doors[i] != null) doors[i].Lock();
        }
    }

    private void UnlockAllDoors()
    {
        for (int i = 0; i < doors.Count; i++)
        {
            if (doors[i] != null) doors[i].Unlock();
        }
    }

    private void SpawnRoomEnemies()
    {
        if (enemyPrefab == null)
        {
            ClearRoom();
            return;
        }

        int count = enemyCount;
        float halfW = roomSize.x * 0.35f;
        float halfD = roomSize.y * 0.35f;

        for (int i = 0; i < count; i++)
        {
            float offsetX = UnityEngine.Random.Range(-halfW, halfW);
            float offsetZ = UnityEngine.Random.Range(-halfD, halfD);
            Vector3 spawnPos = transform.position + new Vector3(offsetX, 0.5f, offsetZ);

            GameObject enemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity, transform);
            enemy.name = $"Enemy_Room_{GridPos.x}_{GridPos.y}_{i}";

            if (Type == RoomType.Final && i == 0)
            {
                enemy.transform.localScale = Vector3.one * 1.6f;
                enemy.name = "Champion_Crab_Guard";
            }

            Health health = enemy.GetComponent<Health>();
            if (health != null)
            {
                aliveEnemies.Add(health);
                health.Died += OnEnemyDied;
            }
        }

        if (aliveEnemies.Count == 0)
        {
            ClearRoom();
        }
    }

    private void OnEnemyDied(Health enemy)
    {
        if (enemy != null)
        {
            enemy.Died -= OnEnemyDied;
            aliveEnemies.Remove(enemy);
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddScore(25);
            }
        }

        if (combatStarted && aliveEnemies.Count == 0 && !IsCleared)
        {
            ClearRoom();
        }
    }

    private void ClearRoom()
    {
        IsCleared = true;
        UnlockAllDoors();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddScore(100);
        }

        if (Type == RoomType.Final && portalObject != null)
        {
            portalObject.SetActive(true);
        }

        if (DungeonManager.Instance != null)
        {
            DungeonManager.Instance.NotifyRoomCleared(this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponentInParent<PlayerController>() != null)
        {
            OnPlayerEnter();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponentInParent<PlayerController>() != null)
        {
            OnPlayerExit();
        }
    }
}
