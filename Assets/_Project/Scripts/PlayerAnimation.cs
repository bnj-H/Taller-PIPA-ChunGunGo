using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerAnimation : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    private Rigidbody rb;
    private Animator animator;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (rb == null || animator == null) return;

        Vector3 v = rb.velocity;
        v.y = 0f;
        animator.SetFloat(SpeedHash, v.magnitude);
    }
}
