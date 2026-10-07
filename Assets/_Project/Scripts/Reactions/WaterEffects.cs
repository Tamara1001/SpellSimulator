using UnityEngine;
using SpellSimulator.Output;

namespace SpellSimulator.Reactions
{
    /// <summary>
    /// Particulas del agua (salpicadura, vapor, chorro succionado): un sistema por tipo y cuerpo de agua, que se
    /// dispara con Emit en el punto. Sin emision propia: no cuesta nada cuando no pasa nada.
    /// </summary>
    public static class WaterEffects
    {
        public const int MaxParticles = 400;

        public static ParticleSystem Create(string name, Transform parent, ElementLook look, float lifetime, float gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.maxParticles = MaxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = lifetime;
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            main.gravityModifier = gravity;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(look.color);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.7f, 1f, 1.3f));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = look.material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ps.Play();
            return ps;
        }

        /// <summary>Gotas hacia arriba en abanico desde un punto.</summary>
        public static void Burst(ParticleSystem ps, Vector3 at, int count, float speed)
        {
            if (ps == null) return;
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                Vector2 r = Random.insideUnitCircle * 0.6f;
                p.position = at + new Vector3(r.x, 0f, r.y) * 0.15f;
                p.velocity = new Vector3(r.x, 1f, r.y).normalized * speed * Random.Range(0.6f, 1.1f);
                ps.Emit(p, 1);
            }
        }

        /// <summary>Chorro de gotas desde un punto con una velocidad dada (succion).</summary>
        public static void Stream(ParticleSystem ps, Vector3 from, Vector3 velocity, float lifetime, int count)
        {
            if (ps == null) return;
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false, startLifetime = lifetime };
            for (int i = 0; i < count; i++)
            {
                p.position = from + Random.insideUnitSphere * 0.12f;
                p.velocity = velocity * Random.Range(0.85f, 1.1f) + Random.insideUnitSphere * 0.3f;
                ps.Emit(p, 1);
            }
        }
    }
}
