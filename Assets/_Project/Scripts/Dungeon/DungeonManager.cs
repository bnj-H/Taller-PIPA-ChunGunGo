using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Procedurally generates an Isaac-style interconnected grid of rooms for Level 1,
/// manages room progression, combat triggers, minimap data, and the final room clear.
/// </summary>
public class DungeonManager : MonoBehaviour
{
    public static DungeonManager Instance { get; private set; }

    [Header("Dungeon Configuration")]
    [SerializeField, Range(4, 10)] private int targetRoomCount = 6;
    [SerializeField] private Vector2 roomDimensions = new Vector2(24f, 16f);
    [SerializeField] private float corridorWidth = 4f;
    [SerializeField] private float wallHeight = 3.5f;
    [SerializeField] private float wallThickness = 1.2f;
    [SerializeField] private float spacingX = 32f;
    [SerializeField] private float spacingZ = 24f;

    [Header("Encounter Tuning")]
    [SerializeField] private int enemiesPerCombatRoom = 3;
    [SerializeField] private int enemiesInFinalRoom = 5;

    [Header("Prefabs & Materials")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private GameObject treePrefab;
    [SerializeField] private GameObject bushPrefab;
    [SerializeField] private GameObject rockPrefab;
    [SerializeField] private Material floorMaterial;
    [SerializeField] private Material wallMaterial;
    [SerializeField] private Material doorLockedMaterial;
    [SerializeField] private Material doorOpenMaterial;
    [SerializeField] private Material portalMaterial;

    [Header("UI Reference")]
    [SerializeField] private GameObject victoryPanel;

    private readonly Dictionary<Vector2Int, DungeonRoom> rooms = new Dictionary<Vector2Int, DungeonRoom>();
    private readonly HashSet<Vector2Int> generatedCoords = new HashSet<Vector2Int>();
    private Vector2Int finalRoomCoord;
    private DungeonRoom activeRoom;
    private int clearedRoomsCount;
    private int totalKills;
    private float startTime;
    private bool levelCompleted;

    public int TotalRooms => targetRoomCount;
    public int ClearedRooms => clearedRoomsCount;
    public int TotalKills => totalKills;
    public float ElapsedTime => Time.time - startTime;
    public DungeonRoom ActiveRoom => activeRoom;
    public IReadOnlyDictionary<Vector2Int, DungeonRoom> AllRooms => rooms;
    public Vector2Int FinalRoomCoord => finalRoomCoord;
    public bool IsLevelCompleted => levelCompleted;

    public event Action<DungeonRoom> RoomEntered;
    public event Action<DungeonRoom> RoomCleared;
    public event Action LevelFinished;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        startTime = Time.time;
        LoadAssetsIfMissing();
        GenerateDungeon();
        SetupPlayerPosition();
    }

    private void LoadAssetsIfMissing()
    {
        if (enemyPrefab == null)
            enemyPrefab = Resources.Load<GameObject>("Enemies/Enemy") ??
                          AssetDatabaseLoad<GameObject>("Assets/_Project/Prefabs/Enemies/Enemy.prefab");

        if (treePrefab == null)
            treePrefab = AssetDatabaseLoad<GameObject>("Assets/_Project/Art/Environment/Nature/Tree.fbx");
        if (bushPrefab == null)
            bushPrefab = AssetDatabaseLoad<GameObject>("Assets/_Project/Art/Environment/Nature/Bush.fbx");
        if (rockPrefab == null)
            rockPrefab = AssetDatabaseLoad<GameObject>("Assets/_Project/Art/Environment/Nature/Rock_1.fbx");

        if (floorMaterial == null)
            floorMaterial = AssetDatabaseLoad<Material>("Assets/_Project/Materials/Mat_Island.mat");

        if (wallMaterial == null)
        {
            Shader standardShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            wallMaterial = new Material(standardShader) { color = new Color(0.28f, 0.28f, 0.32f) };
        }

        if (doorLockedMaterial == null)
        {
            Shader standardShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            doorLockedMaterial = new Material(standardShader) { color = new Color(0.85f, 0.15f, 0.15f) };
        }

        if (doorOpenMaterial == null)
        {
            Shader standardShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            doorOpenMaterial = new Material(standardShader) { color = new Color(0.2f, 0.8f, 0.3f) };
        }

        if (portalMaterial == null)
        {
            Shader standardShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            portalMaterial = new Material(standardShader) { color = new Color(0.1f, 0.85f, 0.95f) };
        }
    }

