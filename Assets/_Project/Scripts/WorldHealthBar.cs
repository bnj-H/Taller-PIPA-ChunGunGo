using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Health))]
public sealed class WorldHealthBar : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float width = 1.25f;
    [SerializeField, Min(0.01f)] private float height = 0.14f;
    [SerializeField, Min(0f)] private float offsetAboveCharacter = 0.24f;
    [SerializeField, Min(0.001f)] private float canvasScale = 0.01f;

    private Health health;
    private Camera targetCamera;
    private Canvas worldCanvas;
    private RectTransform canvasRect;
    private Image fillImage;
    private float verticalPosition;
    private bool isPlayer;

    private static readonly Color PlayerColor = new Color(0.18f, 0.88f, 0.73f, 1f);
    private static readonly Color LowHealthColor = new Color(0.96f, 0.20f, 0.16f, 1f);
    private static readonly Color FullHealthColor = new Color(0.35f, 0.88f, 0.32f, 1f);

    private void Awake()
    {
        health = GetComponent<Health>();
        targetCamera = Camera.main;
        isPlayer = CompareTag("Player");

        CreateBar();
        verticalPosition = FindCharacterTop() + offsetAboveCharacter;
        canvasRect.localPosition = new Vector3(0f, verticalPosition, 0f);
        UpdateFill();
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (worldCanvas != null)
                worldCanvas.worldCamera = targetCamera;
        }

        if (targetCamera != null && canvasRect != null)
        {
            Vector3 towardCamera = targetCamera.transform.position - canvasRect.position;
            if (towardCamera.sqrMagnitude > 0.001f)
                canvasRect.rotation = Quaternion.LookRotation(towardCamera, Vector3.up);
        }

        UpdateFill();
    }

    private void CreateBar()
    {
        GameObject bar = new GameObject("World Health Bar", typeof(RectTransform), typeof(Canvas));
        bar.layer = LayerMask.NameToLayer("UI");
        bar.transform.SetParent(transform, false);

        worldCanvas = bar.GetComponent<Canvas>();
        worldCanvas.renderMode = RenderMode.WorldSpace;
        worldCanvas.worldCamera = targetCamera;
        worldCanvas.overrideSorting = true;
        worldCanvas.sortingOrder = 20;

        canvasRect = bar.GetComponent<RectTransform>();
        float safeScale = Mathf.Max(0.001f, canvasScale);
        canvasRect.sizeDelta = new Vector2(width / safeScale, height / safeScale);
        canvasRect.localScale = Vector3.one * safeScale;

        Image background = CreateImage("Background", canvasRect);
        Stretch(background.rectTransform, 0f);
        background.color = new Color(0.035f, 0.075f, 0.075f, 0.92f);

        fillImage = CreateImage("Fill", canvasRect);
        Stretch(fillImage.rectTransform, canvasRect.sizeDelta.y * 0.14f);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
    }

    private static Image CreateImage(string objectName, Transform parent)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.layer = parent.gameObject.layer;
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
        rect.localScale = Vector3.one;
    }

    private float FindCharacterTop()
    {
        float top = 0f;
        bool foundBounds = false;

        foreach (Collider characterCollider in GetComponentsInChildren<Collider>())
        {
            if (!characterCollider.enabled || characterCollider.isTrigger) continue;
            top = Mathf.Max(top, transform.InverseTransformPoint(characterCollider.bounds.max).y);
            foundBounds = true;
        }

        foreach (Renderer characterRenderer in GetComponentsInChildren<Renderer>())
        {
            if (!characterRenderer.enabled) continue;
            top = Mathf.Max(top, transform.InverseTransformPoint(characterRenderer.bounds.max).y);
            foundBounds = true;
        }

        return foundBounds ? top : 1.5f;
    }

    private void UpdateFill()
    {
        if (health == null || fillImage == null) return;

        float ratio = health.Max > 0f ? Mathf.Clamp01(health.Current / health.Max) : 0f;
        fillImage.fillAmount = ratio;
        fillImage.color = isPlayer ? PlayerColor : Color.Lerp(LowHealthColor, FullHealthColor, ratio);
    }
}
