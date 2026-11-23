using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class HealthPickup : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float healAmount = 1f;
    [SerializeField] private Transform visual;
    [SerializeField, Min(0f)] private float bobHeight = 0.12f;
    [SerializeField, Min(0f)] private float rotateSpeed = 65f;

    private Vector3 visualBasePosition;
    private bool collected;

    private void Awake()
    {
        Collider trigger = GetComponent<Collider>();
        trigger.isTrigger = true;
        if (visual == null && transform.childCount > 0)
            visual = transform.GetChild(0);
        if (visual != null)
            visualBasePosition = visual.localPosition;
    }

    private void Update()
    {
        if (visual == null) return;
        float t = Time.time;
        visual.localPosition = visualBasePosition + Vector3.up * (Mathf.Sin(t * 2.6f) * bobHeight);
        visual.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.Self);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected || !other.CompareTag("Player")) return;
        Health health = other.GetComponentInParent<Health>();
        if (health == null) return;

        float restored = health.Heal(healAmount);
        if (restored <= 0f) return;

        collected = true;
        PickupFeedback.Show(transform.position + Vector3.up * 0.8f, "+" + restored.ToString("0.#") + " VIDA");
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayHit();
        Destroy(gameObject);
    }
}

internal sealed class PickupFeedback : MonoBehaviour
{
    private const float Lifetime = 0.85f;
    private TextMeshPro label;
    private float elapsed;
    private Vector3 startPosition;

    public static void Show(Vector3 position, string message)
    {
        GameObject go = new GameObject("PickupFeedback");
        PickupFeedback feedback = go.AddComponent<PickupFeedback>();
        feedback.Initialize(position, message);
    }

    private void Awake()
    {
        label = gameObject.AddComponent<TextMeshPro>();
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = 34f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.extraPadding = true;
        label.rectTransform.sizeDelta = new Vector2(3.5f, 0.85f);
        label.color = new Color(0.28f, 1f, 0.55f, 1f);
        label.outlineWidth = 0.25f;
        label.outlineColor = new Color(0.02f, 0.16f, 0.07f, 1f);
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = 120;
        }
    }

    private void Initialize(Vector3 position, string message)
    {
        startPosition = position;
        transform.position = position;
        transform.localScale = Vector3.one * 0.65f;
        label.text = message;
    }

    private void LateUpdate()
    {
        elapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(elapsed / Lifetime);
        transform.position = startPosition + Vector3.up * (0.45f * t);
        if (Camera.main != null)
            transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position, Vector3.up);
        Color color = label.color;
        color.a = 1f - t;
        label.color = color;
        if (elapsed >= Lifetime) Destroy(gameObject);
    }
}
