using UnityEngine;

/// <summary>
/// Efecto breve de partículas para impactos. No necesita prefab configurado:
/// se crea en tiempo de ejecución y se destruye automáticamente.
/// </summary>
public static class DamageImpactVFX
{
    public static void Play(Vector3 position, Vector3 normal)
    {
        GameObject effectObject = new GameObject("DamageImpactVFX");
        effectObject.transform.position = position;

        if (normal.sqrMagnitude > 0.001f)
            effectObject.transform.rotation = Quaternion.LookRotation(normal.normalized);

        ParticleSystem particles = effectObject.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.duration = 0.25f;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 32;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.48f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 4.0f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.88f, 0.25f, 1f),
            new Color(1f, 0.28f, 0.08f, 1f));

        var emission = particles.emission;
        emission.enabled = true;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.08f;

        var colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] {
                new GradientColorKey(new Color(1f, 0.82f, 0.18f), 0f),
                new GradientColorKey(new Color(1f, 0.22f, 0.04f), 1f)
            },
            new[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.85f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader != null)
            renderer.material = new Material(shader);

        particles.Play();
        Object.Destroy(effectObject, 1.5f);
    }
}
