using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Authentic Binding of Isaac style procedural Minimap UI.
/// Renders discovered rooms on an interactive grid with clear status, current room indicator,
/// and special marker for the final room.
/// </summary>
public class DungeonMinimap : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform mapContainer;
    [SerializeField] private TMP_Text levelTitleText;
    [SerializeField] private TMP_Text roomProgressText;

    [Header("Cell Settings")]
    [SerializeField] private float cellSize = 22f;
    [SerializeField] private float cellSpacing = 8f;
    [SerializeField] private float mapPadding = 12f;
    [SerializeField] private float minimumCellSize = 8f;

    [Header("Colors")]
    [SerializeField] private Color currentRoomColor = new Color(0.15f, 0.85f, 1f, 1f);
    [SerializeField] private Color clearedRoomColor = new Color(0.35f, 0.75f, 0.4f, 1f);
    [SerializeField] private Color unclearedRoomColor = new Color(0.5f, 0.5f, 0.55f, 0.9f);
    [SerializeField] private Color undiscoveredColor = new Color(0.2f, 0.2f, 0.2f, 0.4f);
    [SerializeField] private Color finalRoomColor = new Color(1f, 0.35f, 0.2f, 1f);

    private readonly Dictionary<Vector2Int, Image> cellImages = new Dictionary<Vector2Int, Image>();
    private readonly Dictionary<Vector2Int, TMP_Text> cellLabels = new Dictionary<Vector2Int, TMP_Text>();
    private bool initialized;
    private DungeonManager subscribedManager;
    private float lastMapWidth;
    private float lastMapHeight;

    private void Start()
    {
        SubscribeToDungeonManager();
        CreateMinimapUI();
    }

    private void OnDestroy()
    {
        if (subscribedManager != null)
        {
            subscribedManager.RoomEntered -= OnRoomEntered;
            subscribedManager.RoomCleared -= OnRoomCleared;
            subscribedManager = null;
        }
    }

    private void SubscribeToDungeonManager()
    {
        DungeonManager manager = DungeonManager.Instance;
        if (manager == null || manager == subscribedManager) return;

        if (subscribedManager != null)
        {
            subscribedManager.RoomEntered -= OnRoomEntered;
            subscribedManager.RoomCleared -= OnRoomCleared;
        }

        subscribedManager = manager;
        subscribedManager.RoomEntered += OnRoomEntered;
        subscribedManager.RoomCleared += OnRoomCleared;
    }

    public void CreateMinimapUI()
    {
        if (initialized || DungeonManager.Instance == null) return;

        if (mapContainer == null)
        {
            mapContainer = GetComponent<RectTransform>();
        }

        IReadOnlyDictionary<Vector2Int, DungeonRoom> allRooms = DungeonManager.Instance.AllRooms;
        if (allRooms.Count == 0) return;

        initialized = true;
        Vector2Int finalCoord = DungeonManager.Instance.FinalRoomCoord;

        if (mapContainer.GetComponent<RectMask2D>() == null)
        {
            mapContainer.gameObject.AddComponent<RectMask2D>();
        }

        // Clear previous children
        for (int i = mapContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = mapContainer.GetChild(i);
            if (child.name.StartsWith("RoomCell_"))
                Destroy(child.gameObject);
        }

        cellImages.Clear();
        cellLabels.Clear();

        foreach (KeyValuePair<Vector2Int, DungeonRoom> pair in allRooms)
        {
            Vector2Int coord = pair.Key;
            DungeonRoom room = pair.Value;

            GameObject cellObj = new GameObject($"RoomCell_{coord.x}_{coord.y}");
            cellObj.transform.SetParent(mapContainer, false);

            RectTransform rt = cellObj.AddComponent<RectTransform>();
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(cellSize, cellSize);
            rt.anchoredPosition = Vector2.zero;

            Image img = cellObj.AddComponent<Image>();
            img.color = undiscoveredColor;
            cellImages.Add(coord, img);

            // Icon/label inside cell
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(cellObj.transform, false);
            RectTransform labelRt = labelObj.AddComponent<RectTransform>();
            labelRt.sizeDelta = new Vector2(cellSize, cellSize);
            labelRt.anchoredPosition = Vector2.zero;

            TMP_Text label = labelObj.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 11f;
            label.fontStyle = FontStyles.Bold;

            if (coord == Vector2Int.zero)
                label.text = "S";
            else if (coord == finalCoord)
                label.text = "F";
            else
                label.text = "";

            cellLabels.Add(coord, label);
        }

        LayoutMinimap(allRooms);
        RefreshMinimap();
    }

    private void LayoutMinimap(IReadOnlyDictionary<Vector2Int, DungeonRoom> allRooms)
    {
        if (allRooms.Count == 0 || mapContainer == null) return;

        int minX = int.MaxValue;
        int maxX = int.MinValue;
        int minY = int.MaxValue;
        int maxY = int.MinValue;

        foreach (KeyValuePair<Vector2Int, DungeonRoom> pair in allRooms)
        {
            Vector2Int coord = pair.Key;
            minX = Mathf.Min(minX, coord.x);
            maxX = Mathf.Max(maxX, coord.x);
            minY = Mathf.Min(minY, coord.y);
            maxY = Mathf.Max(maxY, coord.y);
        }

        Vector2 center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        float spanX = maxX - minX;
        float spanY = maxY - minY;

        Rect containerRect = mapContainer.rect;
        float availableWidth = Mathf.Max(20f, containerRect.width - mapPadding * 2f);
        float availableHeight = Mathf.Max(20f, containerRect.height - mapPadding * 2f);

        float nominalWidth = spanX * (cellSize + cellSpacing) + cellSize;
        float nominalHeight = spanY * (cellSize + cellSpacing) + cellSize;
        float scale = 1f;

        if (nominalWidth > availableWidth)
            scale = Mathf.Min(scale, availableWidth / nominalWidth);
        if (nominalHeight > availableHeight)
            scale = Mathf.Min(scale, availableHeight / nominalHeight);

        float scaledCellSize = Mathf.Clamp(cellSize * scale, minimumCellSize, cellSize);
        float scaledSpacing = Mathf.Clamp(cellSpacing * scale, 2f, cellSpacing);
        float step = scaledCellSize + scaledSpacing;

        foreach (KeyValuePair<Vector2Int, DungeonRoom> pair in allRooms)
        {
            Vector2Int coord = pair.Key;
            if (!cellImages.TryGetValue(coord, out Image image)) continue;

            RectTransform rect = image.rectTransform;
            rect.sizeDelta = new Vector2(scaledCellSize, scaledCellSize);
            rect.anchoredPosition = new Vector2((coord.x - center.x) * step, (coord.y - center.y) * step);

            if (cellLabels.TryGetValue(coord, out TMP_Text label))
            {
                RectTransform labelRect = label.rectTransform;
                labelRect.sizeDelta = new Vector2(scaledCellSize, scaledCellSize);
                label.fontSize = Mathf.Clamp(scaledCellSize * 0.5f, 7f, 11f);
            }
        }

        lastMapWidth = containerRect.width;
        lastMapHeight = containerRect.height;
    }

    private void Update()
    {
        SubscribeToDungeonManager();

        if (!initialized && DungeonManager.Instance != null && DungeonManager.Instance.AllRooms.Count > 0)
        {
            CreateMinimapUI();
        }

        if (initialized && DungeonManager.Instance != null)
        {
            Rect rect = mapContainer.rect;
            if (!Mathf.Approximately(rect.width, lastMapWidth) ||
                !Mathf.Approximately(rect.height, lastMapHeight))
            {
                LayoutMinimap(DungeonManager.Instance.AllRooms);
            }
        }

        // Pulse the current room icon
        if (DungeonManager.Instance != null && DungeonManager.Instance.ActiveRoom != null)
        {
            Vector2Int currentCoord = DungeonManager.Instance.ActiveRoom.GridPos;
            if (cellImages.TryGetValue(currentCoord, out Image activeImg))
            {
                float pulse = 0.85f + Mathf.PingPong(Time.unscaledTime * 2f, 0.3f);
                activeImg.transform.localScale = Vector3.one * pulse;
            }
        }
    }

    private void OnRoomEntered(DungeonRoom room)
    {
        RefreshMinimap();
    }

    private void OnRoomCleared(DungeonRoom room)
    {
        RefreshMinimap();
    }

    public void RefreshMinimap()
    {
        if (DungeonManager.Instance == null) return;

        IReadOnlyDictionary<Vector2Int, DungeonRoom> allRooms = DungeonManager.Instance.AllRooms;
        Vector2Int finalCoord = DungeonManager.Instance.FinalRoomCoord;
        DungeonRoom active = DungeonManager.Instance.ActiveRoom;

        foreach (KeyValuePair<Vector2Int, DungeonRoom> pair in allRooms)
        {
            Vector2Int coord = pair.Key;
            DungeonRoom room = pair.Value;

            if (!cellImages.TryGetValue(coord, out Image img)) continue;

            if (!room.IsDiscovered)
            {
                img.gameObject.SetActive(false);
                continue;
            }

            img.gameObject.SetActive(true);

            if (active != null && room.GridPos == active.GridPos)
            {
                img.color = currentRoomColor;
            }
            else if (room.IsCleared)
            {
                img.color = (coord == finalCoord) ? finalRoomColor : clearedRoomColor;
                img.transform.localScale = Vector3.one;
            }
            else
            {
                img.color = (coord == finalCoord) ? new Color(0.8f, 0.25f, 0.2f, 0.8f) : unclearedRoomColor;
                img.transform.localScale = Vector3.one;
            }
        }

        if (roomProgressText != null)
        {
            roomProgressText.text = $"Salas: {DungeonManager.Instance.ClearedRooms} / {DungeonManager.Instance.TotalRooms}";
        }

        if (levelTitleText != null)
        {
            levelTitleText.text = "Nivel 1: Mazmorra";
        }
    }
}
