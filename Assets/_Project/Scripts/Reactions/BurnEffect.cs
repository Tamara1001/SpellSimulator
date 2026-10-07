using UnityEngine;
using SpellSimulator.Output;

namespace SpellSimulator.Reactions
{
    /// <summary>Llamas sobre un objeto que arde (Shuriken construido en codigo, con el look del fuego).</summary>
    public static class BurnEffect
    {
        public const int MaxParticles = 160;

        public static ParticleSystem Create(Transform target, Bounds bounds, ElementLook look)
        {
            var go = new GameObject("Flames");
            go.transform.SetParent(target, false);
            // Las llamas salen de la cara de arriba y siempre suben, aunque el objeto este girado.
            go.transform.SetPositionAndRotation(bounds.center + Vector3.up * bounds.extents.y * 0.6f, Quaternion.LookRotation(Vector3.up));
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            float size = Mathf.Max(bounds.extents.x, bounds.extents.z, 0.05f);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.maxParticles = MaxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 1.1f, size * 2f);
            main.gravityModifier = look.gravity * 0.5f;

            var emission = ps.emission;
            emission.rateOverTime = Mathf.Clamp(400f * size, 40f, 140f);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = size * 0.8f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(look.color);

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.3f;
            noise.frequency = 1.5f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = look.material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }
    }
}
