using UnityEngine;
using SpellSimulator.Compiler;

namespace SpellSimulator.Output
{
    /// <summary>
    /// Extras visuales de SpellManifestation: region (cerco por elemento), tinta resistente y estabilidad del trazo
    /// (tandas 2 y 3 de Docs/MODIFICADORES_AUDITORIA.md).
    /// </summary>
    public partial class SpellManifestation
    {
        /// <summary>Intensidad de inestabilidad 0..1 (0 = trazo prolijo). Se fija en Init.</summary>
        private float jitter;

        private float FlickerFactor()
        {
            if (jitter <= 0f) return 1f;
            return SpellVisualExtrasMath.Flicker(jitter, Mathf.PerlinNoise(Time.time * 14f, 0.37f));
        }

        private static float SignLength(SpellPlan plan, string id)
        {
            float total = 0f;
            for (int i = 0; i < plan.Signs.Count; i++) if (plan.Signs[i].Id == id) total += plan.Signs[i].Length;
            return total;
        }

        /// <summary>
        /// Float: motas grandes y suaves que suben despacio y se mecen (sin direccion). Levitation: haz fino hacia arriba y anillos que suben
        /// y se achican, como el agarre que sostiene un orbe a una altura fija.
        /// </summary>
        private void ApplyLevitation(SpellPlan plan, Vector3 anchor, float s, float density)
        {
            float floatLen = SignLength(plan, "float");
            float levLen = SignLength(plan, "levitation");
            Vector3 up = Frame.Normal;
            Color tint = look.color != null && look.color.colorKeys.Length > 0 ? look.color.Evaluate(0.2f) : Color.white;
            tint.a = 1f;
            Color fade = new Color(tint.r, tint.g, tint.b, 0f);
            if (floatLen > 0f)
            {
                float h = LevitationMath.HoverHeight(true, s, floatLen);
                var motes = Track("Sign_Float", look, anchor + up * (s * 0.1f), Frame.Facing(up), s, local: false, gravityOverride: -0.04f);
                Shape(motes, ParticleSystemShapeType.Circle, s * 1.7f, 0f);
                Speed(motes, 0.55f, h / 0.55f * 0.9f);
                ApplyColor(motes, Color.Lerp(tint, Color.white, 0.5f), fade);
                ApplySize(motes, look.size * s * 0.9f);
                var n = motes.noise; n.enabled = true; n.strength = 0.45f * s; n.frequency = 0.7f;
                var sol = motes.sizeOverLifetime; sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.4f)));
                AddBody(motes, 38f * density, impacts: false);
            }
            if (levLen > 0f)
            {
                float h = LevitationMath.HoverHeight(false, s, levLen);
                var beam = Track("Sign_Levitation_Beam", look, anchor, Frame.Facing(up), s, local: true, gravityOverride: 0f);
                Shape(beam, ParticleSystemShapeType.Circle, s * 0.35f, 0f);
                Speed(beam, h * 2.2f, 0.45f);
                ApplyColor(beam, Color.white, fade);
                ApplySize(beam, look.size * s * 0.3f);
                Stretch(beam, 0.35f, 2.6f);
                var bv = beam.velocityOverLifetime; bv.enabled = true; bv.space = ParticleSystemSimulationSpace.Local;
                bv.z = new ParticleSystem.MinMaxCurve(h * 2.2f);
                AddBody(beam, 140f * density, impacts: false);

                var ring = Track("Sign_Levitation_Rings", look, anchor + up * (s * 0.05f), Frame.Facing(up), s, local: true, gravityOverride: 0f);
                Shape(ring, ParticleSystemShapeType.Circle, s * 1.3f, 0f);
                var rs = ring.shape; rs.radiusThickness = 0f;
                Speed(ring, 0f, 1f);
                ApplyColor(ring, Color.Lerp(tint, Color.white, 0.4f), fade);
                ApplySize(ring, look.size * s * 0.35f);
                var rv = ring.velocityOverLifetime; rv.enabled = true; rv.space = ParticleSystemSimulationSpace.Local;
                rv.z = new ParticleSystem.MinMaxCurve(h * 0.9f);
                rv.radial = new ParticleSystem.MinMaxCurve(-s * 0.9f);
                RepeatingBurst(ring, Mathf.RoundToInt(60f * density), 0.55f);
            }
        }

        /// <summary>Region: la magia no sale en chorro sino que dibuja un cerco propio de cada elemento.</summary>
        private void ApplyRegion(SpellPlan plan, ElementVariant variant, Vector3 anchor, float s, float speed, float density)
        {
            if (plan.Directed <= 0f) return;
            float g = SpellVisualModifierMath.CalculateEffectiveGravity(look.gravity, Spell.gravity);
            float radius = SpellVisualExtrasMath.RegionRadius(s);
            Vector3 up = Frame.Normal;
            Vector3 forward = Vector3.ProjectOnPlane(Frame.Direction(plan.Aim), up);
            if (forward.sqrMagnitude < 0.0001f) forward = Frame.Right;
            forward.Normalize();
            Color tint = look.color != null && look.color.colorKeys.Length > 0 ? look.color.Evaluate(0.2f) : Color.white;

            switch (variant)
            {
                case ElementVariant.Fire:
                {
                    // anillo de fuego: llamas altas sobre un circulo
                    var ring = Track("Region_FireRing", look, anchor, Frame.Facing(up), s, local: false, gravityOverride: g);
                    Shape(ring, ParticleSystemShapeType.Circle, radius, 0f);
                    var shape = ring.shape; shape.radiusThickness = 0f;
                    Speed(ring, 0.15f, look.lifetime * 1.5f);
                    ApplySize(ring, look.size * s * 1.7f);
                    var vel = ring.velocityOverLifetime;
                    vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
                    vel.z = new ParticleSystem.MinMaxCurve(speed * 0.45f);
                    AddBody(ring, 520f * density, impacts: false);
                    break;
                }
                case ElementVariant.Water:
                {
                    // cortina de agua: lamina que cae desde lo alto, ancha y fina
                    float height = s * 3.2f;
                    Vector3 center = anchor + forward * (s * 1.4f) + up * height;
                    var curtain = Track("Region_WaterCurtain", look, center, Quaternion.LookRotation(-up, forward), s, local: false, gravityOverride: 0.35f);
                    Shape(curtain, ParticleSystemShapeType.Box, 1f, 0f);
                    var shape = curtain.shape; shape.scale = new Vector3(radius * 2f, 0.15f * s, 0.05f * s);
                    Speed(curtain, 1.6f, height / 2.6f);
                    ApplySize(curtain, look.size * s * 0.9f);
                    AddBody(curtain, 900f * density, impacts: true);
                    break;
                }
                case ElementVariant.Wind:
                {
                    // muro de viento: plano vertical de rafagas que corre hacia adelante
                    Vector3 center = anchor + forward * (s * 1.2f) + up * (s * 1.5f);
                    var wall = Track("Region_WindWall", look, center, Quaternion.LookRotation(forward, up), s, local: false, gravityOverride: 0f);
                    Shape(wall, ParticleSystemShapeType.Box, 1f, 0f);
                    var shape = wall.shape; shape.scale = new Vector3(radius * 2f, s * 3f, 0.1f * s);
                    Speed(wall, speed * 0.9f, 0.7f);
                    Stretch(wall, 0.2f, 2.4f);
                    AddBody(wall, 420f * density, impacts: false);
                    break;
                }
                case ElementVariant.Light:
                {
                    // perimetro de luz: pilares finos que suben sobre un circulo
                    var ring = Track("Region_LightPerimeter", look, anchor, Frame.Facing(up), s, local: true, gravityOverride: 0f);
                    Shape(ring, ParticleSystemShapeType.Circle, radius, 0f);
                    var shape = ring.shape; shape.radiusThickness = 0f;
                    Speed(ring, 0f, 1.5f);
                    ApplyColor(ring, Color.white, Color.Lerp(tint, Color.white, 0.4f));
                    ApplySize(ring, look.size * s * 0.7f);
                    var vel = ring.velocityOverLifetime;
                    vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
                    vel.z = new ParticleSystem.MinMaxCurve(1.4f * s + 0.6f);
                    Stretch(ring, 0.6f, 3.2f);
                    AddBody(ring, 220f * density, impacts: false);
                    break;
                }
                case ElementVariant.Crystal:
                {
                    // domo de cristal: casquete de esquirlas casi quietas
                    var dome = Track("Region_CrystalDome", look, anchor, Frame.Facing(up), s, local: true, gravityOverride: 0f);
                    Shape(dome, ParticleSystemShapeType.Hemisphere, radius, 0f);
                    var shape = dome.shape; shape.radiusThickness = 0f;
                    Speed(dome, 0.05f, 2.4f);
                    ApplySize(dome, look.size * s * 2.6f);
                    var vel = dome.velocityOverLifetime;
                    vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
                    vel.orbitalZ = new ParticleSystem.MinMaxCurve(0.35f);
                    AddBody(dome, 320f * density, impacts: false);
                    break;
                }
                default:
                {
                    // tierra: cerco bajo de polvo y esquirlas que brotan del suelo (la roca ya sale lanzada por RockSpell)
                    var ring = Track("Region_EarthRing", look, anchor, Frame.Facing(up), s, local: false, gravityOverride: Mathf.Min(g, 0.6f));
                    Shape(ring, ParticleSystemShapeType.Circle, radius, 0f);
                    var shape = ring.shape; shape.radiusThickness = 0f;
                    Speed(ring, 0.5f, 1.3f);
                    ApplySize(ring, look.size * s * 2.4f);
                    var vel = ring.velocityOverLifetime;
                    vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
                    vel.z = new ParticleSystem.MinMaxCurve(speed * 0.35f);
                    AddBody(ring, 420f * density, impacts: false);
                    break;
                }
            }
        }

        /// <summary>Tinta resistente: un anillo de destellos pale que repele las gotas, para que se note que el sello aguanta el agua.</summary>
        private void ApplyWaterproof(ElementVariant variant, Vector3 anchor, float s)
        {
            if (!Spell.waterproof) return;
            var ward = Track("Sign_Waterproof", look, anchor + Frame.Normal * (s * 0.08f), Frame.Facing(Frame.Normal), s, local: false, gravityOverride: 0f);
            Shape(ward, ParticleSystemShapeType.Circle, s * 1.15f, 0f);
            var shape = ward.shape; shape.radiusThickness = 0f;
            Speed(ward, 0.45f, 0.7f);
            ApplyColor(ward, new Color(0.8f, 0.97f, 1f, 0.95f), new Color(0.35f, 0.75f, 1f, 0f));
            ApplySize(ward, look.size * s * 0.55f);
            Stretch(ward, 0.1f, 1.4f);
            AddBody(ward, 70f, impacts: false);
        }

        /// <summary>Estabilidad baja: las particulas tiemblan, saltan chispas y la luz parpadea.</summary>
        private void ApplyStability(Vector3 anchor, float s)
        {
            jitter = SpellVisualExtrasMath.Jitter(Spell.stability);
            if (jitter <= 0f) return;
            foreach (var ps in GetComponentsInChildren<ParticleSystem>())
            {
                var n = ps.noise;
                if (n.enabled) { n.strength = n.strength.constant + jitter * 0.4f * s; continue; }
                n.enabled = true;
                n.strength = jitter * 0.75f * s;
                n.frequency = 2.6f;
            }
            var spark = Track("Sign_Unstable_Crackle", look, anchor + Frame.Normal * (s * 0.3f), Frame.Facing(Frame.Normal), s, local: false, gravityOverride: 0.2f);
            Shape(spark, ParticleSystemShapeType.Sphere, s * 0.7f, 0f);
            Speed(spark, 3.2f, 0.3f);
            ApplyColor(spark, Color.white, new Color(1f, 0.85f, 0.4f, 0f));
            ApplySize(spark, look.size * s * 0.45f);
            Stretch(spark, 0.08f, 2.4f);
            var emission = spark.emission;
            emission.rateOverTime = 0f;
            var burst = new ParticleSystem.Burst(0f, (short)SpellVisualExtrasMath.CrackleCount(jitter));
            burst.cycleCount = 40;
            burst.repeatInterval = 0.14f;
            emission.SetBursts(new[] { burst });
            bodyTracks.Add((spark, 0f));
        }
    }
}