    private static T AssetDatabaseLoad<T>(string path) where T : UnityEngine.Object
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
#else
        return null;
#endif
    }

    private void GenerateDungeon()
    {
        // 1. Generate room coordinates on a grid (Random Walk with branching)
        generatedCoords.Clear();
        rooms.Clear();

        Vector2Int startCoord = Vector2Int.zero;
        generatedCoords.Add(startCoord);

        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        System.Random random = new System.Random();

        while (generatedCoords.Count < targetRoomCount)
        {
            List<Vector2Int> currentList = new List<Vector2Int>(generatedCoords);
            Vector2Int branchFrom = currentList[random.Next(currentList.Count)];
            Vector2Int chosenDir = directions[random.Next(directions.Length)];
            Vector2Int candidate = branchFrom + chosenDir;

            generatedCoords.Add(candidate);
        }

        // 2. Identify the Final Room (furthest from start)
        int maxDist = -1;
        foreach (Vector2Int coord in generatedCoords)
        {
            int dist = Mathf.Abs(coord.x) + Mathf.Abs(coord.y);
            if (dist > maxDist)
            {
                maxDist = dist;
                finalRoomCoord = coord;
            }
        }

        // 3. Instantiate physical rooms and corridors
        foreach (Vector2Int coord in generatedCoords)
        {
            RoomType type = (coord == startCoord) ? RoomType.Start :
                            (coord == finalRoomCoord) ? RoomType.Final : RoomType.Combat;

            int enemies = (type == RoomType.Final) ? enemiesInFinalRoom : enemiesPerCombatRoom;
            BuildRoom(coord, type, enemies);
        }

        // 4. Connect adjacent rooms with doorways
        foreach (KeyValuePair<Vector2Int, DungeonRoom> pair in rooms)
        {
            Vector2Int coord = pair.Key;
            DungeonRoom room = pair.Value;

            BuildDoorwayIfNeighbor(room, coord, Vector2Int.up, "Door_North");
            BuildDoorwayIfNeighbor(room, coord, Vector2Int.down, "Door_South");
            BuildDoorwayIfNeighbor(room, coord, Vector2Int.right, "Door_East");
            BuildDoorwayIfNeighbor(room, coord, Vector2Int.left, "Door_West");
        }

        // Reveal adjacent rooms to Start on minimap
        if (rooms.TryGetValue(startCoord, out DungeonRoom startRoom))
        {
            startRoom.MarkDiscovered();
            activeRoom = startRoom;
            RevealNeighbors(startCoord);
        }
    }

    private void BuildRoom(Vector2Int coord, RoomType type, int enemies)
    {
        Vector3 worldPos = new Vector3(coord.x * spacingX, 0f, coord.y * spacingZ);
        GameObject roomObj = new GameObject($"Room_{coord.x}_{coord.y}_{type}");
        roomObj.transform.SetParent(transform, false);
        roomObj.transform.position = worldPos;

        DungeonRoom roomComponent = roomObj.AddComponent<DungeonRoom>();
        roomComponent.Setup(coord, type, roomDimensions, enemyPrefab, enemies);

        // Room trigger for player detection
        BoxCollider roomTrigger = roomObj.AddComponent<BoxCollider>();
        roomTrigger.isTrigger = true;
        roomTrigger.size = new Vector3(roomDimensions.x - 1f, 5f, roomDimensions.y - 1f);
        roomTrigger.center = new Vector3(0f, 2.5f, 0f);

        // Floor
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
        floor.name = "Floor";
        floor.layer = LayerMask.NameToLayer("Ground");
        if (floor.layer < 0) floor.layer = 9;
        floor.transform.SetParent(roomObj.transform, false);
        floor.transform.localPosition = Vector3.zero;
        floor.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        floor.transform.localScale = new Vector3(roomDimensions.x, roomDimensions.y, 1f);

        Renderer floorRenderer = floor.GetComponent<Renderer>();
        if (floorRenderer != null && floorMaterial != null)
            floorRenderer.sharedMaterial = floorMaterial;

        // BoxCollider on floor
        Destroy(floor.GetComponent<MeshCollider>());
        BoxCollider floorCol = floor.AddComponent<BoxCollider>();
        floorCol.size = new Vector3(1f, 1f, 0.2f);
        floorCol.center = new Vector3(0f, 0f, 0.1f);

        // Surrounding walls
        BuildRoomWalls(roomObj, coord);

        // Room dressing (decorations)
        DressRoom(roomObj, coord, type);

        // Visual Presentation Banners
        if (type == RoomType.Start)
        {
            CreateRoomBanner(roomObj, "NIVEL 1: ENTRADA", "Explora las salas y encuentra la salida\nWASD: Moverse  |  Click Izq: Disparar");
        }
        else if (type == RoomType.Final)
        {
            CreateRoomBanner(roomObj, "★ SALA FINAL ★", "Elimina a los guardianes para activar el portal");
            GameObject portal = CreateExitPortal(roomObj);
            roomComponent.SetPortalObject(portal);
        }

        rooms.Add(coord, roomComponent);
    }

    private void CreateRoomBanner(GameObject parent, string title, string subtitle)
    {
        GameObject bannerObj = new GameObject("RoomBanner");
        bannerObj.transform.SetParent(parent.transform, false);
        bannerObj.transform.localPosition = new Vector3(0f, 3.8f, roomDimensions.y * 0.4f);
        bannerObj.transform.localRotation = Quaternion.Euler(30f, 45f, 0f);

        TextMeshPro tmp = bannerObj.AddComponent<TextMeshPro>();
        tmp.text = $"<b>{title}</b>\n<size=65%><color=#AADDFF>{subtitle}</color></size>";
        tmp.fontSize = 6.5f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.rectTransform.sizeDelta = new Vector2(16f, 4.5f);
    }

    private void BuildRoomWalls(GameObject roomObj, Vector2Int coord)
    {
        float halfW = roomDimensions.x * 0.5f;
        float halfD = roomDimensions.y * 0.5f;
        float halfDoor = corridorWidth * 0.5f;

        // North Wall (+Z)
        if (generatedCoords.Contains(coord + Vector2Int.up))
        {
            CreateWallSegment(roomObj, new Vector3(-(halfW + halfDoor) * 0.5f, wallHeight * 0.5f, halfD),
                new Vector3(halfW - halfDoor, wallHeight, wallThickness));
            CreateWallSegment(roomObj, new Vector3((halfW + halfDoor) * 0.5f, wallHeight * 0.5f, halfD),
                new Vector3(halfW - halfDoor, wallHeight, wallThickness));
        }
        else
        {
            CreateWallSegment(roomObj, new Vector3(0f, wallHeight * 0.5f, halfD),
                new Vector3(roomDimensions.x, wallHeight, wallThickness));
        }

        // South Wall (-Z)
        if (generatedCoords.Contains(coord + Vector2Int.down))
        {
            CreateWallSegment(roomObj, new Vector3(-(halfW + halfDoor) * 0.5f, wallHeight * 0.5f, -halfD),
                new Vector3(halfW - halfDoor, wallHeight, wallThickness));
            CreateWallSegment(roomObj, new Vector3((halfW + halfDoor) * 0.5f, wallHeight * 0.5f, -halfD),
                new Vector3(halfW - halfDoor, wallHeight, wallThickness));
        }
        else
        {
            CreateWallSegment(roomObj, new Vector3(0f, wallHeight * 0.5f, -halfD),
                new Vector3(roomDimensions.x, wallHeight, wallThickness));
        }

        // East Wall (+X)
        if (generatedCoords.Contains(coord + Vector2Int.right))
        {
            CreateWallSegment(roomObj, new Vector3(halfW, wallHeight * 0.5f, -(halfD + halfDoor) * 0.5f),
                new Vector3(wallThickness, wallHeight, halfD - halfDoor));
            CreateWallSegment(roomObj, new Vector3(halfW, wallHeight * 0.5f, (halfD + halfDoor) * 0.5f),
                new Vector3(wallThickness, wallHeight, halfD - halfDoor));
        }
        else
        {
            CreateWallSegment(roomObj, new Vector3(halfW, wallHeight * 0.5f, 0f),
                new Vector3(wallThickness, wallHeight, roomDimensions.y));
        }

        // West Wall (-X)
        if (generatedCoords.Contains(coord + Vector2Int.left))
        {
            CreateWallSegment(roomObj, new Vector3(-halfW, wallHeight * 0.5f, -(halfD + halfDoor) * 0.5f),
                new Vector3(wallThickness, wallHeight, halfD - halfDoor));
            CreateWallSegment(roomObj, new Vector3(-halfW, wallHeight * 0.5f, (halfD + halfDoor) * 0.5f),
                new Vector3(wallThickness, wallHeight, halfD - halfDoor));
        }
        else
        {
            CreateWallSegment(roomObj, new Vector3(-halfW, wallHeight * 0.5f, 0f),
                new Vector3(wallThickness, wallHeight, roomDimensions.y));
        }
    }

    private void CreateWallSegment(GameObject parent, Vector3 localPos, Vector3 size)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Wall";
        wall.transform.SetParent(parent.transform, false);
        wall.transform.localPosition = localPos;
        wall.transform.localScale = size;

        Renderer r = wall.GetComponent<Renderer>();
        if (r != null && wallMaterial != null) r.sharedMaterial = wallMaterial;
    }

    private void BuildDoorwayIfNeighbor(DungeonRoom room, Vector2Int coord, Vector2Int dir, string doorName)
    {
        Vector2Int neighborCoord = coord + dir;
        if (!generatedCoords.Contains(neighborCoord)) return;

        float halfW = roomDimensions.x * 0.5f;
        float halfD = roomDimensions.y * 0.5f;

        Vector3 doorLocalPos;
        Vector3 barrierSize;

        if (dir == Vector2Int.up)
        {
            doorLocalPos = new Vector3(0f, 1.5f, halfD);
            barrierSize = new Vector3(corridorWidth, 3f, 0.4f);
        }
        else if (dir == Vector2Int.down)
        {
            doorLocalPos = new Vector3(0f, 1.5f, -halfD);
            barrierSize = new Vector3(corridorWidth, 3f, 0.4f);
        }
        else if (dir == Vector2Int.right)
        {
            doorLocalPos = new Vector3(halfW, 1.5f, 0f);
            barrierSize = new Vector3(0.4f, 3f, corridorWidth);
        }
        else // left
        {
            doorLocalPos = new Vector3(-halfW, 1.5f, 0f);
            barrierSize = new Vector3(0.4f, 3f, corridorWidth);
        }

        GameObject doorObj = new GameObject(doorName);
        doorObj.transform.SetParent(room.transform, false);
        doorObj.transform.localPosition = doorLocalPos;

        // Visual barrier
        GameObject barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
        barrier.name = "Barrier";
        barrier.transform.SetParent(doorObj.transform, false);
        barrier.transform.localPosition = Vector3.zero;
        barrier.transform.localScale = barrierSize;

        BoxCollider col = barrier.GetComponent<BoxCollider>();
        Renderer ren = barrier.GetComponent<Renderer>();

        DungeonDoor door = doorObj.AddComponent<DungeonDoor>();
        door.Setup(barrier, col, ren, doorLockedMaterial, doorOpenMaterial);
        room.AddDoor(door);

        // Build connecting hallway floor between this room and the neighbor
        if (dir == Vector2Int.up || dir == Vector2Int.right)
        {
            BuildConnectingHallway(room.transform.position, dir);
        }
    }

    private void BuildConnectingHallway(Vector3 fromRoomPos, Vector2Int dir)
    {
        Vector3 hallwayCenter = fromRoomPos;
        Vector3 hallwayScale = Vector3.one;

        if (dir == Vector2Int.up)
        {
            float dist = spacingZ - roomDimensions.y;
            hallwayCenter += new Vector3(0f, 0f, roomDimensions.y * 0.5f + dist * 0.5f);
            hallwayScale = new Vector3(corridorWidth, dist, 1f);
        }
        else if (dir == Vector2Int.right)
        {
            float dist = spacingX - roomDimensions.x;
            hallwayCenter += new Vector3(roomDimensions.x * 0.5f + dist * 0.5f, 0f, 0f);
            hallwayScale = new Vector3(dist, corridorWidth, 1f);
        }

        GameObject hallwayFloor = GameObject.CreatePrimitive(PrimitiveType.Quad);
        hallwayFloor.name = "HallwayFloor";
        hallwayFloor.layer = LayerMask.NameToLayer("Ground");
        if (hallwayFloor.layer < 0) hallwayFloor.layer = 9;
        hallwayFloor.transform.SetParent(transform, true);
        hallwayFloor.transform.position = hallwayCenter;
        hallwayFloor.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        hallwayFloor.transform.localScale = hallwayScale;

        Renderer ren = hallwayFloor.GetComponent<Renderer>();
        if (ren != null && floorMaterial != null) ren.sharedMaterial = floorMaterial;

        Destroy(hallwayFloor.GetComponent<MeshCollider>());
        BoxCollider bc = hallwayFloor.AddComponent<BoxCollider>();
        bc.size = new Vector3(1f, 1f, 0.2f);
        bc.center = new Vector3(0f, 0f, 0.1f);
    }

    private void DressRoom(GameObject roomObj, Vector2Int coord, RoomType type)
    {
        float halfW = roomDimensions.x * 0.5f - 2f;
        float halfD = roomDimensions.y * 0.5f - 2f;

        Vector3[] corners = {
            new Vector3(-halfW, 0f, halfD),
            new Vector3(halfW, 0f, halfD),
            new Vector3(-halfW, 0f, -halfD),
            new Vector3(halfW, 0f, -halfD)
        };

        for (int i = 0; i < corners.Length; i++)
        {
            GameObject prefabToUse = (i % 2 == 0) ? rockPrefab : bushPrefab;
            if (prefabToUse != null)
            {
                GameObject prop = Instantiate(prefabToUse, roomObj.transform);
                prop.transform.localPosition = corners[i];
                prop.transform.localRotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            }
        }

        if (type == RoomType.Combat && rockPrefab != null)
        {
            Vector3 rockPos1 = new Vector3(-halfW * 0.45f, 0f, 0f);
            Vector3 rockPos2 = new Vector3(halfW * 0.45f, 0f, 0f);

            GameObject r1 = Instantiate(rockPrefab, roomObj.transform);
            r1.transform.localPosition = rockPos1;
            GameObject r2 = Instantiate(rockPrefab, roomObj.transform);
            r2.transform.localPosition = rockPos2;
        }
    }

    private GameObject CreateExitPortal(GameObject parent)
    {
        GameObject portalObj = new GameObject("ExitPortal");
        portalObj.transform.SetParent(parent.transform, false);
        portalObj.transform.localPosition = new Vector3(0f, 0f, 0f);

        // Pedestal base
        GameObject baseCylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseCylinder.name = "PortalPedestal";
        baseCylinder.transform.SetParent(portalObj.transform, false);
        baseCylinder.transform.localPosition = new Vector3(0f, 0.15f, 0f);
        baseCylinder.transform.localScale = new Vector3(3f, 0.15f, 3f);

        Renderer baseRen = baseCylinder.GetComponent<Renderer>();
        if (baseRen != null && portalMaterial != null) baseRen.sharedMaterial = portalMaterial;

        // Rotating Magic Ring / Diamond
        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ring.name = "RotatingPortalCore";
        ring.transform.SetParent(portalObj.transform, false);
        ring.transform.localPosition = new Vector3(0f, 1.4f, 0f);
        ring.transform.localRotation = Quaternion.Euler(45f, 45f, 45f);
        ring.transform.localScale = new Vector3(1f, 1f, 1f);

        Renderer ringRen = ring.GetComponent<Renderer>();
        if (ringRen != null && portalMaterial != null) ringRen.sharedMaterial = portalMaterial;
        Destroy(ring.GetComponent<Collider>());

        // Trigger Collider
        BoxCollider trigger = portalObj.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(2.5f, 3f, 2.5f);
        trigger.center = new Vector3(0f, 1.5f, 0f);

        ExitPortal exitPortal = portalObj.AddComponent<ExitPortal>();
        exitPortal.Setup(ring.transform);

        return portalObj;
    }

    private void SetupPlayerPosition()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerObj.transform.position = new Vector3(0f, 1.5f, 0f);
            PlayerController pc = playerObj.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.SetWorldLimits(new Vector2(1000f, 1000f));
            }
        }
    }

    public void NotifyRoomEntered(DungeonRoom room)
    {
        activeRoom = room;
        room.MarkDiscovered();
        RevealNeighbors(room.GridPos);
        RoomEntered?.Invoke(room);
    }

    public void NotifyRoomCleared(DungeonRoom room)
    {
        clearedRoomsCount++;
        totalKills += room.AliveEnemyCount;
        RoomCleared?.Invoke(room);
    }

    private void RevealNeighbors(Vector2Int coord)
    {
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        for (int i = 0; i < dirs.Length; i++)
        {
            if (rooms.TryGetValue(coord + dirs[i], out DungeonRoom neighbor))
            {
                neighbor.MarkDiscovered();
            }
        }
    }

    public void CompleteLevel()
    {
        if (levelCompleted) return;
        levelCompleted = true;

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
        }

        Time.timeScale = 0f;
        LevelFinished?.Invoke();
    }
}
