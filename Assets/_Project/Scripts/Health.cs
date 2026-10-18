using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Health : MonoBehaviour, IDamageable
{
    // float permite aplicar reducciones exactas como 20% (3 -> 2.4 HP).
    [SerializeField, Min(0.1f)] private float maxHealth = 2f;
    [SerializeField] private float killY = -6.5f;
    [SerializeField] private float current;

    public float Current => current;
    public float Max => maxHealth;
    public bool FellOff { get; private set; }
    public event Action<Health> Died;

    private bool dead;

    private void Awake()
    {
        maxHealth = Mathf.Max(0.1f, maxHealth);
        current = maxHealth;

        if (GetComponent<WorldHealthBar>() == null)
            gameObject.AddComponent<WorldHealthBar>();
    }

    private void Update()
    {
        if (!dead && transform.position.y < killY)
        {
            FellOff = true;
            Die();
        }
    }

    public bool TakeDamage(int amount)
    {
        if (dead || amount <= 0) return false;

        bool lethalHit = current - amount <= 0f;
        if (!CompareTag("Player")) DamageNumberPopup.Show(transform, amount, lethalHit);
        current -= amount;
        if (current <= 0f)
        {
            current = 0f;
            Die();
            return false;
        }

        return true;
    }

    private void Die()
    {
        if (dead) return;

        dead = true;
        Died?.Invoke(this);
        Destroy(gameObject);
    }
}

public sealed class DamageNumberPopup : MonoBehaviour
{
    private static readonly Stack<DamageNumberPopup> Pool = new Stack<DamageNumberPopup>();
    private const int PoolLimit = 32;
    private const float Lifetime = 0.5f;
    private const float RiseDistance = 0.7f;
    private static TMP_FontAsset sharedFont;
    private static Material sharedOutlineMaterial;

    private TextMeshPro label;
    private float elapsed;
    private Vector3 startPosition;
    private Vector3 drift;
    private Camera targetCamera;
    private bool lethal;

    public static void Show(Transform target, int damage, bool isLethal)
    {
        if (target == null || damage <= 0) return;

        DamageNumberPopup popup = null;
        while (Pool.Count > 0 && popup == null)
            popup = Pool.Pop();
        if (popup == null)
        {
            GameObject go = new GameObject("DamageNumber");
            popup = go.AddComponent<DamageNumberPopup>();
        }

        popup.Prepare(target, damage, isLethal);
    }

    private void Awake()
    {
        label = gameObject.AddComponent<TextMeshPro>();
        if (sharedFont == null)
            sharedFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (sharedOutlineMaterial == null)
            sharedOutlineMaterial = Resources.Load<Material>("Fonts & Materials/LiberationSans SDF - Outline");
        label.font = sharedFont != null ? sharedFont : TMP_Settings.defaultFontAsset;
        if (sharedOutlineMaterial != null) label.fontSharedMaterial = sharedOutlineMaterial;
        label.fontSize = 36f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.extraPadding = true;
        label.rectTransform.sizeDelta = new Vector2(3f, 1.2f);

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.sortingOrder = 100;
        gameObject.SetActive(false);
    }

    private void Prepare(Transform target, int damage, bool isLethal)
    {
        lethal = isLethal;
        elapsed = 0f;
        targetCamera = Camera.main;
        drift = new Vector3(UnityEngine.Random.Range(-0.22f, 0.22f), 0f, UnityEngine.Random.Range(-0.12f, 0.12f));

        float top = 1.5f;
        Collider[] colliders = target.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null && colliders[i].enabled)
                top = Mathf.Max(top, target.InverseTransformPoint(colliders[i].bounds.max).y + 0.25f);

        startPosition = target.position + Vector3.up * top;
        label.text = "-" + damage;
        label.color = lethal ? new Color(1f, 0.22f, 0.08f, 1f) : new Color(1f, 0.88f, 0.08f, 1f);
        transform.position = startPosition;
        transform.localScale = Vector3.one * 1.2f;
        gameObject.SetActive(true);
    }

    private void LateUpdate()
    {
        elapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(elapsed / Lifetime);
        transform.position = startPosition + Vector3.up * (RiseDistance * t) + drift * t;
        float pop = t < 0.16f ? Mathf.Lerp(1.35f, 1.05f, t / 0.16f) : Mathf.Lerp(1.05f, 0.92f, (t - 0.16f) / 0.84f);
        transform.localScale = Vector3.one * pop;

        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera != null)
            transform.rotation = Quaternion.LookRotation(transform.position - targetCamera.transform.position, Vector3.up);

        Color color = lethal ? new Color(1f, 0.22f, 0.08f, 1f) : new Color(1f, 0.88f, 0.08f, 1f);
        color.a = 1f - Mathf.SmoothStep(0.45f, 1f, t);
        label.color = color;
        if (elapsed >= Lifetime) Recycle();
    }

    private void Recycle()
    {
        gameObject.SetActive(false);
        if (Pool.Count < PoolLimit) Pool.Push(this);
        else Destroy(gameObject);
    }
}
