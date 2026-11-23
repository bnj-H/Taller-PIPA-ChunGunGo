using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Fades only renderers between the gameplay camera and the player.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class CameraOcclusionFader : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField, Min(0.1f)] private float targetHeight = 1.15f;
    [SerializeField, Min(0f)] private float castRadius = 0.18f;
    [SerializeField, Range(0.1f, 0.9f)] private float occludedOpacity = 0.42f;
    [SerializeField, Min(0.05f)] private float fadeSpeed = 5f;
    [SerializeField] private LayerMask occluderMask = ~0;

    private const float RayRefreshInterval = 0.04f;
    private readonly RaycastHit[] hits = new RaycastHit[48];
    private readonly HashSet<Renderer> currentBlockers = new HashSet<Renderer>();
    private readonly Dictionary<Renderer, FadeState> states = new Dictionary<Renderer, FadeState>();
    private readonly List<Renderer> stateKeys = new List<Renderer>();
    private Camera targetCamera;
    private float nextRayTime;
    private int groundLayer = -1;

    private sealed class FadeState
    {
        public Renderer Renderer;
        public Material[] OriginalMaterials;
        public Material[] FadeMaterials;
        public Color[] BaseColors;
        public bool[] UsesBaseColor;
        public float Opacity = 1f;
    }

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        groundLayer = LayerMask.NameToLayer("Ground");
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) target = player.transform;
        }

        if (Time.unscaledTime >= nextRayTime)
        {
            nextRayTime = Time.unscaledTime + RayRefreshInterval;
            FindBlockers();
        }

        UpdateFades(Time.unscaledDeltaTime);
    }

    private void FindBlockers()
    {
        currentBlockers.Clear();
        if (target == null || targetCamera == null) return;

        Vector3 destination = target.position + Vector3.up * targetHeight;
        Vector3 offset = destination - targetCamera.transform.position;
        float distance = offset.magnitude;
        if (distance < 0.05f) return;

        int count = Physics.SphereCastNonAlloc(targetCamera.transform.position, castRadius,
            offset / distance, hits, distance, occluderMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null || hitCollider.isTrigger) continue;
            if (groundLayer >= 0 && hitCollider.gameObject.layer == groundLayer) continue;

            Transform hitTransform = hitCollider.transform;
            if (hitTransform == target || hitTransform.IsChildOf(target)) continue;
            if (IsGameplayObject(hitTransform)) continue;

            Renderer renderer = hitCollider.GetComponent<Renderer>();
            if (renderer == null) renderer = hitCollider.GetComponentInChildren<Renderer>(true);
            if (renderer == null || !renderer.enabled || IsGameplayObject(renderer.transform)) continue;
            currentBlockers.Add(renderer);
            if (!states.ContainsKey(renderer)) TryCreateState(renderer);
        }
    }

    private static bool IsGameplayObject(Transform candidate)
    {
        if (candidate == null) return true;
        return candidate.GetComponentInParent<Health>() != null ||
               candidate.GetComponentInParent<PlayerController>() != null ||
               candidate.GetComponentInParent<EnemyChaser>() != null ||
               candidate.GetComponentInParent<HealthPickup>() != null ||
               candidate.GetComponentInParent<ExitPortal>() != null ||
               candidate.GetComponentInParent<DungeonDoor>() != null;
    }

    private void TryCreateState(Renderer renderer)
    {
        Material[] originals = renderer.sharedMaterials;
        if (originals == null || originals.Length == 0) return;

        FadeState state = new FadeState
        {
            Renderer = renderer,
            OriginalMaterials = originals,
            FadeMaterials = new Material[originals.Length],
            BaseColors = new Color[originals.Length],
            UsesBaseColor = new bool[originals.Length]
        };

        bool anySupported = false;
        for (int i = 0; i < originals.Length; i++)
        {
            Material original = originals[i];
            if (original == null) continue;
            Material fadeMaterial = new Material(original)
            {
                name = original.name + " (Occlusion Fade)"
            };
            if (!ConfigureTransparency(fadeMaterial))
            {
                Destroy(fadeMaterial);
                state.FadeMaterials[i] = original;
                continue;
            }

            anySupported = true;
            string colorProperty = fadeMaterial.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
            state.BaseColors[i] = fadeMaterial.GetColor(colorProperty);
            state.UsesBaseColor[i] = true;
            state.FadeMaterials[i] = fadeMaterial;
        }

        if (!anySupported)
        {
            DestroyMaterials(state.FadeMaterials, originals);
            return;
        }

        renderer.sharedMaterials = state.FadeMaterials;
        states.Add(renderer, state);
    }

    private static bool ConfigureTransparency(Material material)
    {
        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }
        else if (material.HasProperty("_Mode"))
        {
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
        }
        else return false;

        return material.HasProperty("_BaseColor") || material.HasProperty("_Color");
    }

    private void UpdateFades(float deltaTime)
    {
        stateKeys.Clear();
        stateKeys.AddRange(states.Keys);
        for (int i = 0; i < stateKeys.Count; i++)
        {
            Renderer renderer = stateKeys[i];
            if (!states.TryGetValue(renderer, out FadeState state)) continue;
            if (renderer == null)
            {
                DestroyMaterials(state.FadeMaterials, state.OriginalMaterials);
                states.Remove(renderer);
                continue;
            }

            float desired = currentBlockers.Contains(renderer) ? occludedOpacity : 1f;
            state.Opacity = Mathf.MoveTowards(state.Opacity, desired, fadeSpeed * deltaTime);
            if (state.Opacity >= 0.999f && desired >= 0.999f)
            {
                renderer.sharedMaterials = state.OriginalMaterials;
                DestroyMaterials(state.FadeMaterials, state.OriginalMaterials);
                states.Remove(renderer);
                continue;
            }

            ApplyOpacity(state);
        }
    }

    private static void ApplyOpacity(FadeState state)
    {
        for (int i = 0; i < state.FadeMaterials.Length; i++)
        {
            Material material = state.FadeMaterials[i];
            if (material == null || !state.UsesBaseColor[i]) continue;
            Color color = state.BaseColors[i];
            color.a *= state.Opacity;
            material.SetColor(material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color", color);
        }
    }

    private static void DestroyMaterials(Material[] candidates, Material[] originals)
    {
        if (candidates == null) return;
        for (int i = 0; i < candidates.Length; i++)
        {
            Material candidate = candidates[i];
            if (candidate == null || (originals != null && System.Array.IndexOf(originals, candidate) >= 0)) continue;
            Destroy(candidate);
        }
    }

    private void OnDisable()
    {
        foreach (KeyValuePair<Renderer, FadeState> entry in states)
        {
            if (entry.Key != null) entry.Key.sharedMaterials = entry.Value.OriginalMaterials;
            DestroyMaterials(entry.Value.FadeMaterials, entry.Value.OriginalMaterials);
        }
        states.Clear();
    }
}
