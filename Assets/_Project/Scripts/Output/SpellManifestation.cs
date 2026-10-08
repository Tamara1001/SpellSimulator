using System.Collections.Generic;
using UnityEngine;
using SpellSimulator.Compiler;

namespace SpellSimulator.Output
{
    /// <summary>
    /// Ejecuta un hechizo en 3D: una "pista" de particulas por primitiva del plan (rafaga, chorro, abanico, vortice,
    /// orbe, succion) con los tiempos de animation-spec.md R-01 (carga, golpe, cuerpo, liberacion, resplandor).
    /// El look sale de ElementLook; la forma y la fuerza, del SpellIR y su SpellPlan.
    /// </summary>
    public class SpellManifestation : MonoBehaviour
    {
        // R-01: solo el cuerpo se estira.
        public const float ChargeSeconds = 0.45f;
        public const float StrikeSeconds = 0.32f;
        public const float ReleaseSeconds = 0.76f;
        public const float AfterglowSeconds = 0.42f;
        public const float MinBodySeconds = 0.6f;
        public const float MaxBodySeconds = 6f;

        private readonly List<(ParticleSystem system, float rate)> bodyTracks = new List<(ParticleSystem, float)>();
        private readonly List<(ParticleSystem system, int count)> bursts = new List<(ParticleSystem, int)>();
        private ParticleSystem charge;
        private ElementLook look;
        private float scale;
        private Light glow;
        private float lightIntensity;
        private float startTime, bodySeconds;
        private bool struck, released;

        public SpellIR Spell { get; private set; }
        public SealFrame Frame { get; private set; }
        public float BodySeconds => bodySeconds;
        public float TotalSeconds => ChargeSeconds + StrikeSeconds + bodySeconds + ReleaseSeconds + AfterglowSeconds;

        /// <summary>Tiempo transcurrido desde el golpe (negativo durante la carga).</summary>
        public float SinceStrike => Time.time - startTime - ChargeSeconds;
        public bool InBody => SinceStrike >= 0f && SinceStrike < StrikeSeconds + bodySeconds;

