using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyChaser : MonoBehaviour, IKnockbackable
{
    [SerializeField] private float speed = 3f;
    [SerializeField] private float stunTime = 0.4f;

    [SerializeField] private float stopChaseBelowY = 1f;

    private Rigidbody rb;
    private Transform target;
    private float stunUntil;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null) target = player.transform;
    }

    public void ApplyKnockback(Vector3 direction, float force)
    {
        stunUntil = Time.time + stunTime;
        direction.y = 0.3f;
        rb.AddForce(direction.normalized * force, ForceMode.Impulse);
    }

    private void FixedUpdate()
    {
        if (target == null || Time.time < stunUntil) return;
        if (transform.position.y < stopChaseBelowY) return;
        
        Vector3 dir = target.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude > 0.01f)
            rb.MoveRotation(Quaternion.LookRotation(dir.normalized));
       
        Vector3 vel = dir.normalized * speed;
        rb.velocity = new Vector3(vel.x, rb.velocity.y, vel.z);
    }
}