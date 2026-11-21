using UnityEngine;

/// <summary>
/// Controls a physical doorway between adjacent dungeon rooms.
/// Locks during combat and opens when the room is cleared, Isaac-style.
/// </summary>
public class DungeonDoor : MonoBehaviour
{
    [SerializeField] private GameObject barrier;
    [SerializeField] private BoxCollider doorCollider;
    [SerializeField] private Renderer barrierRenderer;
    [SerializeField] private Material lockedMaterial;
    [SerializeField] private Material openMaterial;

    public bool IsLocked { get; private set; }

    public void Setup(GameObject barrierObject, BoxCollider collider, Renderer renderer, Material lockedMat, Material openMat)
    {
        barrier = barrierObject;
        doorCollider = collider;
        barrierRenderer = renderer;
        lockedMaterial = lockedMat;
        openMaterial = openMat;
        Unlock();
    }

    public void Lock()
    {
        IsLocked = true;
        if (barrier != null) barrier.SetActive(true);
        if (doorCollider != null) doorCollider.enabled = true;
        if (barrierRenderer != null && lockedMaterial != null)
            barrierRenderer.sharedMaterial = lockedMaterial;
    }

    public void Unlock()
    {
        IsLocked = false;
        if (barrier != null) barrier.SetActive(false);
        if (doorCollider != null) doorCollider.enabled = false;
        if (barrierRenderer != null && openMaterial != null)
            barrierRenderer.sharedMaterial = openMaterial;
    }
}