        public void Init(SpellIR spell, SealFrame frame, ElementLook look)
        {
            Spell = spell;
            Frame = frame;
            this.look = look;
            SpellPlan plan = spell.plan ?? SpellPlan.Inert();
            bodySeconds = Mathf.Clamp(spell.duration - 1.5f, MinBodySeconds, MaxBodySeconds) * (plan.Leak ? 1.5f : 1f);
            startTime = Time.time;
            transform.SetPositionAndRotation(frame.Origin, frame.Facing(frame.Normal));

            float s = frame.Scale;
            scale = s;
            float speed = (2f + 8f * spell.force) * look.speed;
            float density = look.density * Mathf.Clamp(0.5f + spell.quality, 0.5f, 1.5f);

            charge = Track("Charge", look, frame.Origin, frame.Facing(frame.Normal), s);
            Shape(charge, ParticleSystemShapeType.Circle, s * 1.4f, 0f);
            Speed(charge, -speed * 0.25f, s * 0.35f);
            Emit(charge, 40f * density);

            if (plan.Has(PlanPrimitive.Burst))
            {
                var burst = Track("Burst", look, frame.Origin, frame.Facing(frame.Normal), s);
                Shape(burst, ParticleSystemShapeType.Hemisphere, s * 0.4f, 0f);
                Speed(burst, speed * 0.6f, look.lifetime);
                AddBurst(burst, Mathf.RoundToInt(220f * density * (1f + spell.strength)));
            }

            if (plan.Has(PlanPrimitive.Jet))
            {
                Vector3 dir = frame.Direction(plan.Aim);
                float length = s * (4f + 10f * spell.range);
                float jetSpeed = speed * Mathf.Clamp(1f + plan.Aim.magnitude * 0.25f, 1f, 2f);
                var jet = Track("Jet", look, frame.Origin, frame.Facing(dir), s);
                float cone = Mathf.Lerp(18f, 3f, Mathf.Clamp01((plan.Focus - 1f) * 0.8f + spell.focus * 0.5f));
                Shape(jet, ParticleSystemShapeType.Cone, s * 0.35f / plan.Focus, cone);
                Speed(jet, jetSpeed, length / jetSpeed);
                AddBody(jet, 420f * density, impacts: true);
            }

            if (plan.Has(PlanPrimitive.Fan))
            {
                var fan = Track("Fan", look, frame.Origin + frame.Normal * s * 0.1f, frame.Facing(frame.Normal), s);
                Shape(fan, ParticleSystemShapeType.Circle, s * 0.5f, 0f);
                Speed(fan, speed * (0.6f + 0.4f * Mathf.Min(plan.Dispersion, 2f)), (s * (3f + 6f * spell.range)) / speed);
                // Abanico = lamina radial sobre el plano del papel: particulas finas y casi sin flotacion, para que se
                // lea como una erupcion plana y no como una nube que tapa la camara (prueba manual 2026-10-07).
                Thin(fan, FanSizeFactor, FanGravityFactor * look.gravity);
                AddBody(fan, 360f * density, impacts: true);
            }

            if (plan.Has(PlanPrimitive.Vortex))
            {
                var vortex = Track("Vortex", look, frame.Origin, frame.Facing(frame.Normal), s, local: true);
                Shape(vortex, ParticleSystemShapeType.Circle, s * 0.9f, 0f);
                Speed(vortex, 0.3f, look.lifetime * 1.6f);
                var vel = vortex.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.Local;
                vel.orbitalZ = new ParticleSystem.MinMaxCurve(Mathf.Clamp(plan.Circulation, -4f, 4f) * 2.2f);
                vel.radial = new ParticleSystem.MinMaxCurve(-0.4f * s);
                vel.z = new ParticleSystem.MinMaxCurve(speed * 0.35f);
                AddBody(vortex, 400f * density, impacts: false);
            }

            if (plan.Has(PlanPrimitive.Hold))
            {
                Vector3 holdPoint = frame.Point(plan.Hold.At) + frame.Normal * s * 0.3f;
                var orb = Track("Hold", look, holdPoint, frame.Facing(frame.Normal), s, local: true);
                Shape(orb, ParticleSystemShapeType.Sphere, s * 0.3f, 0f);
                Speed(orb, 0.15f, look.lifetime);
                var main = orb.main;
                main.gravityModifier = 0f;
                var vel = orb.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.Local;
                vel.orbitalZ = new ParticleSystem.MinMaxCurve(2f + Mathf.Clamp(plan.Hold.Spin, -3f, 3f) * 2f);
                AddBody(orb, 300f * density, impacts: false);
            }

            if (plan.Has(PlanPrimitive.Intake))
            {
                var intake = Track("Intake", look, frame.Origin, frame.Facing(frame.Normal), s, local: true);
                float reach = s * 4f;
                Shape(intake, ParticleSystemShapeType.Sphere, reach, 0f);
                var shape = intake.shape;
                shape.radiusThickness = 0f;
                float inSpeed = speed * 0.6f * Mathf.Clamp(0.5f + plan.Intake.Draw, 0.5f, 2f);
                Speed(intake, -inSpeed, reach / inSpeed);
                var vel = intake.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.Local;
                vel.orbitalZ = new ParticleSystem.MinMaxCurve(Mathf.Clamp(plan.Intake.Swirl, -3f, 3f) * 1.5f);
                AddBody(intake, 260f * density, impacts: false);
            }

            if (look.emitsLight)
            {
                var lightGo = new GameObject("Glow");
                lightGo.transform.SetParent(transform, false);
                lightGo.transform.position = frame.Origin + frame.Normal * s * 0.5f;
                glow = lightGo.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = look.lightColor;
                glow.range = s * 6f;
                glow.shadows = LightShadows.None;
                lightIntensity = look.lightIntensity * (0.6f + spell.strength);
                glow.intensity = 0f;
            }

            charge.Play();
        }

