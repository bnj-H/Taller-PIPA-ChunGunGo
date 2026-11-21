using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyChaser : MonoBehaviour, IKnockbackable
{
    [SerializeField, Min(0f)] private float speed = 3f;
    [SerializeField, Min(0f)] private float stunTime = 0.4f;
    [SerializeField] private float stopChaseBelowY = -4.5f;
    [Header("Obstacle avoidance")]
    [SerializeField, Min(0.1f)] private float lookAheadDistance = 2.2f;
    [SerializeField, Min(0.05f)] private float avoidanceRadius = 0.48f;
    [SerializeField, Min(0.1f)] private float turnRate = 420f;
    [SerializeField, Min(0.02f)] private float steeringRefreshInterval = 0.12f;
    [SerializeField, Min(0f)] private float stopDistance = 0.25f;
    [SerializeField, Min(0.1f)] private float stuckCheckInterval = 0.6f;
    [SerializeField, Min(0.1f)] private float stuckRecoveryTime = 0.75f;
    [SerializeField] private LayerMask obstacleLayers = ~0;

    private readonly RaycastHit[] castHits = new RaycastHit[16];
    private static readonly float[] CandidateAngles = { 35f, -35f, 70f, -70f, 110f, -110f, 150f, -150f, 180f };
    private Rigidbody rb;
    private Transform target;
    private float stunUntil;
    private float nextTargetSearch;
    private float nextStuckCheck;
    private float nextSteeringUpdate;
    private Vector3 cachedSteering;
    private float stuckTime;
    private float recoveryUntil;
    private float sideBias;
    private Vector3 lastPosition;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        sideBias = (GetInstanceID() & 1) == 0 ? 1f : -1f;
        lastPosition = rb.position;
    }

    private void Start() => FindTarget();

    private void FindTarget()
    {
        GameObject player = GameObject.FindWithTag("Player");
        target = player != null ? player.transform : null;
        nextTargetSearch = Time.time + 1f;
    }

    public void ApplyKnockback(Vector3 direction, float force)
    {
        stunUntil = Time.time + stunTime;
        direction.y = 0.3f;
        rb.AddForce(direction.normalized * force, ForceMode.Impulse);
        stuckTime = 0f;
    }

    private void FixedUpdate()
    {
        if (target == null)
        {
            if (Time.time >= nextTargetSearch) FindTarget();
            return;
        }
        if (Time.time < stunUntil || transform.position.y < stopChaseBelowY) return;

        Vector3 toTarget = target.position - rb.position;
        toTarget.y = 0f;
        float targetDistance = toTarget.magnitude;
        if (targetDistance <= stopDistance)
        {
            rb.velocity = new Vector3(0f, rb.velocity.y, 0f);
            return;
        }
        Vector3 desired = toTarget / Mathf.Max(targetDistance, 0.001f);
        Vector3 origin = rb.position + Vector3.up * 0.65f;
        float probeDistance = Mathf.Min(lookAheadDistance, targetDistance);
        DetectStuck(desired);
        if (Time.time >= nextSteeringUpdate || cachedSteering.sqrMagnitude < 0.001f)
        {
            cachedSteering = ChooseSteering(desired, origin, probeDistance);
            if (Time.time < recoveryUntil)
            {
                Vector3 escape = Quaternion.Euler(0f, 75f * sideBias, 0f) * desired;
                if (CastClearance(origin, escape, probeDistance) < avoidanceRadius * 1.8f)
                {
                    sideBias *= -1f;
                    escape = Quaternion.Euler(0f, 75f * sideBias, 0f) * desired;
                }
                cachedSteering = escape;
            }
            nextSteeringUpdate = Time.time + steeringRefreshInterval;
        }
        Vector3 steering = cachedSteering;
        steering.y = 0f;
        if (steering.sqrMagnitude < 0.001f) steering = desired;
        steering.Normalize();
        Vector3 facing = Vector3.RotateTowards(transform.forward, steering, turnRate * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f);
        facing.y = 0f;
        if (facing.sqrMagnitude > 0.001f) rb.MoveRotation(Quaternion.LookRotation(facing, Vector3.up));
        rb.velocity = new Vector3(steering.x * speed, rb.velocity.y, steering.z * speed);
    }

    private Vector3 ChooseSteering(Vector3 desired, Vector3 origin, float probeDistance)
    {
        float directClearance = CastClearance(origin, desired, probeDistance);
        if (directClearance >= probeDistance - 0.02f) return desired;

        float bestScore = float.NegativeInfinity;
        Vector3 best = desired;
        for (int i = 0; i < CandidateAngles.Length; i++)
        {
            float angle = CandidateAngles[i];
            Vector3 candidate = Quaternion.Euler(0f, angle, 0f) * desired;
            float clearance = CastClearance(origin, candidate, probeDistance);
            float alignment = Vector3.Dot(candidate, desired);
            float sidePreference = Mathf.Sign(angle) * sideBias;
            float score = alignment * 1.6f + clearance / Mathf.Max(probeDistance, 0.01f) * 1.2f + sidePreference * 0.035f;
            if (clearance < avoidanceRadius * 1.5f) score -= 2f;
            if (score > bestScore) { bestScore = score; best = candidate; }
        }
        return best;
    }

    private float CastClearance(Vector3 origin, Vector3 direction, float distance)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return 0f;
        direction.Normalize();
        int count = Physics.SphereCastNonAlloc(origin, avoidanceRadius, direction, castHits,
            distance, obstacleLayers, QueryTriggerInteraction.Ignore);
        float nearest = distance;
        for (int i = 0; i < count; i++)
        {
            Collider hitCollider = castHits[i].collider;
            if (hitCollider == null || hitCollider.attachedRigidbody == rb) continue;
            if (target != null && (hitCollider.transform == target || hitCollider.transform.IsChildOf(target))) continue;
            if (castHits[i].distance < nearest) nearest = castHits[i].distance;
        }
        return nearest;
    }

    private void DetectStuck(Vector3 desired)
    {
        if (Time.time < nextStuckCheck) return;
        float interval = Mathf.Max(0.1f, stuckCheckInterval);
        Vector3 moved = rb.position - lastPosition;
        moved.y = 0f;
        if (desired.sqrMagnitude > 0.01f && moved.sqrMagnitude < 0.025f * 0.025f)
        {
            stuckTime += interval;
            if (stuckTime >= stuckRecoveryTime)
            {
                sideBias *= -1f;
                recoveryUntil = Time.time + 0.85f;
                stuckTime = 0f;
            }
        }
        else stuckTime = 0f;
        lastPosition = rb.position;
        nextStuckCheck = Time.time + interval;
    }
}



