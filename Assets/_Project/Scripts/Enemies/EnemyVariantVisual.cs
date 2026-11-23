using UnityEngine;

/// <summary>Swaps the shared enemy prefab's visible model and combat tuning per spawn.</summary>
public sealed class EnemyVariantVisual : MonoBehaviour
{
    [SerializeField] private GameObject[] visualPrefabs = new GameObject[0];
    [SerializeField] private float[] visualHeights = new float[0];
    [SerializeField] private float[] movementSpeeds = new float[0];
    [SerializeField] private float[] healthValues = new float[0];
    [SerializeField] private int[] contactDamages = new int[0];

    public void Configure(GameObject[] visuals, float[] heights, float[] speeds, float[] health, int[] damages)
    {
        visualPrefabs = visuals;
        visualHeights = heights;
        movementSpeeds = speeds;
        healthValues = health;
        contactDamages = damages;
    }

    private void Start()
    {
        int variant = ChooseVariant();
        HidePreviousVisual();
        ApplyCombatStats(variant);

        if (visualPrefabs == null || variant < 0 || variant >= visualPrefabs.Length || visualPrefabs[variant] == null)
            return;

        GameObject visual = Instantiate(visualPrefabs[variant], transform);
        visual.name = "EnemyVisual_" + visualPrefabs[variant].name;
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;
        FitVisualToCollider(visual, GetHeight(variant));
    }

    private int ChooseVariant()
    {
        if (visualPrefabs == null || visualPrefabs.Length == 0) return -1;
        int available = 0;
        for (int i = 0; i < visualPrefabs.Length; i++)
            if (visualPrefabs[i] != null) available++;
        if (available <= 1) return available == 1 ? FirstValidVariant() : -1;

        int choice = Random.Range(0, available);
        for (int i = 0; i < visualPrefabs.Length; i++)
        {
            if (visualPrefabs[i] == null) continue;
            if (choice-- == 0) return i;
        }
        return FirstValidVariant();
    }

    private int FirstValidVariant()
    {
        for (int i = 0; i < visualPrefabs.Length; i++)
            if (visualPrefabs[i] != null) return i;
        return -1;
    }

    private void HidePreviousVisual()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null && renderers[i].transform != transform)
                renderers[i].enabled = false;
    }

    private void ApplyCombatStats(int variant)
    {
        EnemyChaser chaser = GetComponent<EnemyChaser>();
        if (chaser != null && IsValid(movementSpeeds, variant))
            chaser.SetMovementSpeed(movementSpeeds[variant]);

        Health health = GetComponent<Health>();
        if (health != null && IsValid(healthValues, variant))
            health.SetMaximum(healthValues[variant], true);

        EnemyContactDamage contactDamage = GetComponent<EnemyContactDamage>();
        if (contactDamage != null && IsValid(contactDamages, variant))
            contactDamage.SetDamage(contactDamages[variant]);
    }

    private void FitVisualToCollider(GameObject visual, float desiredHeight)
    {
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        if (bounds.size.y <= 0.001f) return;

        visual.transform.localScale = Vector3.one * (desiredHeight / bounds.size.y);
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        Collider body = GetComponent<Collider>();
        float floorY = body != null ? body.bounds.min.y : transform.position.y;
        visual.transform.position += new Vector3(transform.position.x - bounds.center.x,
            floorY - bounds.min.y, transform.position.z - bounds.center.z);
    }

    private float GetHeight(int index) => IsValid(visualHeights, index) ? Mathf.Max(0.1f, visualHeights[index]) : 1.4f;
    private static bool IsValid<T>(T[] values, int index) => values != null && index >= 0 && index < values.Length;
}
