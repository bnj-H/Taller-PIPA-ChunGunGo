using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 6f;
    [Header("Arena")]
    [SerializeField] private Vector2 arenaHalfExtents = new Vector2(19.25f, 19.25f);
    [SerializeField, Min(0f)] private float boundaryPadding = 0.75f;
    [Header("Camera")]
    [SerializeField, Min(0f)] private float cameraFollowSmoothTime = 0.14f;
    [SerializeField] private Vector3 cameraTargetOffset;

    private Rigidbody rb;
    private Camera cam;
    private Transform cameraRig;
    private Vector3 cameraFollowVelocity;
    private Vector3 moveInput;
    private Vector3 aimDirection;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        cam = Camera.main;
        aimDirection = transform.forward;

        if (cam != null && cam.transform.parent != null)
        {
            cameraRig = cam.transform.parent;
        }

        if (cam == null)
            Debug.LogError("PlayerController: no hay una cámara con la etiqueta MainCamera.", this);
    }

    private void Update()
    {
        ReadMovement();
        ReadAim();
    }

    private void LateUpdate()
    {
        if (cameraRig == null) return;

        Vector3 targetPosition = transform.position + cameraTargetOffset;
        cameraRig.position = Vector3.SmoothDamp(
            cameraRig.position,
            targetPosition,
            ref cameraFollowVelocity,
            cameraFollowSmoothTime);
    }

    /// <summary>Sets the maximum player-center coordinates while preserving Inspector padding.</summary>
    public void SetWorldLimits(Vector2 maximumCoordinates)
    {
        float padding = Mathf.Max(0f, boundaryPadding);
        arenaHalfExtents = new Vector2(
            Mathf.Max(0f, maximumCoordinates.x) + padding,
            Mathf.Max(0f, maximumCoordinates.y) + padding);
    }

    private void ReadMovement()
    {
        if (cam == null) return;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = cam.transform.right;
        right.y = 0f;
        right.Normalize();

        moveInput = (forward * v + right * h).normalized;
    }

    private void ReadAim()
    {
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.up, transform.position);

        if (plane.Raycast(ray, out float distance))
        {
            Vector3 point = ray.GetPoint(distance);
            Vector3 dir = point - transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.01f)
                aimDirection = dir.normalized;
        }
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        Vector3 velocity = moveInput * moveSpeed;
        velocity.y = rb.velocity.y;

        float delta = Time.fixedDeltaTime;
        float limitX = Mathf.Max(0f, arenaHalfExtents.x - boundaryPadding);
        float limitZ = Mathf.Max(0f, arenaHalfExtents.y - boundaryPadding);
        float nextX = Mathf.Clamp(rb.position.x + velocity.x * delta, -limitX, limitX);
        float nextZ = Mathf.Clamp(rb.position.z + velocity.z * delta, -limitZ, limitZ);

        // Limita la velocidad antes de integrar para que el jugador no atraviese el borde.
        velocity.x = delta > 0f ? (nextX - rb.position.x) / delta : 0f;
        velocity.z = delta > 0f ? (nextZ - rb.position.z) / delta : 0f;
        rb.velocity = velocity;

        if (aimDirection.sqrMagnitude > 0.001f)
            rb.MoveRotation(Quaternion.LookRotation(aimDirection, Vector3.up));
    }
}
