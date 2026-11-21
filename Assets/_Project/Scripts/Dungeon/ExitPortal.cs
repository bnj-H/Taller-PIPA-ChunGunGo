using UnityEngine;

/// <summary>
/// Final room exit portal. Stepping inside finishes Level 1 with victory.
/// </summary>
public class ExitPortal : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 60f;
    [SerializeField] private float bobFrequency = 2f;
    [SerializeField] private float bobHeight = 0.2f;

    private Transform ringVisual;
    private Vector3 basePosition;
    private bool activated;

    public void Setup(Transform ring)
    {
        ringVisual = ring;
        basePosition = ringVisual != null ? ringVisual.localPosition : Vector3.zero;
    }

    private void Update()
    {
        if (ringVisual == null) return;
        ringVisual.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        float offset = Mathf.Sin(Time.time * bobFrequency) * bobHeight;
        ringVisual.localPosition = basePosition + Vector3.up * offset;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (activated) return;
        if (other.CompareTag("Player") || other.GetComponentInParent<PlayerController>() != null)
        {
            activated = true;
            if (DungeonManager.Instance != null)
            {
                DungeonManager.Instance.CompleteLevel();
            }
        }
    }
}
