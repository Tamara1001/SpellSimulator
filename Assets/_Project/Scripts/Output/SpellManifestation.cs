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
            // empower: efecto 1.6x mas grande
            if (originalPlan.Empower > 0f) s *= 1.6f;
            scale = s;

            float speed = (2f + 8f * spell.force) * look.speed;
            float density = look.density * Mathf.Clamp(0.5f + spell.quality, 0.5f, 1.5f);

            ElementVariant variant = SpellVisualModifierMath.ResolveVariant(spell.element, look.crystalline);
            float effGravity = SpellVisualModifierMath.CalculateEffectiveGravity(look.gravity, spell.gravity);
            if (spell.gravity < 0.4f)
            {
                // gravity_low: particulas que flotan hacia arriba
                effGravity = -0.32f * (1f - spell.gravity * 2f);
            }
            else if (spell.element == ElementType.Earth || look.crystalline)
            {
                // Limitar caida desmedida bajo el terreno en particulas
                effGravity = Mathf.Min(effGravity, 0.7f);
            }

            // Ancla ligeramente elevada en superficies para evitar que el efecto se hunda
            Vector3 spawnOrigin = frame.Origin;
            if (frame.Normal.y > 0.3f) spawnOrigin += frame.Normal * (s * 0.15f);

            // Regla: cuando hay un signo dominante la emision base (Jet/Burst) baja a 20-30%
            bool hasDominantSign = originalPlan.Crush > 0f || originalPlan.Orb > 0f || originalPlan.Billow > 0f || originalPlan.Cool > 0f
                || originalPlan.Empower > 0f || originalPlan.Entwine > 0f || originalPlan.Target > 0f || originalPlan.Gather > 0f
                || originalPlan.Collection > 0f || originalPlan.Repetition > 0f || originalPlan.Weave > 0f;

            float baseDampen = 1.0f;
            if (originalPlan.Orb > 0f) baseDampen = 0.20f; // orb: columna base reducida al 20%
            else if (originalPlan.Billow > 0f) baseDampen = 0.22f; // billowing: base reducida
            else if (originalPlan.Target > 0f) baseDampen = 0.20f; // focus: base reducida
            else if (hasDominantSign) baseDampen = 0.25f; // regla general 20-30%

            charge = Track("Charge", look, spawnOrigin, frame.Facing(frame.Normal), s, local: false, effGravity);
            Shape(charge, ParticleSystemShapeType.Circle, s * 1.4f, 0f);
            Speed(charge, -speed * 0.25f, s * 0.35f);
            Emit(charge, 40f * density * (hasDominantSign ? 0.4f : 1f));

            bool solidEarth = spell.element == ElementType.Earth && !look.crystalline;

            if (plan.Has(PlanPrimitive.Burst))
            {
                var burst = Track("Burst", look, spawnOrigin, frame.Facing(frame.Normal), s, local: false, effGravity);
                float burstSpreadMul = spell.spread > 0.6f ? 2.0f : 1.0f;
                float burstRadius = s * 0.4f * (1f + 0.5f * Mathf.Max(0f, spell.spread)) * burstSpreadMul;
                Shape(burst, ParticleSystemShapeType.Hemisphere, burstRadius, 0f);
                Speed(burst, speed * 0.6f, look.lifetime);
                float burstDampen = hasDominantSign ? 0.3f : 1.0f;
                AddBurst(burst, Mathf.RoundToInt(220f * density * (1f + spell.strength) * (solidEarth ? 0.25f : 1f) * burstDampen));
            }

            if (solidEarth) plan = SpellPlan.Inert(); // sin chorro, abanico, vortice, orbe ni succion de particulas base

            if (plan.Has(PlanPrimitive.Jet))
            {
                Vector3 dir = frame.Direction(plan.Aim);
                float length = s * (4f + 10f * spell.range);
                float jetSpeed = speed * Mathf.Clamp(1f + plan.Aim.magnitude * 0.25f, 1f, 2f);
                var jet = Track("Jet", look, spawnOrigin, frame.Facing(dir), s, local: false, effGravity);
                float cone = SpellVisualModifierMath.CalculateConeAngle(18f, spell.spread, spell.focus, plan.Focus);
                if (spell.spread > 0.6f) cone = Mathf.Max(cone, 45f); // spread_high ancho de cono notable
                float coneRadius = SpellVisualModifierMath.CalculateConeRadius(s * 0.35f, spell.spread, plan.Focus);
                if (spell.spread > 0.6f) coneRadius *= 1.8f;
                Shape(jet, ParticleSystemShapeType.Cone, coneRadius, cone);
                Speed(jet, jetSpeed, length / jetSpeed);
                AddBody(jet, 420f * density * baseDampen, impacts: true);
            }

            if (plan.Has(PlanPrimitive.Fan))
            {
                var fan = Track("Fan", look, spawnOrigin + frame.Normal * s * 0.1f, frame.Facing(frame.Normal), s, local: false, effGravity);
                float fanRadius = s * 0.5f * (1f + 0.6f * Mathf.Max(0f, spell.spread));
                Shape(fan, ParticleSystemShapeType.Circle, fanRadius, 0f);
                Speed(fan, speed * (0.6f + 0.4f * Mathf.Min(plan.Dispersion, 2f)), (s * (3f + 6f * spell.range)) / speed);
                Thin(fan, FanSizeFactor, FanGravityFactor * effGravity);
                AddBody(fan, 360f * density * baseDampen, impacts: true);
            }

            if (plan.Has(PlanPrimitive.Vortex))
            {
                var vortex = Track("Vortex", look, spawnOrigin, frame.Facing(frame.Normal), s, local: true, 0f);
                Shape(vortex, ParticleSystemShapeType.Circle, s * 0.9f, 0f);
                Speed(vortex, 0.3f, look.lifetime * 1.6f);
                var vel = vortex.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.Local;
                vel.orbitalZ = new ParticleSystem.MinMaxCurve(Mathf.Clamp(plan.Circulation, -4f, 4f) * 2.2f);
                vel.radial = new ParticleSystem.MinMaxCurve(-0.4f * s);
                vel.z = new ParticleSystem.MinMaxCurve(speed * 0.35f);
                AddBody(vortex, 400f * density * baseDampen, impacts: false);
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
                AddBody(orb, 300f * density * baseDampen, impacts: false);
            }

            if (plan.Has(PlanPrimitive.Intake))
            {
                var intake = Track("Intake", look, spawnOrigin, frame.Facing(frame.Normal), s, local: true, 0f);
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
                AddBody(intake, 260f * density * baseDampen, impacts: false);
            }

            if (look.emitsLight)
            {
                var lightGo = new GameObject("Glow");
                lightGo.transform.SetParent(transform, false);
                lightGo.transform.position = spawnOrigin + frame.Normal * s * 0.5f;
                glow = lightGo.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = look.lightColor;
                glow.range = s * 6f;
                glow.shadows = LightShadows.None;
                // empower: brillante
                lightIntensity = look.lightIntensity * (0.6f + spell.strength) * (originalPlan.Empower > 0f ? 2.6f : 1f);
                glow.intensity = 0f;
            }

            // Pistas visuales distintivas por elemento para los signos modificadores
            ApplySignTracks(originalPlan, variant, s, speed, density, effGravity);

            // cool: tinte azul/blanco forzado en TODAS las particulas del hechizo
            if (originalPlan.Cool > 0f)
            {
                ApplyCoolTintToAll();
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
            Vector3 anchor = Frame.Origin;
            if (Frame.Normal.y > 0.3f) anchor += Frame.Normal * (s * 0.15f);

            if (plan.Crush > 0f)
            {
                var p = SpellVisualModifierMath.GetCrushParams(variant, plan.Crush, s);
                // crush: fragmentos solidos grandes cayendo/estallando (usar mesh de ParticleSystemRenderer con cubos/rocas chicas) y destello de choque
                var crush = Track("Sign_Crush", look, anchor + Frame.Normal * (s * 0.1f), Frame.Facing(Frame.Normal), s, local: false, gravityOverride: 0.85f);
                Shape(crush, p.shapeType, p.shapeRadiusFactor * s, p.shapeAngle);
                Speed(crush, speed * p.speedFactor, look.lifetime * p.lifetimeFactor);
                ApplyColor(crush, p.startColor, p.endColor);
                ApplySize(crush, look.size * s * p.sizeFactor);

                var renderer = crush.GetComponent<ParticleSystemRenderer>();
                Mesh meshToUse = look.mesh != null ? look.mesh : ElementVfx.GetCubeMesh();
                renderer.renderMode = ParticleSystemRenderMode.Mesh;
                renderer.mesh = meshToUse;
                renderer.alignment = ParticleSystemRenderSpace.World;
                renderer.SetActiveVertexStreams(ElementVfx.MeshStreams);

                var main = crush.main;
                main.startRotation3D = true;
                var full = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startRotationX = full;
                main.startRotationY = full;
                main.startRotationZ = full;
                var rot = crush.rotationOverLifetime;
                rot.enabled = true;
                rot.separateAxes = true;
                rot.x = new ParticleSystem.MinMaxCurve(-4f, 4f);
                rot.y = new ParticleSystem.MinMaxCurve(-4f, 4f);
                rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f);

                AddBurst(crush, Mathf.RoundToInt(p.burstCount * density));

                // Destello de choque
                var flash = Track("Sign_Crush_Flash", look, anchor + Frame.Normal * (s * 0.2f), Frame.Facing(Frame.Normal), s, local: false, gravityOverride: 0f);
                Shape(flash, ParticleSystemShapeType.Sphere, s * 0.15f, 0f);
                Speed(flash, 0.4f, 0.2f);
                ApplyColor(flash, Color.white, p.startColor);
                ApplySize(flash, s * 2.5f);
                AddBurst(flash, 4);
                if (glow != null) lightIntensity *= 2.2f;
            }

            if (plan.Orb > 0f)
            {
                var p = SpellVisualModifierMath.GetOrbParams(variant, plan.Orb, s);
                Vector3 orbCenter = anchor + Frame.Normal * (s * 0.7f);

                // orb: esfera translucida con borde fresnel del color del elemento rodeando el efecto
                var orbBubble = Track("Sign_Orb_Membrane", look, orbCenter, Frame.Facing(Frame.Normal), s, local: false, gravityOverride: 0f);
                Shape(orbBubble, ParticleSystemShapeType.Sphere, 0.05f, 0f);
                Speed(orbBubble, 0.01f, 1.2f);
                Color fresnelRim = p.startColor;
                fresnelRim.a = 0.85f;
                Color centerColor = p.startColor;
                centerColor.a = 0.22f;
                ApplyColor(orbBubble, centerColor, fresnelRim);
                ApplySize(orbBubble, s * 2.2f * p.shapeRadiusFactor);
                AddBody(orbBubble, 14f, impacts: false);

                // Cascaron orbital de particulas de alta densidad
                var orbShell = Track("Sign_Orb_Shell", look, orbCenter, Frame.Facing(Frame.Normal), s, local: true, gravityOverride: 0f);
                Shape(orbShell, ParticleSystemShapeType.Sphere, s * 1.0f * p.shapeRadiusFactor, 0f);
                var shellShape = orbShell.shape;
                shellShape.radiusThickness = 0f;
                Speed(orbShell, speed * 0.12f, look.lifetime * p.lifetimeFactor);
                ApplyColor(orbShell, p.startColor, p.endColor);
                ApplySize(orbShell, look.size * s * p.sizeFactor * 0.75f);
                var vel = orbShell.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.Local;
                vel.orbitalZ = new ParticleSystem.MinMaxCurve(p.orbitalSpeed);
                vel.orbitalY = new ParticleSystem.MinMaxCurve(p.orbitalSpeed * 0.6f);
                vel.radial = new ParticleSystem.MinMaxCurve(p.radialSpeed * s);
                AddBody(orbShell, p.rateFactor * density * 1.5f, impacts: false);
            }

            if (plan.Billow > 0f)
            {
                var p = SpellVisualModifierMath.GetBillowingParams(variant, plan.Billow, s);
                // billowing: nube enorme lenta y esponjosa (particulas 3-4x mas grandes, velocidad baja) con la base reducida
                var billow = Track("Sign_Billowing", look, anchor + Frame.Normal * (s * 0.25f), Frame.Facing(Frame.Normal), s, local: false, gravityOverride: -0.04f);
                Shape(billow, ParticleSystemShapeType.Sphere, s * 1.1f * p.shapeRadiusFactor, 0f);
                Speed(billow, speed * 0.15f, look.lifetime * 2.2f);
                ApplyColor(billow, p.startColor, p.endColor);
                ApplySize(billow, look.size * s * p.sizeFactor);
                var noise = billow.noise;
                noise.enabled = true;
                noise.strength = (look.turbulence + 1.2f) * s;
                noise.frequency = 0.45f;
                AddBody(billow, p.rateFactor * density * 1.6f, impacts: false);
            }

            if (plan.Cool > 0f)
            {
                var p = SpellVisualModifierMath.GetCoolParams(variant, plan.Cool, s);
                // cool: vaho que cae
                var coolMist = Track("Sign_Cool_Mist", look, anchor + Frame.Normal * (s * 0.5f), Frame.Facing(-Vector3.up), s, local: false, gravityOverride: 0.55f);
                Shape(coolMist, ParticleSystemShapeType.Circle, s * 0.75f, 0f);
                Speed(coolMist, 0.25f, look.lifetime * 1.6f);
                ApplyColor(coolMist, new Color(0.85f, 0.95f, 1f, 0.75f), new Color(0.35f, 0.65f, 1f, 0f));
                ApplySize(coolMist, look.size * s * 2.4f);
                AddBody(coolMist, p.rateFactor * density * 1.5f, impacts: false);

                if (glow != null) glow.color = Color.Lerp(glow.color, new Color(0.55f, 0.85f, 1f, 1f), 0.85f);
            }

            if (plan.Empower > 0f)
            {
                var p = SpellVisualModifierMath.GetEmpowerParams(variant, plan.Empower, s);
                // empower: efecto 1.6x mas grande y brillante con destellos
                var empower = Track("Sign_Empower", look, anchor + Frame.Normal * (s * 0.15f), Frame.Facing(Frame.Normal), s, local: false, gravityOverride: 0f);
                Shape(empower, ParticleSystemShapeType.Sphere, s * 0.7f, 0f);
                Speed(empower, speed * 1.5f, 0.3f);
                ApplyColor(empower, Color.white, p.startColor);
                ApplySize(empower, look.size * s * 1.6f);

                // Destellos rapidos y brillantes
                var emission = empower.emission;
                emission.rateOverTime = 0f;
                int glintCount = Mathf.RoundToInt(p.burstCount * density * 0.8f);
                var burstObj = new ParticleSystem.Burst(0f, (short)glintCount);
                burstObj.cycleCount = 15;
                burstObj.repeatInterval = 0.2f;
                emission.SetBursts(new[] { burstObj });
                bodyTracks.Add((empower, 0f));

                AddBurst(empower, Mathf.RoundToInt(p.burstCount * density * 1.5f));
            }

            if (plan.Entwine > 0f)
            {
                var p = SpellVisualModifierMath.GetEntwineParams(variant, plan.Entwine, s);
                // entwine: espiral estrecha muy visible alrededor del eje
                Vector3 aimDir = Frame.Direction(plan.Aim);
                var entwine = Track("Sign_Entwine", look, anchor, Frame.Facing(aimDir), s, local: true, gravityOverride: 0f);
                Shape(entwine, p.shapeType, s * 0.22f * p.shapeRadiusFactor, p.shapeAngle);
                Speed(entwine, speed * 0.25f, look.lifetime * p.lifetimeFactor);
                ApplyColor(entwine, p.startColor, p.endColor);
                ApplySize(entwine, look.size * s * p.sizeFactor * 1.7f);
                var vel = entwine.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.Local;
                vel.orbitalZ = new ParticleSystem.MinMaxCurve(p.orbitalSpeed * 1.6f);
                vel.radial = new ParticleSystem.MinMaxCurve(-0.18f * s);
                vel.z = new ParticleSystem.MinMaxCurve(speed * 0.8f);
                AddBody(entwine, p.rateFactor * density * 2.0f, impacts: false);
            }

            if (plan.Target > 0f)
            {
                var p = SpellVisualModifierMath.GetFocusAimParams(variant, plan.Target, s);
                // focus: haz fino y brillante tipo laser hacia delante y base reducida
                Vector3 aimDir = Frame.Direction(plan.Aim);
                var focusAim = Track("Sign_FocusAim", look, anchor, Frame.Facing(aimDir), s, local: false, gravityOverride: 0f);
                Shape(focusAim, ParticleSystemShapeType.Cone, s * 0.05f, 0.8f);
                float beamSpeed = speed * p.speedFactor * 1.3f;
                Speed(focusAim, beamSpeed, (s * (6f + 14f * Spell.range)) / beamSpeed);
                ApplyColor(focusAim, Color.white, p.startColor);
                ApplySize(focusAim, look.size * s * 0.65f);
                AddBody(focusAim, p.rateFactor * density * 2.5f, impacts: false);

                // Resplandor del laser en la base
                var muzzle = Track("Sign_Focus_Muzzle", look, anchor, Frame.Facing(aimDir), s, local: false, gravityOverride: 0f);
                Shape(muzzle, ParticleSystemShapeType.Sphere, s * 0.08f, 0f);
                Speed(muzzle, 0.1f, 0.2f);
                ApplyColor(muzzle, Color.white, p.startColor);
                ApplySize(muzzle, s * 1.2f);
                AddBody(muzzle, 30f, impacts: false);
            }

            if (plan.Gather > 0f)
            {
                var p = SpellVisualModifierMath.GetGatherParams(variant, plan.Gather, s);
                // gather: particulas convergiendo visiblemente hacia el sello (velocidad radial hacia adentro)
                var gather = Track("Sign_Gather", look, anchor, Frame.Facing(Frame.Normal), s, local: true, gravityOverride: 0f);
                float reach = s * p.shapeRadiusFactor;
                Shape(gather, p.shapeType, reach, p.shapeAngle);
                var shape = gather.shape;
                shape.radiusThickness = 0.05f;
                float inSpeed = speed * p.speedFactor;
                Speed(gather, inSpeed, reach / Mathf.Max(0.01f, Mathf.Abs(inSpeed)));
                ApplyColor(gather, p.startColor, p.endColor);
                ApplySize(gather, look.size * s * p.sizeFactor * 1.5f);
                var vel = gather.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.Local;
                vel.orbitalZ = new ParticleSystem.MinMaxCurve(p.orbitalSpeed);
                vel.radial = new ParticleSystem.MinMaxCurve(-speed * 1.8f);
                AddBody(gather, p.rateFactor * density * 1.8f, impacts: false);
            }

            if (plan.Collection > 0f)
            {
                var p = SpellVisualModifierMath.GetCollectionParams(variant, plan.Collection, s);
                // collection: pulsos periodicos hacia un punto sobre el sello
                Vector3 targetPoint = anchor + Frame.Normal * (s * 0.45f);
                var col = Track("Sign_Collection", look, targetPoint, Frame.Facing(Frame.Normal), s, local: true, gravityOverride: 0f);
                float reach = s * p.shapeRadiusFactor;
                Shape(col, p.shapeType, reach, 0f);
                var shape = col.shape;
                shape.radiusThickness = 0.05f;
                float inSpeed = speed * p.speedFactor;
                Speed(col, inSpeed, reach / Mathf.Max(0.01f, Mathf.Abs(inSpeed)));
                ApplyColor(col, p.startColor, p.endColor);
                ApplySize(col, look.size * s * p.sizeFactor * 1.4f);
                var vel = col.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.Local;
                vel.radial = new ParticleSystem.MinMaxCurve(p.radialSpeed * speed);

                var emission = col.emission;
                emission.rateOverTime = 0f;
                int pulseCount = Mathf.RoundToInt(p.burstCount * density);
                var pulseBurst = new ParticleSystem.Burst(0.0f, (short)pulseCount);
                pulseBurst.cycleCount = 15;
                pulseBurst.repeatInterval = 0.35f;
                emission.SetBursts(new[] { pulseBurst });
                bodyTracks.Add((col, 0f));

                var flash = Track("Sign_Collection_Flash", look, targetPoint, Frame.Facing(Frame.Normal), s, local: true, gravityOverride: 0f);
                Shape(flash, ParticleSystemShapeType.Sphere, s * 0.15f, 0f);
                Speed(flash, 0.2f, 0.22f);
                ApplyColor(flash, p.startColor, p.endColor);
                ApplySize(flash, s * 1.2f);
                var flashEmission = flash.emission;
                flashEmission.rateOverTime = 0f;
                var flashBurstObj = new ParticleSystem.Burst(0.2f, (short)4);
                flashBurstObj.cycleCount = 15;
                flashBurstObj.repeatInterval = 0.35f;
                flashEmission.SetBursts(new[] { flashBurstObj });
                bodyTracks.Add((flash, 0f));
            }

            if (plan.Repetition > 0f)
            {
                var p = SpellVisualModifierMath.GetRepetitionParams(variant, plan.Repetition, s);
                // repetition: particulas que vuelven al sello (reflujo) con destello
                var rep = Track("Sign_Repetition", look, anchor, Frame.Facing(Frame.Normal), s, local: true, gravityOverride: 0f);
                Shape(rep, p.shapeType, p.shapeRadiusFactor * s * 1.8f, p.shapeAngle);
                Speed(rep, speed * 0.4f, look.lifetime * p.lifetimeFactor * 1.2f);
                ApplyColor(rep, p.startColor, p.endColor);
                ApplySize(rep, look.size * s * p.sizeFactor * 1.5f);

                var vel = rep.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.Local;
                vel.z = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0f, speed * 0.6f),
                    new Keyframe(0.4f, 0f),
                    new Keyframe(1f, -speed * 1.2f)
                ));
                vel.radial = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0f, 0.25f * s),
                    new Keyframe(0.5f, 0f),
                    new Keyframe(1f, -0.8f * s)
                ));
                AddBody(rep, p.rateFactor * density * 1.4f, impacts: false);

                var flash = Track("Sign_Repetition_Flash", look, anchor, Frame.Facing(Frame.Normal), s, local: true, gravityOverride: 0f);
                Shape(flash, ParticleSystemShapeType.Sphere, s * 0.2f, 0f);
                Speed(flash, 0.3f, 0.2f);
                ApplyColor(flash, Color.white, p.startColor);
                ApplySize(flash, s * 1.8f);
                var flashEmission = flash.emission;
                flashEmission.rateOverTime = 0f;
                int flashBurst = Mathf.RoundToInt(p.burstCount * density * 0.4f);
                var repBurstObj = new ParticleSystem.Burst(0.0f, (short)flashBurst);
                repBurstObj.cycleCount = 10;
                repBurstObj.repeatInterval = 0.45f;
                flashEmission.SetBursts(new[] { repBurstObj });
                bodyTracks.Add((flash, 0f));
                AddBurst(rep, Mathf.RoundToInt(p.burstCount * density));
            }

            if (plan.Weave > 0f)
            {
                // weave: dos helices entrelazadas gruesas
                Vector3 aimDir = Frame.Direction(plan.Aim);
                var pA = SpellVisualModifierMath.GetWeaveParams(variant, plan.Weave, s, isSecondStrand: false);
                var pB = SpellVisualModifierMath.GetWeaveParams(variant, plan.Weave, s, isSecondStrand: true);

                float strandOffset = s * 0.3f;
                float strandSize = look.size * s * pA.sizeFactor * 1.9f;

                Vector3 posA = anchor + Frame.Right * strandOffset;
                var weaveA = Track("Sign_Weave_A", look, posA, Frame.Facing(aimDir), s, local: true, gravityOverride: 0f);
                Shape(weaveA, pA.shapeType, strandOffset, 0f);
                Speed(weaveA, speed * 0.2f, look.lifetime * pA.lifetimeFactor);
                ApplyColor(weaveA, pA.startColor, pA.endColor);
                ApplySize(weaveA, strandSize);
                var velA = weaveA.velocityOverLifetime;
                velA.enabled = true;
                velA.space = ParticleSystemSimulationSpace.Local;
                velA.orbitalZ = new ParticleSystem.MinMaxCurve(pA.orbitalSpeed * 1.4f);
                velA.z = new ParticleSystem.MinMaxCurve(speed * 0.85f);
                AddBody(weaveA, pA.rateFactor * density * 1.8f, impacts: false);

                Vector3 posB = anchor - Frame.Right * strandOffset;
                var weaveB = Track("Sign_Weave_B", look, posB, Frame.Facing(aimDir), s, local: true, gravityOverride: 0f);
                Shape(weaveB, pB.shapeType, strandOffset, 0f);
                Speed(weaveB, speed * 0.2f, look.lifetime * pB.lifetimeFactor);
                ApplyColor(weaveB, pB.startColor, pB.endColor);
                ApplySize(weaveB, strandSize);
                var velB = weaveB.velocityOverLifetime;
                velB.enabled = true;
                velB.space = ParticleSystemSimulationSpace.Local;
                velB.orbitalZ = new ParticleSystem.MinMaxCurve(pB.orbitalSpeed * 1.4f);
                velB.z = new ParticleSystem.MinMaxCurve(speed * 0.85f);
                AddBody(weaveB, pB.rateFactor * density * 1.8f, impacts: false);
            }
        }

        private void ApplyCoolTintToAll()
        {
            Color coolWhite = new Color(0.85f, 0.95f, 1f, 1f);
            Color coolBlue = new Color(0.25f, 0.55f, 1f, 0f);
            foreach (var ps in GetComponentsInChildren<ParticleSystem>())
            {
                if (ps.name.StartsWith("Sign_Cool")) continue;
                var col = ps.colorOverLifetime;
                col.enabled = true;
                col.color = new ParticleSystem.MinMaxGradient(SpellVisualModifierMath.CreateGradient(coolWhite, coolBlue));
                var main = ps.main;
                main.startColor = new Color(0.85f, 0.95f, 1f, 1f);
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
            float sMin = Mathf.Min(speed * 0.8f, speed * 1.2f);
            float sMax = Mathf.Max(speed * 0.8f, speed * 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(sMin, sMax);
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.8f, lifetime * 1.2f);
        }

        private static void Emit(ParticleSystem ps, float rate)
        {
            var emission = ps.emission;
            emission.rateOverTime = rate;
        }
    }
}
