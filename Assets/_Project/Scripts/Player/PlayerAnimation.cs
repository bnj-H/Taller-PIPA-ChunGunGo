using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerAnimation : MonoBehaviour
{
    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int MoveZ = Animator.StringToHash("MoveZ");
    private static readonly int Shooting = Animator.StringToHash("IsShooting");
    private static readonly int DieTrigger = Animator.StringToHash("Die");

    [SerializeField] private float moveSpeed = 6f; // igual que PlayerController

    private Rigidbody rb;
    private Animator animator;
    private Health health;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        health = GetComponent<Health>();
        animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }
    }

    private void OnEnable()  { if (health != null) health.Died += OnDied; }
    private void OnDisable() { if (health != null) health.Died -= OnDied; }

    private void Update()
    {
        if (animator == null) return;

        Vector3 v = rb.velocity; v.y = 0f;
        Vector3 local = animator.transform.InverseTransformDirection(v) / moveSpeed;
        animator.SetFloat(MoveX, local.x, 0.1f, Time.deltaTime);
        animator.SetFloat(MoveZ, local.z, 0.1f, Time.deltaTime);

        bool canAct = GameManager.Instance == null || GameManager.Instance.IsPlaying;
        animator.SetBool(Shooting, canAct && Input.GetMouseButton(0));
    }

    private void OnDied(Health h)
    {
        if (animator == null) return;
        animator.transform.SetParent(null, true);
        animator.SetBool(Shooting, false);
        animator.SetTrigger(DieTrigger);
        Destroy(animator.gameObject, 4f);
    }
}