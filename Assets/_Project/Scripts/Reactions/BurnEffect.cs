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

        // ---------------------------------------------------------------- llamas de pasto

        public const int GroundMaxParticles = 60;
        public const int SparkMaxParticles = 12;

        /// <summary>
        /// Llama baja de pasto para una celda de cellSize m: lenguas chicas que nacen amarillo-naranja y terminan rojo
        /// oscuro (color por debajo de 1 para que el bloom no las queme a blanco) y unas pocas chispas que suben. Las
        /// chispas son un ParticleSystem hijo: Play/Stop/Clear(true) de la llama las incluyen. Usa material y gravedad
        /// del look del fuego (no su gradiente, que es el de las llamas de objetos y queda intacto).
        /// </summary>
        public static ParticleSystem CreateGround(Transform parent, float cellSize, ElementLook look)
        {
            var go = new GameObject("GrassFlames");
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.LookRotation(Vector3.up);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.maxParticles = GroundMaxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.28f, 0.6f);
            main.gravityModifier = look.gravity * 0.35f;

            var emission = ps.emission;
            emission.rateOverTime = 38f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = cellSize * 0.42f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(GroundFlameGradient());

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.15f));

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 2f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = look.material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            CreateSparks(go.transform, cellSize, look);
            return ps;
        }

        private static void CreateSparks(Transform flames, float cellSize, ElementLook look)
        {
            var go = new GameObject("Sparks");
            go.transform.SetParent(flames, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.maxParticles = SparkMaxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.07f);
            main.gravityModifier = 0.15f;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.15f, 1f), new Color(1f, 0.8f, 0.35f, 1f));

            var emission = ps.emission;
            emission.rateOverTime = 5f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 20f;
            shape.radius = cellSize * 0.35f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(fade);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 1.2f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = look.material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>Nace amarillo-naranja tenue, pasa a naranja y muere rojo oscuro; alfa bajo (aditivo sin quemar a blanco).</summary>
        private static Gradient GroundFlameGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.85f, 0.5f, 0.16f), 0f),
                    new GradientColorKey(new Color(0.8f, 0.3f, 0.07f), 0.45f),
                    new GradientColorKey(new Color(0.45f, 0.08f, 0.03f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.55f, 0.12f),
                    new GradientAlphaKey(0.35f, 0.6f),
                    new GradientAlphaKey(0f, 1f),
                });
            return g;
        }
    }
}
