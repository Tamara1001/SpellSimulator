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
            SpellPlan originalPlan = plan; // Conservar el plan original con todos los signos modificadores
            bodySeconds = Mathf.Clamp(spell.duration - 1.5f, MinBodySeconds, MaxBodySeconds) * (plan.Leak ? 1.5f : 1f);
            startTime = Time.time;
            transform.SetPositionAndRotation(frame.Origin, frame.Facing(frame.Normal));

            float s = frame.Scale;
            scale = s;
            float speed = (2f + 8f * spell.force) * look.speed;
            float density = look.density * Mathf.Clamp(0.5f + spell.quality, 0.5f, 1.5f);

            ElementVariant variant = SpellVisualModifierMath.ResolveVariant(spell.element, look.crystalline);
            float effGravity = SpellVisualModifierMath.CalculateEffectiveGravity(look.gravity, spell.gravity);

            charge = Track("Charge", look, frame.Origin, frame.Facing(frame.Normal), s, local: false, effGravity);
            Shape(charge, ParticleSystemShapeType.Circle, s * 1.4f, 0f);
            Speed(charge, -speed * 0.25f, s * 0.35f);
            Emit(charge, 40f * density);

            // Tierra de roca: el cuerpo solido (roca, muro, puente, estacas) es el espectaculo; sus esquirlas de particulas
            // salian en otro color y movimiento (prueba manual), asi que solo queda la carga y un golpe de polvo.
            bool solidEarth = spell.element == ElementType.Earth && !look.crystalline;

            if (plan.Has(PlanPrimitive.Burst))
            {
                var burst = Track("Burst", look, frame.Origin, frame.Facing(frame.Normal), s, local: false, effGravity);
                float burstRadius = s * 0.4f * (1f + 0.5f * Mathf.Max(0f, spell.spread));
                Shape(burst, ParticleSystemShapeType.Hemisphere, burstRadius, 0f);
                Speed(burst, speed * 0.6f, look.lifetime);
                AddBurst(burst, Mathf.RoundToInt(220f * density * (1f + spell.strength) * (solidEarth ? 0.25f : 1f)));
            }

            if (solidEarth) plan = SpellPlan.Inert(); // sin chorro, abanico, vortice, orbe ni succion de particulas base

            if (plan.Has(PlanPrimitive.Jet))
            {
                Vector3 dir = frame.Direction(plan.Aim);
                float length = s * (4f + 10f * spell.range);
                float jetSpeed = speed * Mathf.Clamp(1f + plan.Aim.magnitude * 0.25f, 1f, 2f);
                var jet = Track("Jet", look, frame.Origin, frame.Facing(dir), s, local: false, effGravity);
                float cone = SpellVisualModifierMath.CalculateConeAngle(18f, spell.spread, spell.focus, plan.Focus);
                float coneRadius = SpellVisualModifierMath.CalculateConeRadius(s * 0.35f, spell.spread, plan.Focus);
                Shape(jet, ParticleSystemShapeType.Cone, coneRadius, cone);
                Speed(jet, jetSpeed, length / jetSpeed);
                AddBody(jet, 420f * density, impacts: true);
            }

            if (plan.Has(PlanPrimitive.Fan))
            {
                var fan = Track("Fan", look, frame.Origin + frame.Normal * s * 0.1f, frame.Facing(frame.Normal), s, local: false, effGravity);
                float fanRadius = s * 0.5f * (1f + 0.6f * Mathf.Max(0f, spell.spread));
                Shape(fan, ParticleSystemShapeType.Circle, fanRadius, 0f);
                Speed(fan, speed * (0.6f + 0.4f * Mathf.Min(plan.Dispersion, 2f)), (s * (3f + 6f * spell.range)) / speed);
                // Abanico = lamina radial sobre el plano del papel: particulas finas y casi sin flotacion.
                Thin(fan, FanSizeFactor, FanGravityFactor * effGravity);
                AddBody(fan, 360f * density, impacts: true);
            }

            if (plan.Has(PlanPrimitive.Vortex))
            {
                var vortex = Track("Vortex", look, frame.Origin, frame.Facing(frame.Normal), s, local: true, effGravity);
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
                var orb = Track("Hold", look, holdPoint, frame.Facing(frame.Normal), s, local: true, 0f);
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
                var intake = Track("Intake", look, frame.Origin, frame.Facing(frame.Normal), s, local: true, effGravity);
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

            // Pistas visuales distintivas por elemento para los 10 signos de la Tanda 1
            ApplySignTracks(originalPlan, variant, s, speed, density, effGravity);

            charge.Play();
        }

        private void Update()
        {
            float t = SinceStrike;
            if (!struck && t >= 0f)
            {
                struck = true;
                charge.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                for (int i = 0; i < bursts.Count; i++)
                {
                    var item = bursts[i];
                    item.system.Play();
                    item.system.Emit(item.count);
                }
                for (int i = 0; i < bodyTracks.Count; i++)
                {
                    var item = bodyTracks[i];
                    var emission = item.system.emission;
                    emission.rateOverTime = item.rate;
                    item.system.Play();
                }
            }

            if (struck && !released && t >= StrikeSeconds + bodySeconds)
            {
                released = true;
                for (int i = 0; i < bodyTracks.Count; i++)
                {
                    bodyTracks[i].system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
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

        private ParticleSystem Track(string trackName, ElementLook look, Vector3 position, Quaternion rotation, float scale,
            bool local = false, float? gravityOverride = null)
        {
            return ElementVfx.Create(trackName, transform, look, position, rotation, scale, local, gravityOverride);
        }

        /// <summary>Construye pistas de partículas específicas y visualmente diferenciadas para cada signo modificador.</summary>
        private void ApplySignTracks(SpellPlan plan, ElementVariant variant, float s, float speed, float density, float effGravity)
        {
            if (plan.Crush > 0f)
            {
                var p = SpellVisualModifierMath.GetCrushParams(variant, plan.Crush, s);
                var crush = Track("Sign_Crush", look, Frame.Origin, Frame.Facing(Frame.Normal), s, local: false, effGravity);
                Shape(crush, p.shapeType, p.shapeRadiusFactor * s, p.shapeAngle);
                Speed(crush, speed * p.speedFactor, look.lifetime * p.lifetimeFactor);
                ApplyColor(crush, p.startColor, p.endColor);
                ApplySize(crush, look.size * s * p.sizeFactor);
                AddBurst(crush, Mathf.RoundToInt(p.burstCount * density));
            }

            if (plan.Orb > 0f)
            {
                var p = SpellVisualModifierMath.GetOrbParams(variant, plan.Orb, s);
                Vector3 orbPoint = plan.Hold != null ? Frame.Point(plan.Hold.At) + Frame.Normal * s * 0.3f : Frame.Origin + Frame.Normal * s * 0.5f;
                var orb = Track("Sign_Orb", look, orbPoint, Frame.Facing(Frame.Normal), s, local: true, 0f);
                Shape(orb, p.shapeType, p.shapeRadiusFactor * s, p.shapeAngle);
                Speed(orb, speed * p.speedFactor, look.lifetime * p.lifetimeFactor);
                ApplyColor(orb, p.startColor, p.endColor);
                ApplySize(orb, look.size * s * p.sizeFactor);
                var vel = orb.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.Local;
                vel.orbitalZ = new ParticleSystem.MinMaxCurve(p.orbitalSpeed);
                vel.radial = new ParticleSystem.MinMaxCurve(p.radialSpeed * s);
                AddBody(orb, p.rateFactor * density, impacts: false);
            }

            if (plan.Billow > 0f)
            {
                var p = SpellVisualModifierMath.GetBillowingParams(variant, plan.Billow, s);
                var billow = Track("Sign_Billowing", look, Frame.Origin + Frame.Normal * s * 0.2f, Frame.Facing(Frame.Normal), s, local: false, effGravity * 0.2f);
                Shape(billow, p.shapeType, p.shapeRadiusFactor * s, p.shapeAngle);
                Speed(billow, speed * p.speedFactor, look.lifetime * p.lifetimeFactor);
                ApplyColor(billow, p.startColor, p.endColor);
                ApplySize(billow, look.size * s * p.sizeFactor);
                var noise = billow.noise;
                noise.enabled = true;
                noise.strength = (look.turbulence + 0.5f) * s;
                noise.frequency = 0.75f;
                AddBody(billow, p.rateFactor * density, impacts: false);
            }

            if (plan.Cool > 0f)
            {
                var p = SpellVisualModifierMath.GetCoolParams(variant, plan.Cool, s);
                var cool = Track("Sign_Cool", look, Frame.Origin + Frame.Normal * s * 0.15f, Frame.Facing(Frame.Normal), s, local: false, effGravity + 0.15f);
                Shape(cool, p.shapeType, p.shapeRadiusFactor * s, p.shapeAngle);
                Speed(cool, speed * p.speedFactor, look.lifetime * p.lifetimeFactor);
                ApplyColor(cool, p.startColor, p.endColor);
                ApplySize(cool, look.size * s * p.sizeFactor);
                AddBody(cool, p.rateFactor * density, impacts: false);
                if (glow != null) glow.color = Color.Lerp(glow.color, p.startColor, 0.45f * Mathf.Clamp01(plan.Cool));
            }

            if (plan.Empower > 0f)
            {
                var p = SpellVisualModifierMath.GetEmpowerParams(variant, plan.Empower, s);
                var empower = Track("Sign_Empower", look, Frame.Origin + Frame.Normal * s * 0.1f, Frame.Facing(Frame.Normal), s, local: false, effGravity);
                Shape(empower, p.shapeType, p.shapeRadiusFactor * s, p.shapeAngle);
                Speed(empower, speed * p.speedFactor, look.lifetime * p.lifetimeFactor);
                ApplyColor(empower, p.startColor, p.endColor);
                ApplySize(empower, look.size * s * p.sizeFactor);
                AddBody(empower, p.rateFactor * density, impacts: true);
                AddBurst(empower, Mathf.RoundToInt(p.burstCount * density));
                lightIntensity *= (1f + 0.35f * plan.Empower * p.lightIntensityBoost);
            }

            if (plan.Entwine > 0f)
            {
                var p = SpellVisualModifierMath.GetEntwineParams(variant, plan.Entwine, s);
                var entwine = Track("Sign_Entwine", look, Frame.Origin, Frame.Facing(Frame.Normal), s, local: true, effGravity);
                Shape(entwine, p.shapeType, p.shapeRadiusFactor * s, p.shapeAngle);
                Speed(entwine, speed * p.speedFactor, look.lifetime * p.lifetimeFactor);
                ApplyColor(entwine, p.startColor, p.endColor);
                ApplySize(entwine, look.size * s * p.sizeFactor);
                var vel = entwine.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.Local;
                vel.orbitalZ = new ParticleSystem.MinMaxCurve(p.orbitalSpeed);
                vel.radial = new ParticleSystem.MinMaxCurve(p.radialSpeed * s);
                vel.z = new ParticleSystem.MinMaxCurve(speed * 0.4f);
                AddBody(entwine, p.rateFactor * density, impacts: false);
            }

            if (plan.Target > 0f)
            {
                var p = SpellVisualModifierMath.GetFocusAimParams(variant, plan.Target, s);
                Vector3 aimDir = Frame.Direction(plan.Aim);
                var focusAim = Track("Sign_FocusAim", look, Frame.Origin, Frame.Facing(aimDir), s, local: false, 0f);
                Shape(focusAim, p.shapeType, p.shapeRadiusFactor * s, p.shapeAngle);
                float beamSpeed = speed * p.speedFactor;
                Speed(focusAim, beamSpeed, (s * (5f + 12f * Spell.range)) / beamSpeed);
                ApplyColor(focusAim, p.startColor, p.endColor);
                ApplySize(focusAim, look.size * s * p.sizeFactor);
                AddBody(focusAim, p.rateFactor * density, impacts: false);
            }

            if (plan.Gather > 0f || plan.Collection > 0f)
            {
                float gatherPower = Mathf.Max(plan.Gather, plan.Collection);
                var p = SpellVisualModifierMath.GetGatherParams(variant, gatherPower, s);
                var gather = Track("Sign_Gather", look, Frame.Origin, Frame.Facing(Frame.Normal), s, local: true, 0f);
                float reach = s * p.shapeRadiusFactor;
                Shape(gather, p.shapeType, reach, p.shapeAngle);
                var shape = gather.shape;
                shape.radiusThickness = 0.15f;
                float inSpeed = speed * p.speedFactor;
                Speed(gather, inSpeed, reach / Mathf.Max(0.01f, Mathf.Abs(inSpeed)));
                ApplyColor(gather, p.startColor, p.endColor);
                ApplySize(gather, look.size * s * p.sizeFactor);
                var vel = gather.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.Local;
                vel.orbitalZ = new ParticleSystem.MinMaxCurve(p.orbitalSpeed);
                AddBody(gather, p.rateFactor * density, impacts: false);
            }

            if (plan.Repetition > 0f)
            {
                var p = SpellVisualModifierMath.GetRepetitionParams(variant, plan.Repetition, s);
                var rep = Track("Sign_Repetition", look, Frame.Origin + Frame.Normal * s * 0.1f, Frame.Facing(Frame.Normal), s, local: true, 0f);
                Shape(rep, p.shapeType, p.shapeRadiusFactor * s, p.shapeAngle);
                Speed(rep, speed * p.speedFactor, look.lifetime * p.lifetimeFactor);
                ApplyColor(rep, p.startColor, p.endColor);
                ApplySize(rep, look.size * s * p.sizeFactor);
                var vel = rep.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.Local;
                vel.orbitalZ = new ParticleSystem.MinMaxCurve(p.orbitalSpeed);
                AddBody(rep, p.rateFactor * density, impacts: false);
                AddBurst(rep, Mathf.RoundToInt(p.burstCount * density));
            }

            if (plan.Weave > 0f)
            {
                Vector3 aimDir = Frame.Direction(plan.Aim);
                var pA = SpellVisualModifierMath.GetWeaveParams(variant, plan.Weave, s, isSecondStrand: false);
                var pB = SpellVisualModifierMath.GetWeaveParams(variant, plan.Weave, s, isSecondStrand: true);

                Vector3 posA = Frame.Origin + Frame.Right * (s * 0.15f * pA.shapeRadiusFactor);
                var weaveA = Track("Sign_Weave_A", look, posA, Frame.Facing(aimDir), s, local: true, effGravity);
                Shape(weaveA, pA.shapeType, s * 0.15f * pA.shapeRadiusFactor, 0f);
                Speed(weaveA, speed * pA.speedFactor, look.lifetime * pA.lifetimeFactor);
                ApplyColor(weaveA, pA.startColor, pA.endColor);
                ApplySize(weaveA, look.size * s * pA.sizeFactor);
                var velA = weaveA.velocityOverLifetime;
                velA.enabled = true;
                velA.space = ParticleSystemSimulationSpace.Local;
                velA.orbitalZ = new ParticleSystem.MinMaxCurve(pA.orbitalSpeed);
                velA.z = new ParticleSystem.MinMaxCurve(speed * 0.75f);
                AddBody(weaveA, pA.rateFactor * density, impacts: false);

                Vector3 posB = Frame.Origin - Frame.Right * (s * 0.15f * pB.shapeRadiusFactor);
                var weaveB = Track("Sign_Weave_B", look, posB, Frame.Facing(aimDir), s, local: true, effGravity);
                Shape(weaveB, pB.shapeType, s * 0.15f * pB.shapeRadiusFactor, 0f);
                Speed(weaveB, speed * pB.speedFactor, look.lifetime * pB.lifetimeFactor);
                ApplyColor(weaveB, pB.startColor, pB.endColor);
                ApplySize(weaveB, look.size * s * pB.sizeFactor);
                var velB = weaveB.velocityOverLifetime;
                velB.enabled = true;
                velB.space = ParticleSystemSimulationSpace.Local;
                velB.orbitalZ = new ParticleSystem.MinMaxCurve(pB.orbitalSpeed);
                velB.z = new ParticleSystem.MinMaxCurve(speed * 0.75f);
                AddBody(weaveB, pB.rateFactor * density, impacts: false);
            }
        }

        private static void ApplyColor(ParticleSystem ps, Color start, Color end)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(SpellVisualModifierMath.CreateGradient(start, end));
        }

        private static void ApplySize(ParticleSystem ps, float size)
        {
            var main = ps.main;
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.8f, size * 1.3f);
        }

        /// <summary>Pista del cuerpo: se prende en el golpe con su tasa; suma las capas del look y, si choca, sus impactos.</summary>
        private void AddBody(ParticleSystem track, float rate, bool impacts)
        {
            bodyTracks.Add((track, rate));
            if (look.layers != null)
                for (int i = 0; i < look.layers.Length; i++)
                {
                    var layer = look.layers[i];
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
                for (int i = 0; i < look.layers.Length; i++)
                {
                    var layer = look.layers[i];
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