        private void Update()
        {
            float t = SinceStrike;
            if (!struck && t >= 0f)
            {
                struck = true;
                charge.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                foreach (var (system, count) in bursts)
                {
                    system.Play();
                    system.Emit(count);
                }
                foreach (var (system, rate) in bodyTracks)
                {
                    var emission = system.emission;
                    emission.rateOverTime = rate;
                    system.Play();
                }
            }

            if (struck && !released && t >= StrikeSeconds + bodySeconds)
            {
                released = true;
                foreach (var (system, _) in bodyTracks) system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            if (glow != null) glow.intensity = lightIntensity * Envelope(t);
            if (t > StrikeSeconds + bodySeconds + ReleaseSeconds + AfterglowSeconds + 2f) Destroy(gameObject);
        }

        /// <summary>Adelanta el hechizo (previews, capturas, laboratorio de hechizos).</summary>
        public void FastForward(float seconds)
        {
            startTime -= seconds;
            Update();
            foreach (var ps in GetComponentsInChildren<ParticleSystem>())
                if (ps.isPlaying) ps.Simulate(Mathf.Min(seconds, 2f), false, false, true);
        }

        /// <summary>Intensidad 0..1 a lo largo de los tiempos del hechizo (para luz y fuerzas).</summary>
        public float Envelope(float sinceStrike)
        {
            if (sinceStrike < 0f) return Mathf.Clamp01(1f + sinceStrike / ChargeSeconds) * 0.3f;
            if (sinceStrike < StrikeSeconds) return 1f;
            float afterBody = sinceStrike - StrikeSeconds - bodySeconds;
            if (afterBody < 0f) return 0.85f;
            return Mathf.Clamp01(1f - afterBody / (ReleaseSeconds + AfterglowSeconds)) * 0.85f;
        }

        // ---------------------------------------------------------------- construccion de pistas

        private ParticleSystem Track(string trackName, ElementLook look, Vector3 position, Quaternion rotation, float scale, bool local = false)
        {
            return ElementVfx.Create(trackName, transform, look, position, rotation, scale, local);
        }

        /// <summary>Pista del cuerpo: se prende en el golpe con su tasa; suma las capas del look y, si choca, sus impactos.</summary>
        private void AddBody(ParticleSystem track, float rate, bool impacts)
        {
            bodyTracks.Add((track, rate));
            if (look.layers != null)
                foreach (var layer in look.layers)
                {
                    if (layer == null || layer.material == null) continue;
                    var ps = ElementVfx.AddLayer(track, layer, scale);
                    bodyTracks.Add((ps, rate * layer.density * layer.layerRatio));
                }
            if (impacts) ElementVfx.AddImpacts(track, look, scale);
        }

        /// <summary>Rafaga del golpe: se emite una vez; las capas emiten su fraccion.</summary>
        private void AddBurst(ParticleSystem track, int count)
        {
            bursts.Add((track, count));
            if (look.layers != null)
                foreach (var layer in look.layers)
                {
                    if (layer == null || layer.material == null) continue;
                    var ps = ElementVfx.AddLayer(track, layer, scale);
                    bursts.Add((ps, Mathf.RoundToInt(count * layer.density * layer.layerRatio)));
                }
            ElementVfx.AddImpacts(track, look, scale);
        }

        private const float FanSizeFactor = 0.45f;
        private const float FanGravityFactor = 0.25f;

        private static void Thin(ParticleSystem ps, float sizeFactor, float gravity)
        {
            var main = ps.main;
            var size = main.startSize;
            main.startSize = new ParticleSystem.MinMaxCurve(size.constantMin * sizeFactor, size.constantMax * sizeFactor);
            main.gravityModifier = gravity;
            var overLife = ps.sizeOverLifetime;
            overLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.3f));
        }

        private static void Shape(ParticleSystem ps, ParticleSystemShapeType type, float radius, float angle)
        {
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = type;
            shape.radius = Mathf.Max(0.01f, radius);
            shape.angle = angle;
        }

        private static void Speed(ParticleSystem ps, float speed, float lifetime)
        {
            var main = ps.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.8f, speed * 1.2f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.8f, lifetime * 1.2f);
        }

        private static void Emit(ParticleSystem ps, float rate)
        {
            var emission = ps.emission;
            emission.rateOverTime = rate;
        }
    }
}
