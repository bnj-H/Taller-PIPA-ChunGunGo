using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Secondary motion para la cola del chungungo.
///
/// La cola (Tail_01..Tail_05) cuelga de Hips y las animaciones de Mixamo no la tocan, asi que
/// por defecto queda rigida. Este script simula cada hueso como una particula unida por un
/// resorte a su posicion "animada" (la pose de reposo pegada a las caderas):
///   - cuando el personaje se mueve o gira, la cola se queda atras y luego se acomoda (inercia);
///   - la punta es mas suelta que la base (frecuencia del resorte baja hacia la punta);
///   - cada segmento mantiene su largo y no puede doblarse mas de Max Angle respecto a su pose.
///
/// Se pone en el objeto que tiene el Animator (Visual). Tail_01 es el ancla y no se simula.
/// Corre despues de PlayerController (que gira el VisualPivot en su LateUpdate).
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public class TailSecondaryMotion : MonoBehaviour
{
    [Header("Cadena (se autodetecta por nombre)")]
    [Tooltip("Primer hueso de la cola. Sigue a las caderas y no se simula; los siguientes si.")]
    [SerializeField] private Transform tailRoot;
    [SerializeField] private string rootBoneName = "Tail_01";

    [Header("Resorte")]
    [Tooltip("Frecuencia (Hz) del resorte en la base. Mas alto = base mas rigida.")]
    [SerializeField, Min(0.1f)] private float baseFrequency = 3.5f;
    [Tooltip("Frecuencia (Hz) en la punta. Mas bajo = punta mas suelta y con mas retraso.")]
    [SerializeField, Min(0.1f)] private float tipFrequency = 1.7f;
    [Tooltip("0.1 = rebota mucho, 1 = se frena sin rebotar.")]
    [SerializeField, Range(0.05f, 1.5f)] private float dampingRatio = 0.3f;

    [Header("Fuerzas")]
    [Tooltip("Gravedad sobre la cola en m/s2. Hace que caiga un poco.")]
    [SerializeField] private float gravity = 4f;

    [Header("Limites")]
    [Tooltip("Cuanto puede desviarse cada segmento (grados) respecto a su direccion animada.")]
    [SerializeField, Range(5f, 120f)] private float maxAngle = 55f;
    [Tooltip("Evita que la cola atraviese el piso (usa la altura de los pies de este objeto).")]
    [SerializeField] private bool keepAboveFeet = true;
    [SerializeField, Min(0f)] private float minHeightAboveFeet = 0.06f;

    [Header("Simulacion")]
    [Tooltip("Usar el mismo reloj que el Animator del jugador (UnscaledTime).")]
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField, Min(0.002f)] private float maxSubstep = 1f / 120f;
    [Tooltip("Si el personaje salta mas que esto en un frame (respawn/teletransporte), la cola se reinicia.")]
    [SerializeField, Min(0.5f)] private float teleportDistance = 3f;
    [Tooltip("Evita que la malla de la cola desaparezca por culling cuando se mueve fuera de su caja original.")]
    [SerializeField] private bool expandCullingBounds = true;
    [SerializeField] private bool drawGizmos;

    private const float MaxSpeed = 40f;

    private Transform[] nodes;
    private Quaternion[] restLocalRotation;
    private Vector3[] pos, vel, old, target, prevTarget, goal;
    private float[] restLength;
    private int count;
    private bool initialized;

    private void Awake()
    {
        Build();
    }

    private void OnEnable()
    {
        initialized = false;
    }

    private void OnDisable()
    {
        // Al apagar el script la cola vuelve a su pose rigida.
        if (nodes == null) return;
        for (int i = 0; i < count; i++)
            if (nodes[i] != null) nodes[i].localRotation = restLocalRotation[i];
    }

    private void Build()
    {
        if (tailRoot == null)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == rootBoneName) { tailRoot = t; break; }
            }
        }

        if (tailRoot == null)
        {
            Debug.LogWarning("TailSecondaryMotion: no encontre el hueso '" + rootBoneName + "'.", this);
            enabled = false;
            return;
        }

        // Cadena lineal: Tail_01 -> Tail_02 -> ... -> Tail_05 -> Tail_05_end
        var list = new List<Transform>();
        Transform node = tailRoot;
        while (node != null)
        {
            list.Add(node);
            node = node.childCount > 0 ? node.GetChild(0) : null;
        }

        if (list.Count < 3)
        {
            Debug.LogWarning("TailSecondaryMotion: la cadena de la cola tiene menos de 3 huesos.", this);
            enabled = false;
            return;
        }

        count = list.Count;
        nodes = list.ToArray();
        restLocalRotation = new Quaternion[count];
        pos = new Vector3[count];
        vel = new Vector3[count];
        old = new Vector3[count];
        target = new Vector3[count];
        prevTarget = new Vector3[count];
        goal = new Vector3[count];
        restLength = new float[count];
        for (int i = 0; i < count; i++) restLocalRotation[i] = nodes[i].localRotation;

        if (expandCullingBounds)
        {
            foreach (SkinnedMeshRenderer smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                string n = smr.name;
                if (n.IndexOf("cola", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("tail", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    smr.updateWhenOffscreen = true;
            }
        }
    }

    private void LateUpdate()
    {
        if (count < 3) return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        // 1) Pose animada: huesos de la cola en reposo, pegados a las caderas.
        for (int i = 0; i < count; i++) nodes[i].localRotation = restLocalRotation[i];
        for (int i = 0; i < count; i++) target[i] = nodes[i].position;
        for (int i = 1; i < count; i++) restLength[i] = Vector3.Distance(target[i], target[i - 1]);

        float tp = teleportDistance;
        if (!initialized || dt > 0.25f || (target[0] - prevTarget[0]).sqrMagnitude > tp * tp)
            Snap();

        // 2) Simulacion (resortes + restricciones).
        if (dt > 1e-5f) Simulate(dt);
        System.Array.Copy(target, prevTarget, count);

        // 3) Llevar la simulacion a las rotaciones de los huesos.
        ApplyPose();
    }

    private void Snap()
    {
        for (int i = 0; i < count; i++)
        {
            pos[i] = target[i];
            prevTarget[i] = target[i];
            vel[i] = Vector3.zero;
        }
        initialized = true;
    }

    private void Simulate(float dt)
    {
        int steps = Mathf.Clamp(Mathf.CeilToInt(dt / maxSubstep), 1, 12);
        float h = dt / steps;
        float inv = 1f / steps;
        float minY = keepAboveFeet ? transform.position.y + minHeightAboveFeet : float.NegativeInfinity;
        Vector3 g = Vector3.down * gravity;
        float angleLimit = Mathf.Max(maxAngle, 1f);

        for (int s = 1; s <= steps; s++)
        {
            float a = s * inv;
            for (int i = 0; i < count; i++) goal[i] = Vector3.Lerp(prevTarget[i], target[i], a);
            pos[0] = goal[0];

            // Integracion: resorte amortiguado hacia la pose animada + gravedad.
            for (int i = 1; i < count; i++)
            {
                float u = (i - 1) / (float)(count - 2);
                float w = 2f * Mathf.PI * Mathf.Lerp(baseFrequency, tipFrequency, u);
                Vector3 goalVel = (target[i] - prevTarget[i]) / dt;
                Vector3 acc = (w * w) * (goal[i] - pos[i])
                            + (2f * dampingRatio * w) * (goalVel - vel[i])
                            + g;
                vel[i] += acc * h;
                old[i] = pos[i];
                pos[i] += vel[i] * h;
            }

            // Restricciones: largo del segmento, angulo maximo y piso.
            for (int i = 1; i < count; i++)
            {
                Vector3 refDir = (goal[i] - goal[i - 1]).normalized;
                Vector3 d = pos[i] - pos[i - 1];
                Vector3 dir = d.sqrMagnitude > 1e-10f ? d.normalized : refDir;

                float ang = Vector3.Angle(refDir, dir);
                if (ang > angleLimit) dir = Vector3.Slerp(refDir, dir, angleLimit / ang);

                pos[i] = pos[i - 1] + dir * restLength[i];
                if (pos[i].y < minY) pos[i].y = minY;

                vel[i] = Vector3.ClampMagnitude((pos[i] - old[i]) / h, MaxSpeed);
            }
        }
    }

    private void ApplyPose()
    {
        // De la base a la punta: cada hueso apunta hacia la particula siguiente.
        for (int i = 0; i < count - 1; i++)
        {
            Transform b = nodes[i];
            Vector3 current = nodes[i + 1].position - b.position;
            Vector3 desired = pos[i + 1] - b.position;
            if (current.sqrMagnitude < 1e-10f || desired.sqrMagnitude < 1e-10f) continue;
            b.rotation = Quaternion.FromToRotation(current, desired) * b.rotation;
        }
    }

    [ContextMenu("Reiniciar cola")]
    private void ResetTail()
    {
        initialized = false;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos || !Application.isPlaying || pos == null || !initialized) return;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < count; i++)
        {
            Gizmos.DrawSphere(pos[i], 0.04f);
            if (i > 0) Gizmos.DrawLine(pos[i - 1], pos[i]);
        }
    }
}
