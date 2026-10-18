using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Streams deterministic terrain chunks from the camera frustum while preserving a safe gameplay neighborhood.
/// The finite playable area has continuous collision boundaries and tree visuals at its edge.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldChunkManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private BoxCollider legacyIslandCollider;
    [SerializeField] private Material terrainMaterial;
    [SerializeField] private GameObject treePrefab;
    [SerializeField] private GameObject bushPrefab;
    [SerializeField] private GameObject rockPrefab;

    [Header("Chunk layout")]
    [SerializeField, Min(8f)] private float chunkSize = 20f;
    [SerializeField, Min(2)] private int worldRadiusChunks = 4;
    [SerializeField] private int seed = 21761;
    [SerializeField, Min(0.05f)] private float chunkCheckInterval = 0.15f;
    [SerializeField] private float groundY;

    [Header("Camera visibility streaming")]
    [SerializeField] private Camera worldCamera;
    [SerializeField, Range(0, 4)] private int safetyMarginChunks = 1;
    [SerializeField, Min(1)] private int activeChunkLimit = 36;
    [SerializeField, Min(0f)] private float unloadGraceSeconds = 1.25f;
    [SerializeField, Min(0f)] private float cameraMoveThreshold = 0.5f;
    [SerializeField, Min(0f)] private float cameraRotationThreshold = 2f;
    [SerializeField] private Transform[] persistentObjects;
    [SerializeField, Range(0, 2)] private int playerSafetyRadiusChunks = 1;
    [SerializeField] private bool logChunkStreamingStats;

    [Header("Streaming diagnostics (read only)")]
    [SerializeField] private int visibleChunksInFrustum;
    [SerializeField] private int loadedChunks;
    [SerializeField] private int pendingUnloadChunks;

    [Header("Deterministic vegetation")]
    [SerializeField, Min(0)] private int treesPerChunk = 2;
    [SerializeField, Min(0)] private int bushesPerChunk = 4;
    [SerializeField, Min(0)] private int rocksPerChunk = 2;
    [SerializeField, Min(0f)] private float clearRadiusAtStart = 18f;
    [SerializeField, Min(0.1f)] private float treeWallSpacing = 2.3f;
    [SerializeField, Min(0f)] private int pooledChunkLimit = 8;

    [Header("World boundary")]
    [SerializeField, Min(0.5f)] private float boundaryWallThickness = 2f;
    [SerializeField, Min(1f)] private float boundaryWallHeight = 7f;
    [SerializeField, Min(0.1f)] private float treeScale = 0.82f;
    [SerializeField, Min(0.1f)] private float bushScale = 0.62f;
    [SerializeField, Min(0.1f)] private float boundaryTreeScale = 0.82f;
    [SerializeField, Min(0f)] private float minimumDecorationSpacing = 2.1f;
    [SerializeField] private float treeGroundOffset = 0f;
    [SerializeField] private float bushGroundOffset = 0f;
    [SerializeField] private float rockGroundOffset = 0.19f;

    private readonly Dictionary<Vector2Int, Chunk> activeChunks = new Dictionary<Vector2Int, Chunk>();
    private readonly Dictionary<Vector2Int, float> pendingUnloads = new Dictionary<Vector2Int, float>();
    private readonly HashSet<Vector2Int> gameplayRequiredChunks = new HashSet<Vector2Int>();
    private readonly Queue<Chunk> chunkPool = new Queue<Chunk>();
    private readonly List<GameObject> wallTrees = new List<GameObject>();
    private readonly List<GameObject> boundaryObjects = new List<GameObject>();
    private Vector2Int lastPlayerChunk = new Vector2Int(int.MinValue, int.MinValue);
    private Vector3 lastCameraPosition;
    private Quaternion lastCameraRotation;
    private float lastCameraFieldOfView;
    private float lastCameraOrthographicSize;
    private float lastCameraAspect;
    private float checkTimer;
    private bool initialized;

    public int ActiveChunkCount => activeChunks.Count;
    public int PooledChunkCount => chunkPool.Count;
    public int VisibleChunkCount => visibleChunksInFrustum;
    public int PendingUnloadCount => pendingUnloadChunks;

    private sealed class Chunk
    {
        public Vector2Int Coordinate;
        public GameObject Root;
        public GameObject[] Trees;
        public GameObject[] Bushes;
        public GameObject[] Rocks;
        public Mesh SurfaceMesh;
    }

    private void Awake()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
        }

        if (legacyIslandCollider == null)
        {
            GameObject island = GameObject.Find("Island");
            if (island != null) legacyIslandCollider = island.GetComponent<BoxCollider>();
        }
    }

    private void Start()
    {
        if (worldCamera == null) worldCamera = Camera.main;
        if (player == null)
        {
            Debug.LogError("WorldChunkManager: no se encontró el jugador con tag Player.", this);
            enabled = false;
            return;
        }

        // Keep a continuous fallback floor beneath the streamed chunks. It prevents
        // a missing or late chunk from opening a hole in the playable area.
        ConfigureFallbackGround();
        DisableCloudDecorations();
        HideLegacyIslandVisual();
        CreateBoundaryWalls();
        CreateBoundaryTreeWall();
        ApplyWorldLimits();
        initialized = true;
        RefreshChunks(GetPlayerChunk());
        Physics.SyncTransforms();
    }

    private void DisableCloudDecorations()
    {
        Transform[] transforms = FindObjectsOfType<Transform>(true);
        int disabledCount = 0;
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || !candidate.name.StartsWith("Cloud_", StringComparison.Ordinal)) continue;
            candidate.gameObject.SetActive(false);
            disabledCount++;
        }

        if (disabledCount > 0)
            Debug.Log($"WorldChunkManager: se desactivaron {disabledCount} nubes decorativas de la escena.", this);
    }

    private void HideLegacyIslandVisual()
    {
        GameObject islandBase = GameObject.Find("IslandBase");
        if (islandBase == null) return;

        Renderer[] renderers = islandBase.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].enabled = false;
    }

    private void ConfigureFallbackGround()
    {
        if (legacyIslandCollider == null) return;

        Transform islandTransform = legacyIslandCollider.transform;
        Vector3 lossyScale = islandTransform.lossyScale;
        Vector3 safeScale = new Vector3(
            Mathf.Max(0.0001f, Mathf.Abs(lossyScale.x)),
            Mathf.Max(0.0001f, Mathf.Abs(lossyScale.y)),
            Mathf.Max(0.0001f, Mathf.Abs(lossyScale.z)));
        float extent = worldRadiusChunks * chunkSize;
        Vector3 floorCenter = islandTransform.InverseTransformPoint(new Vector3(0f, groundY - 0.1f, 0f));

        legacyIslandCollider.center = floorCenter;
        legacyIslandCollider.size = new Vector3(
            extent * 2f / safeScale.x,
            0.2f / safeScale.y,
            extent * 2f / safeScale.z);
        legacyIslandCollider.enabled = true;
    }
    private void Update()
    {
        if (!initialized || player == null) return;

        checkTimer += Time.unscaledDeltaTime;
        if (checkTimer < chunkCheckInterval) return;
        checkTimer = 0f;

        Vector2Int playerChunk = GetPlayerChunk();
        bool gameplayDependenciesChanged = !GetGameplayRequiredChunks(playerChunk).SetEquals(gameplayRequiredChunks);
        if (playerChunk != lastPlayerChunk || CameraMovedEnough() || gameplayDependenciesChanged)
            RefreshChunks(playerChunk);

        ProcessPendingUnloads();
        UpdateStreamingDiagnostics();
    }

    private bool CameraMovedEnough()
    {
        if (worldCamera == null) return false;
        Transform cameraTransform = worldCamera.transform;
        return Vector3.Distance(cameraTransform.position, lastCameraPosition) >= cameraMoveThreshold
            || Quaternion.Angle(cameraTransform.rotation, lastCameraRotation) >= cameraRotationThreshold
            || !Mathf.Approximately(worldCamera.fieldOfView, lastCameraFieldOfView)
            || !Mathf.Approximately(worldCamera.orthographicSize, lastCameraOrthographicSize)
            || !Mathf.Approximately(worldCamera.aspect, lastCameraAspect);
    }

    private void RememberCameraState()
    {
        if (worldCamera == null) return;
        Transform cameraTransform = worldCamera.transform;
        lastCameraPosition = cameraTransform.position;
        lastCameraRotation = cameraTransform.rotation;
        lastCameraFieldOfView = worldCamera.fieldOfView;
        lastCameraOrthographicSize = worldCamera.orthographicSize;
        lastCameraAspect = worldCamera.aspect;
    }
    private Vector2Int GetPlayerChunk()
    {
        return new Vector2Int(
            Mathf.FloorToInt(player.position.x / chunkSize),
            Mathf.FloorToInt(player.position.z / chunkSize));
    }

    private void RefreshChunks(Vector2Int center)
    {
        lastPlayerChunk = center;
        RememberCameraState();

        Plane[] frustumPlanes = worldCamera != null ? GeometryUtility.CalculateFrustumPlanes(worldCamera) : null;
        HashSet<Vector2Int> visible = new HashSet<Vector2Int>();
        HashSet<Vector2Int> expanded = new HashSet<Vector2Int>();
        float safetyMargin = safetyMarginChunks * chunkSize;

        for (int z = -worldRadiusChunks; z < worldRadiusChunks; z++)
        {
            for (int x = -worldRadiusChunks; x < worldRadiusChunks; x++)
            {
                Vector2Int coordinate = new Vector2Int(x, z);
                if (frustumPlanes == null || GeometryUtility.TestPlanesAABB(frustumPlanes, GetChunkBounds(coordinate, 0f)))
                    visible.Add(coordinate);
                if (frustumPlanes == null || GeometryUtility.TestPlanesAABB(frustumPlanes, GetChunkBounds(coordinate, safetyMargin)))
                    expanded.Add(coordinate);
            }
        }

        visibleChunksInFrustum = visible.Count;
        gameplayRequiredChunks.Clear();
        gameplayRequiredChunks.UnionWith(GetGameplayRequiredChunks(center));
        HashSet<Vector2Int> desired = new HashSet<Vector2Int>(visible);
        desired.UnionWith(gameplayRequiredChunks);

        List<Vector2Int> marginCandidates = new List<Vector2Int>(expanded);
        marginCandidates.RemoveAll(coordinate => desired.Contains(coordinate));
        marginCandidates.Sort((a, b) => ChunkPriority(a, center).CompareTo(ChunkPriority(b, center)));
        int targetCapacity = Mathf.Max(activeChunkLimit, desired.Count);
        for (int i = 0; i < marginCandidates.Count && desired.Count < targetCapacity; i++)
            desired.Add(marginCandidates[i]);

        foreach (Vector2Int coordinate in desired)
        {
            pendingUnloads.Remove(coordinate);
            if (activeChunks.ContainsKey(coordinate)) continue;
            Chunk chunk = chunkPool.Count > 0 ? chunkPool.Dequeue() : CreateChunk();
            ConfigureChunk(chunk, coordinate);
            chunk.Root.SetActive(true);
            activeChunks.Add(coordinate, chunk);
        }

        List<Vector2Int> activeCoordinates = new List<Vector2Int>(activeChunks.Keys);
        for (int i = 0; i < activeCoordinates.Count; i++)
        {
            Vector2Int coordinate = activeCoordinates[i];
            if (desired.Contains(coordinate) || IsGameplayChunkRequired(coordinate))
            {
                pendingUnloads.Remove(coordinate);
                continue;
            }
            if (!pendingUnloads.ContainsKey(coordinate))
                pendingUnloads.Add(coordinate, Time.unscaledTime + unloadGraceSeconds);
        }
        Physics.SyncTransforms();
        UpdateStreamingDiagnostics();
        if (logChunkStreamingStats && activeChunks.Count > activeChunkLimit)
            Debug.LogWarning($"WorldChunkManager: {visibleChunksInFrustum} visibles, {activeChunks.Count} cargados; se supera el límite de {activeChunkLimit} para conservar chunks visibles y de jugabilidad.", this);
    }

    private Bounds GetChunkBounds(Vector2Int coordinate, float horizontalExpansion)
    {
        float size = chunkSize + horizontalExpansion * 2f;
        return new Bounds(new Vector3(coordinate.x * chunkSize + chunkSize * 0.5f, groundY,
            coordinate.y * chunkSize + chunkSize * 0.5f), new Vector3(size, 0.4f, size));
    }

    private HashSet<Vector2Int> GetGameplayRequiredChunks(Vector2Int playerChunk)
    {
        HashSet<Vector2Int> required = new HashSet<Vector2Int>();
        AddRequiredNeighborhood(required, playerChunk, playerSafetyRadiusChunks);
        if (persistentObjects != null)
            for (int i = 0; i < persistentObjects.Length; i++)
                if (persistentObjects[i] != null) required.Add(GetChunkCoordinate(persistentObjects[i].position));

        EnemyChaser[] enemies = FindObjectsOfType<EnemyChaser>();
        for (int i = 0; i < enemies.Length; i++)
            if (enemies[i] != null && enemies[i].gameObject.activeInHierarchy)
                required.Add(GetChunkCoordinate(enemies[i].transform.position));
        required.RemoveWhere(coordinate => !IsInsideWorld(coordinate));
        return required;
    }

    private bool IsGameplayChunkRequired(Vector2Int coordinate)
    {
        if (player != null && GetChunkCoordinate(player.position) == coordinate) return true;
        if (persistentObjects != null)
            for (int i = 0; i < persistentObjects.Length; i++)
                if (persistentObjects[i] != null && GetChunkCoordinate(persistentObjects[i].position) == coordinate) return true;
        return gameplayRequiredChunks.Contains(coordinate);
    }

    private void AddRequiredNeighborhood(HashSet<Vector2Int> required, Vector2Int center, int radius)
    {
        for (int z = -radius; z <= radius; z++)
            for (int x = -radius; x <= radius; x++)
            {
                Vector2Int coordinate = new Vector2Int(center.x + x, center.y + z);
                if (IsInsideWorld(coordinate)) required.Add(coordinate);
            }
    }

    private Vector2Int GetChunkCoordinate(Vector3 position)
    {
        return new Vector2Int(Mathf.FloorToInt(position.x / chunkSize), Mathf.FloorToInt(position.z / chunkSize));
    }

    private float ChunkPriority(Vector2Int coordinate, Vector2Int playerChunk)
    {
        return (coordinate - playerChunk).sqrMagnitude;
    }

    private void ProcessPendingUnloads()
    {
        if (pendingUnloads.Count == 0) return;
        List<Vector2Int> expired = new List<Vector2Int>();
        foreach (KeyValuePair<Vector2Int, float> pair in pendingUnloads)
            if (Time.unscaledTime >= pair.Value && !IsGameplayChunkRequired(pair.Key)) expired.Add(pair.Key);

        for (int i = 0; i < expired.Count; i++)
        {
            Vector2Int coordinate = expired[i];
            pendingUnloads.Remove(coordinate);
            if (!activeChunks.TryGetValue(coordinate, out Chunk chunk)) continue;
            activeChunks.Remove(coordinate);
            chunk.Root.SetActive(false);
            if (chunkPool.Count < pooledChunkLimit) chunkPool.Enqueue(chunk);
            else DestroyChunk(chunk);
        }
        if (expired.Count > 0)
        {
            Physics.SyncTransforms();
        }
    }

    private void UpdateStreamingDiagnostics()
    {
        loadedChunks = activeChunks.Count;
        pendingUnloadChunks = pendingUnloads.Count;
    }

    [ContextMenu("Log chunk streaming stats")]
    private void LogChunkStreamingStats()
    {
        Debug.Log($"WorldChunkManager: {visibleChunksInFrustum} visibles, {loadedChunks} cargados, {pendingUnloadChunks} pendientes de descarga, {chunkPool.Count} en pool.", this);
    }
    private bool IsInsideWorld(Vector2Int coordinate)
    {
        return coordinate.x >= -worldRadiusChunks && coordinate.x < worldRadiusChunks
            && coordinate.y >= -worldRadiusChunks && coordinate.y < worldRadiusChunks;
    }

    private Chunk CreateChunk()
    {
        Chunk chunk = new Chunk();
        chunk.Root = new GameObject("WorldChunk");
        chunk.Root.transform.SetParent(transform, false);
        chunk.Root.layer = gameObject.layer;

        GameObject ground = new GameObject("Ground");
        ground.transform.SetParent(chunk.Root.transform, false);
        ground.layer = gameObject.layer;

        MeshFilter filter = ground.AddComponent<MeshFilter>();
        MeshRenderer renderer = ground.AddComponent<MeshRenderer>();
        BoxCollider collider = ground.AddComponent<BoxCollider>();
        collider.size = new Vector3(chunkSize, 0.2f, chunkSize);
        collider.center = new Vector3(chunkSize * 0.5f, -0.1f, chunkSize * 0.5f);
        chunk.SurfaceMesh = new Mesh { name = "WorldChunkSurface" };
        chunk.SurfaceMesh.vertices = new[]
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(0f, 0f, chunkSize),
            new Vector3(chunkSize, 0f, chunkSize),
            new Vector3(chunkSize, 0f, 0f)
        };
        chunk.SurfaceMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        chunk.SurfaceMesh.uv = new[]
        {
            Vector2.zero, new Vector2(0f, 1f), Vector2.one, new Vector2(1f, 0f)
        };
        chunk.SurfaceMesh.RecalculateNormals();
        chunk.SurfaceMesh.RecalculateBounds();
        filter.sharedMesh = chunk.SurfaceMesh;
        renderer.sharedMaterial = terrainMaterial != null ? terrainMaterial : CreateFallbackTerrainMaterial();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = true;

        chunk.Trees = CreatePropPool(treePrefab, treesPerChunk, chunk.Root.transform);
        chunk.Bushes = CreatePropPool(bushPrefab, bushesPerChunk, chunk.Root.transform);
        chunk.Rocks = CreatePropPool(rockPrefab, rocksPerChunk, chunk.Root.transform);
        chunk.Root.SetActive(false);
        return chunk;
    }

    private static Material fallbackTerrainMaterial;

    private static Material CreateFallbackTerrainMaterial()
    {
        if (fallbackTerrainMaterial != null) return fallbackTerrainMaterial;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        fallbackTerrainMaterial = new Material(shader) { color = new Color(0.2f, 0.39f, 0.12f) };
        return fallbackTerrainMaterial;
    }

    private static GameObject[] CreatePropPool(GameObject prefab, int count, Transform parent)
    {
        GameObject[] props = new GameObject[Mathf.Max(0, count)];
        if (prefab == null) return props;

        for (int i = 0; i < props.Length; i++)
        {
            props[i] = Instantiate(prefab, parent);
            props[i].name = prefab.name;
            props[i].transform.localPosition = Vector3.zero;
            props[i].transform.localRotation = Quaternion.identity;
            props[i].SetActive(false);
        }
        return props;
    }

    private void ConfigureChunk(Chunk chunk, Vector2Int coordinate)
    {
        chunk.Coordinate = coordinate;
        chunk.Root.name = "Chunk_" + coordinate.x + "_" + coordinate.y;
        chunk.Root.transform.position = new Vector3(coordinate.x * chunkSize, groundY, coordinate.y * chunkSize);

        System.Random random = new System.Random(MakeSeed(coordinate));
        List<Vector2> occupiedPositions = new List<Vector2>();
        PlaceProps(chunk.Trees, random, coordinate, treeScale, true, false, treeGroundOffset, occupiedPositions);
        PlaceProps(chunk.Bushes, random, coordinate, bushScale, false, false, bushGroundOffset, occupiedPositions);
        PlaceProps(chunk.Rocks, random, coordinate, 0.65f, false, true, rockGroundOffset, occupiedPositions);
    }

    private void PlaceProps(GameObject[] props, System.Random random, Vector2Int coordinate,
        float baseScale, bool isTree, bool isRock, float groundOffset, List<Vector2> occupiedPositions)
    {
        for (int i = 0; i < props.Length; i++)
        {
            GameObject prop = props[i];
            if (prop == null) continue;
            bool placed = false;
            Vector3 localPosition = Vector3.zero;

            for (int attempt = 0; attempt < 32; attempt++)
            {
                float margin = Mathf.Min(chunkSize * 0.4f, Mathf.Max(2f, baseScale * 2.8f));
                float x = margin + (float)random.NextDouble() * (chunkSize - 2f * margin);
                float z = margin + (float)random.NextDouble() * (chunkSize - 2f * margin);
                Vector3 worldPosition = new Vector3(coordinate.x * chunkSize + x, groundY + groundOffset,
                    coordinate.y * chunkSize + z);
                if (worldPosition.sqrMagnitude < clearRadiusAtStart * clearRadiusAtStart) continue;
                if (DistanceToWorldEdge(worldPosition) < treeWallSpacing * 2f) continue;
                if (IsTooCloseToExistingDecoration(x, z, occupiedPositions)) continue;

                localPosition = new Vector3(x, groundOffset, z);
                occupiedPositions.Add(new Vector2(x, z));
                placed = true;
                break;
            }

            if (!placed)
            {
                prop.SetActive(false);
                continue;
            }

            Transform propTransform = prop.transform;
            propTransform.localPosition = localPosition;
            propTransform.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            float scale = baseScale * Mathf.Lerp(0.88f, 1.12f, (float)random.NextDouble());
            if (!isTree && !isRock) scale *= Mathf.Lerp(0.9f, 1.08f, (float)random.NextDouble());
            propTransform.localScale = Vector3.one * scale;

            if (isTree) EnsureTreeCollider(propTransform, 8.6f, 0.72f);
            else if (isRock) EnsureRockCollider(propTransform, scale);
            prop.SetActive(true);
        }
    }

    private bool IsTooCloseToExistingDecoration(float x, float z, List<Vector2> occupiedPositions)
    {
        float minimumDistanceSquared = minimumDecorationSpacing * minimumDecorationSpacing;
        Vector2 candidate = new Vector2(x, z);
        for (int i = 0; i < occupiedPositions.Count; i++)
            if ((candidate - occupiedPositions[i]).sqrMagnitude < minimumDistanceSquared)
                return true;
        return false;
    }
    private float DistanceToWorldEdge(Vector3 position)
    {
        float extent = worldRadiusChunks * chunkSize;
        return Mathf.Min(extent - Mathf.Abs(position.x), extent - Mathf.Abs(position.z));
    }

    private int MakeSeed(Vector2Int coordinate)
    {
        unchecked
        {
            int value = seed ^ coordinate.x * 73856093 ^ coordinate.y * 19349663;
            return value ^ 0x5f3759df;
        }
    }

    private static void EnsureTreeCollider(Transform tree, float height, float radius)
    {
        if (tree.GetComponent<Collider>() != null) return;
        CapsuleCollider collider = tree.gameObject.AddComponent<CapsuleCollider>();
        collider.direction = 1;
        collider.height = Mathf.Max(height, radius * 2f);
        collider.radius = radius;
        collider.center = Vector3.up * (height * 0.5f);
    }

    private static void EnsureRockCollider(Transform rock, float scale)
    {
        if (rock.GetComponent<Collider>() != null) return;
        SphereCollider collider = rock.gameObject.AddComponent<SphereCollider>();
        collider.center = Vector3.up * 0.45f;
        collider.radius = 0.8f;
        collider.transform.localScale = Vector3.one * scale;
    }

    private void CreateBoundaryWalls()
    {
        float extent = worldRadiusChunks * chunkSize;
        float wallCenter = extent - boundaryWallThickness * 0.5f;
        float diameter = extent * 2f;
        AddBoundary("Wall_East", new Vector3(wallCenter, boundaryWallHeight * 0.5f, 0f),
            new Vector3(boundaryWallThickness, boundaryWallHeight, diameter));
        AddBoundary("Wall_West", new Vector3(-wallCenter, boundaryWallHeight * 0.5f, 0f),
            new Vector3(boundaryWallThickness, boundaryWallHeight, diameter));
        AddBoundary("Wall_North", new Vector3(0f, boundaryWallHeight * 0.5f, wallCenter),
            new Vector3(diameter, boundaryWallHeight, boundaryWallThickness));
        AddBoundary("Wall_South", new Vector3(0f, boundaryWallHeight * 0.5f, -wallCenter),
            new Vector3(diameter, boundaryWallHeight, boundaryWallThickness));
    }

    private void AddBoundary(string objectName, Vector3 position, Vector3 size)
    {
        GameObject wall = new GameObject(objectName);
        wall.layer = gameObject.layer;
        wall.transform.SetParent(transform, false);
        wall.transform.localPosition = position;
        BoxCollider collider = wall.AddComponent<BoxCollider>();
        collider.size = size;
        boundaryObjects.Add(wall);
    }

    private void ApplyWorldLimits()
    {
        float extent = worldRadiusChunks * chunkSize;
        float safeLimit = extent - boundaryWallThickness - 0.55f;

        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController != null) playerController.SetWorldLimits(new Vector2(safeLimit, safeLimit));

        EnemySpawner spawner = FindObjectOfType<EnemySpawner>();
        if (spawner != null) spawner.SetArenaHalfExtents(new Vector2(extent, extent));
    }

    private void CreateBoundaryTreeWall()
    {
        if (treePrefab == null) return;

        // This perimeter persists independently from streamed terrain chunks.
        float extent = worldRadiusChunks * chunkSize - boundaryWallThickness * 0.5f - 0.2f;
        float span = extent * 2f;
        int steps = Mathf.Max(1, Mathf.CeilToInt(span / treeWallSpacing));
        float spacing = span / steps;
        System.Random random = new System.Random(seed ^ 0x51ed270b);

        for (int i = 0; i <= steps; i++)
        {
            float along = -extent + spacing * i;
            CreateBoundaryTree(new Vector3(extent, groundY + treeGroundOffset, along), random);
            CreateBoundaryTree(new Vector3(-extent, groundY + treeGroundOffset, along), random);
            if (i > 0 && i < steps)
            {
                CreateBoundaryTree(new Vector3(along, groundY + treeGroundOffset, extent), random);
                CreateBoundaryTree(new Vector3(along, groundY + treeGroundOffset, -extent), random);
            }
        }
    }

    private void CreateBoundaryTree(Vector3 position, System.Random random)
    {
        GameObject tree = Instantiate(treePrefab, transform);
        tree.name = "BoundaryTree";
        tree.transform.SetPositionAndRotation(position,
            Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f));
        tree.transform.localScale = Vector3.one * boundaryTreeScale * Mathf.Lerp(0.92f, 1.1f, (float)random.NextDouble());
        wallTrees.Add(tree);
    }
    private void DestroyChunk(Chunk chunk)
    {
        if (chunk == null) return;
        if (chunk.Root != null) DestroyOwnedObject(chunk.Root);
        if (chunk.SurfaceMesh != null) DestroyOwnedObject(chunk.SurfaceMesh);
    }

    private static void DestroyOwnedObject(UnityEngine.Object target)
    {
        if (Application.isPlaying) Destroy(target);
        else DestroyImmediate(target);
    }

    private void OnDestroy()
    {
        foreach (Chunk chunk in activeChunks.Values) DestroyChunk(chunk);
        while (chunkPool.Count > 0) DestroyChunk(chunkPool.Dequeue());
        for (int i = 0; i < wallTrees.Count; i++)
            if (wallTrees[i] != null) DestroyOwnedObject(wallTrees[i]);
        for (int i = 0; i < boundaryObjects.Count; i++)
            if (boundaryObjects[i] != null) DestroyOwnedObject(boundaryObjects[i]);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.15f, 0.8f, 0.2f, 0.9f);
        float extent = worldRadiusChunks * chunkSize;
        Gizmos.DrawWireCube(new Vector3(0f, groundY, 0f), new Vector3(extent * 2f, 0.1f, extent * 2f));
    }
}
