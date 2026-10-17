using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 6f;

    private Rigidbody rb;
    private Camera cam;
    private Vector3 moveInput;
    private Vector3 aimDirection;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        cam = Camera.main;
        aimDirection = transform.forward;
    }

    private void Update()
    {
        ReadMovement();
        ReadAim();
    }

    private void ReadMovement()
    {
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
        Vector3 vel = moveInput * moveSpeed;
        vel.y = rb.velocity.y;
        rb.velocity = vel;

        rb.MoveRotation(Quaternion.LookRotation(aimDirection));
    }
}