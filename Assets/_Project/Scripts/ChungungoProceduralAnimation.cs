using UnityEngine;

/// <summary>Low-cost procedural motion for the skinned Chungungo rig.</summary>
public sealed class ChungungoProceduralAnimation : MonoBehaviour
{
    [SerializeField, Min(0f)] private float walkBobHeight = 0.045f;
    [SerializeField, Min(0.1f)] private float walkFrequency = 9f;
    [SerializeField, Range(0f, 20f)] private float aimLeanDegrees = 8f;
    [SerializeField, Range(0f, 30f)] private float recoilDegrees = 10f;
    [SerializeField, Min(0.05f)] private float shotInterval = 0.25f;
    [SerializeField, Min(0.01f)] private float recoilDuration = 0.14f;
    [SerializeField, Range(0f, 35f)] private float legSwingDegrees = 22f;
    [SerializeField] private bool holdWeaponPoseWhenIdle = true;

    private Transform model;
    private Rigidbody playerBody;
    private Vector3 restPosition;
    private Quaternion restRotation;
    private float walkPhase;
    private float tailSway;
    private float tailSwayVelocity;
    private float nextShotTime;
    private float lastShotTime = -10f;
    private Transform spine, leftArm, rightArm, leftForearm, rightForearm;
    private Transform leftThigh, rightThigh, leftShin, rightShin, tail, tailTip;
    private readonly System.Collections.Generic.Dictionary<Transform, Quaternion> restBones = new();

    private void Awake()
    {
        model = transform.Find("Model");
        playerBody = GetComponentInParent<Rigidbody>();
        if (model == null) return;
        restPosition = model.localPosition;
        CreateWeaponVisual();
        restRotation = model.localRotation;
        foreach (Transform bone in model.GetComponentsInChildren<Transform>(true))
        {
            string n = bone.name;
            restBones[bone] = bone.localRotation;
            if (n.EndsWith("Spine")) spine = bone;
            else if (n.EndsWith("UpperArm.L")) leftArm = bone;
            else if (n.EndsWith("UpperArm.R")) rightArm = bone;
            else if (n.EndsWith("Forearm.L")) leftForearm = bone;
            else if (n.EndsWith("Forearm.R")) rightForearm = bone;
            else if (n.EndsWith("Thigh.L")) leftThigh = bone;
            else if (n.EndsWith("Thigh.R")) rightThigh = bone;
            else if (n.EndsWith("Shin.L")) leftShin = bone;
            else if (n.EndsWith("Shin.R")) rightShin = bone;
            else if (n.EndsWith("TailTip")) tailTip = bone;
            else if (n.EndsWith("Tail")) tail = bone;
        }
    }

    private void LateUpdate()
    {
        if (model == null) return;
        Vector3 velocity = playerBody != null ? playerBody.velocity : Vector3.zero;
        velocity.y = 0f;
        float moving = Mathf.Clamp01(velocity.magnitude / 6f);
        if (moving > 0.02f) walkPhase += Time.deltaTime * walkFrequency;
        else walkPhase = 0f;

        bool canAct = GameManager.Instance == null || GameManager.Instance.IsPlaying;
        bool firing = canAct && Input.GetMouseButton(0);
        if (firing && Time.time >= nextShotTime)
        {
            lastShotTime = Time.time;
            nextShotTime = Time.time + shotInterval;
        }
        float aim = (firing || holdWeaponPoseWhenIdle) ? 1f : 0f;
        float step = Mathf.Sin(walkPhase) * moving;
        float bob = Mathf.Abs(Mathf.Sin(walkPhase)) * walkBobHeight * moving;
        float sway = Mathf.Sin(walkPhase * 0.5f) * 2f * moving;
        float recoilAge = Time.time - lastShotTime;
        float recoil = recoilAge < recoilDuration ? recoilDegrees * (1f - recoilAge / recoilDuration) : 0f;
        model.localPosition = restPosition + Vector3.up * (bob - 0.025f * aim);
        model.localRotation = restRotation * Quaternion.Euler(-aimLeanDegrees * aim + recoil, 0f, sway);

        Pose(spine, new Vector3(-4f * aim, 0f, 0f));
        // Arm bones bend forward into a two-handed aiming pose; small alternating motion when walking.
        Pose(leftArm, Vector3.zero);
        Pose(rightArm, Vector3.zero);
        Pose(leftForearm, Vector3.zero);
        Pose(rightForearm, Vector3.zero);
        if (aim > 0.01f)
        {
            AimArm(leftArm, leftForearm, true, step);
            AimArm(rightArm, rightForearm, false, step);
        }
        Pose(leftThigh, new Vector3(legSwingDegrees * step, 0f, 0f));
        Pose(rightThigh, new Vector3(-legSwingDegrees * step, 0f, 0f));
        Pose(leftShin, new Vector3(Mathf.Max(0f, -step) * 18f * moving, 0f, 0f));
        Pose(rightShin, new Vector3(Mathf.Max(0f, step) * 18f * moving, 0f, 0f));
        float tailTarget = Mathf.Sin(walkPhase * 0.5f) * 12f * moving;
        tailSway = Mathf.SmoothDamp(tailSway, tailTarget, ref tailSwayVelocity, 0.22f);
        Pose(tail, new Vector3(0f, 0f, tailSway));
        Pose(tailTip, new Vector3(0f, 0f, tailSway * 0.7f));
    }

