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

    [Header("Colors")]
    [SerializeField] private Color currentRoomColor = new Color(0.15f, 0.85f, 1f, 1f);
    [SerializeField] private Color clearedRoomColor = new Color(0.35f, 0.75f, 0.4f, 1f);
    [SerializeField] private Color unclearedRoomColor = new Color(0.5f, 0.5f, 0.55f, 0.9f);
    [SerializeField] private Color undiscoveredColor = new Color(0.2f, 0.2f, 0.2f, 0.4f);
    [SerializeField] private Color finalRoomColor = new Color(1f, 0.35f, 0.2f, 1f);

    private readonly Dictionary<Vector2Int, Image> cellImages = new Dictionary<Vector2Int, Image>();
    private readonly Dictionary<Vector2Int, TMP_Text> cellLabels = new Dictionary<Vector2Int, TMP_Text>();
    private bool initialized;

    private void Start()
    {
        if (DungeonManager.Instance != null)
        {
            DungeonManager.Instance.RoomEntered += OnRoomEntered;
            DungeonManager.Instance.RoomCleared += OnRoomCleared;
        }

        CreateMinimapUI();
    }

    private void OnDestroy()
    {
        if (DungeonManager.Instance != null)
        {
            DungeonManager.Instance.RoomEntered -= OnRoomEntered;
            DungeonManager.Instance.RoomCleared -= OnRoomCleared;
        }
    }

    public void CreateMinimapUI()
    {
        if (initialized || DungeonManager.Instance == null) return;
        initialized = true;

        if (mapContainer == null)
        {
            mapContainer = GetComponent<RectTransform>();
        }

        IReadOnlyDictionary<Vector2Int, DungeonRoom> allRooms = DungeonManager.Instance.AllRooms;
        Vector2Int finalCoord = DungeonManager.Instance.FinalRoomCoord;

        // Clear previous children
        for (int i = mapContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = mapContainer.GetChild(i);
            if (child.name.StartsWith("RoomCell_"))
                Destroy(child.gameObject);
        }

        cellImages.Clear();
        cellLabels.Clear();

        float step = cellSize + cellSpacing;

        foreach (KeyValuePair<Vector2Int, DungeonRoom> pair in allRooms)
        {
            Vector2Int coord = pair.Key;
            DungeonRoom room = pair.Value;

            GameObject cellObj = new GameObject($"RoomCell_{coord.x}_{coord.y}");
            cellObj.transform.SetParent(mapContainer, false);

            RectTransform rt = cellObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(cellSize, cellSize);
            rt.anchoredPosition = new Vector2(coord.x * step, coord.y * step);

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
                label.text = "★";
            else
                label.text = "";

            cellLabels.Add(coord, label);
        }

        RefreshMinimap();
    }

    private void Update()
    {
        if (!initialized && DungeonManager.Instance != null && DungeonManager.Instance.AllRooms.Count > 0)
        {
            CreateMinimapUI();
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