    private void CreateWeaponVisual()
    {
        Transform muzzle = transform.parent != null ? transform.parent.Find("Muzzle") : null;
        if (muzzle == null || muzzle.Find("ChungungoWeapon") != null) return;
        Transform weapon = new GameObject("ChungungoWeapon").transform;
        weapon.SetParent(muzzle, false);
        Material body = MakeWeaponMaterial(new Color(0.08f, 0.28f, 0.62f));
        Material dark = MakeWeaponMaterial(new Color(0.12f, 0.15f, 0.18f));
        Material accent = MakeWeaponMaterial(new Color(0.78f, 0.82f, 0.85f));
        WeaponPart(weapon, "Receiver", new Vector3(0f, 0f, -0.32f), new Vector3(0.30f, 0.20f, 0.42f), body);
        WeaponPart(weapon, "Barrel", new Vector3(0f, 0.015f, -0.08f), new Vector3(0.12f, 0.12f, 0.24f), dark);
        WeaponPart(weapon, "Grip", new Vector3(0f, -0.15f, -0.30f), new Vector3(0.13f, 0.24f, 0.14f), dark).localRotation = Quaternion.Euler(-12f, 0f, 0f);
        WeaponPart(weapon, "Sight", new Vector3(0f, 0.13f, -0.26f), new Vector3(0.08f, 0.06f, 0.10f), accent);
    }

    private static Material MakeWeaponMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        var material = new Material(shader) { color = color };
        return material;
    }

    private static Transform WeaponPart(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        var collider = part.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        var renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        return part.transform;
    }
    private void AimArm(Transform upperArm, Transform forearm, bool left, float step)
    {
        if (upperArm == null || forearm == null) return;

        Transform root = model.parent != null ? model.parent : model;
        Vector3 forward = root.forward;
        Vector3 inward = root.right * (left ? 1f : -1f);
        Vector3 upperDirection = (forward * 0.82f + inward * (0.36f + step * 0.04f) - root.up * 0.03f).normalized;
        AlignBone(upperArm, forearm, upperDirection);

        Vector3 forearmDirection = (forward * 0.98f + inward * 0.18f).normalized;
        Vector3 boneAxis = forearm.up;
        if (boneAxis.sqrMagnitude > 0.000001f)
            forearm.rotation = Quaternion.FromToRotation(boneAxis, forearmDirection) * forearm.rotation;
    }

    private static void AlignBone(Transform bone, Transform nextJoint, Vector3 desiredDirection)
    {
        Vector3 currentDirection = nextJoint.position - bone.position;
        if (currentDirection.sqrMagnitude < 0.000001f || desiredDirection.sqrMagnitude < 0.000001f) return;
        bone.rotation = Quaternion.FromToRotation(currentDirection, desiredDirection) * bone.rotation;
    }

    private void Pose(Transform bone, Vector3 euler)
    {
        if (bone != null && restBones.TryGetValue(bone, out Quaternion rest))
            bone.localRotation = rest * Quaternion.Euler(euler);
    }
}


