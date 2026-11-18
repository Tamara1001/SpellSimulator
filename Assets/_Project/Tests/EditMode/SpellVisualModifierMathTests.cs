using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Compiler;
using SpellSimulator.Output;

namespace SpellSimulator.Tests
{
    /// <summary>
    /// Pruebas EditMode de la lógica pura de modificadores visuales (Tanda 1 de MODIFICADORES_AUDITORIA.md).
    /// Verifica fórmulas de cono/gravedad y que cada signo se configure distintamente por elemento.
    /// </summary>
    public class SpellVisualModifierMathTests
    {
        private static readonly ElementVariant[] AllVariants = (ElementVariant[])Enum.GetValues(typeof(ElementVariant));

        [Test]
        public void ResolveVariant_DistingueTodosLosElementosYCristal()
        {
            Assert.AreEqual(ElementVariant.Fire, SpellVisualModifierMath.ResolveVariant(ElementType.Fire, false));
            Assert.AreEqual(ElementVariant.Water, SpellVisualModifierMath.ResolveVariant(ElementType.Water, false));
            Assert.AreEqual(ElementVariant.Wind, SpellVisualModifierMath.ResolveVariant(ElementType.Wind, false));
            Assert.AreEqual(ElementVariant.Earth, SpellVisualModifierMath.ResolveVariant(ElementType.Earth, false));
            Assert.AreEqual(ElementVariant.Crystal, SpellVisualModifierMath.ResolveVariant(ElementType.Earth, true));
            Assert.AreEqual(ElementVariant.Light, SpellVisualModifierMath.ResolveVariant(ElementType.Light, false));
        }

        [Test]
        public void CalculateConeAngle_EnsanchaConSpread_EstrechaConFoco()
        {
            float baseAngle = SpellVisualModifierMath.CalculateConeAngle(18f, spread: 0f, focus: 0f, convergence: 1f);
            Assert.AreEqual(18f, baseAngle, 0.01f);

            // Spread ensancha el cono
            float wideAngle = SpellVisualModifierMath.CalculateConeAngle(18f, spread: 0.5f, focus: 0f, convergence: 1f);
            Assert.Greater(wideAngle, baseAngle);
            Assert.AreEqual(18f * (1f + 1.8f * 0.5f), wideAngle, 0.01f);

            // Convergencia y focus estrechan el cono
            float narrowAngle = SpellVisualModifierMath.CalculateConeAngle(18f, spread: 0f, focus: 1f, convergence: 2f);
            Assert.Less(narrowAngle, baseAngle);

            // Clamping en límites [2, 50]
            float extremeNarrow = SpellVisualModifierMath.CalculateConeAngle(18f, spread: 0f, focus: 10f, convergence: 10f);
            Assert.AreEqual(2f, extremeNarrow, 0.01f);

            float extremeWide = SpellVisualModifierMath.CalculateConeAngle(18f, spread: 5f, focus: 0f, convergence: 0.5f);
            Assert.AreEqual(50f, extremeWide, 0.01f);
        }

        [Test]
        public void CalculateConeRadius_EscalaConSpreadYConvergencia()
        {
            float baseRadius = 0.35f;
            float rBase = SpellVisualModifierMath.CalculateConeRadius(baseRadius, spread: 0f, convergence: 1f);
            Assert.AreEqual(baseRadius, rBase, 0.001f);

            float rWide = SpellVisualModifierMath.CalculateConeRadius(baseRadius, spread: 1f, convergence: 1f);
            Assert.Greater(rWide, rBase);

            float rFocused = SpellVisualModifierMath.CalculateConeRadius(baseRadius, spread: 0f, convergence: 2f);
            Assert.Less(rFocused, rBase);
        }

        [Test]
        public void CalculateEffectiveGravity_ModulaPorSpellGravity()
        {
            float waterGravity = 0.8f;
            // spell.gravity = 1 -> gravedad normal
            Assert.AreEqual(0.8f, SpellVisualModifierMath.CalculateEffectiveGravity(waterGravity, 1f), 0.001f);

            // spell.gravity = 0 (levitación total) -> gravedad cero
            Assert.AreEqual(0f, SpellVisualModifierMath.CalculateEffectiveGravity(waterGravity, 0f), 0.001f);

            // Gravedad aumentada
            Assert.AreEqual(1.6f, SpellVisualModifierMath.CalculateEffectiveGravity(waterGravity, 2f), 0.001f);

            // Gravedad negativa (fuego asciende)
            float fireGravity = -0.3f;
            Assert.AreEqual(-0.3f, SpellVisualModifierMath.CalculateEffectiveGravity(fireGravity, 1f), 0.001f);
            Assert.AreEqual(0f, SpellVisualModifierMath.CalculateEffectiveGravity(fireGravity, 0f), 0.001f);
        }

        [Test]
        public void CreateGradient_ConstruyeClavesCorrectas()
        {
            Color c1 = new Color(1f, 0f, 0f, 0.8f);
            Color c2 = new Color(0f, 0f, 1f, 0.2f);
            Gradient grad = SpellVisualModifierMath.CreateGradient(c1, c2);

            Assert.IsNotNull(grad);
            Assert.AreEqual(2, grad.colorKeys.Length);
            Assert.AreEqual(2, grad.alphaKeys.Length);
            Assert.AreEqual(c1.r, grad.colorKeys[0].color.r, 0.01f);
            Assert.AreEqual(c2.b, grad.colorKeys[1].color.b, 0.01f);
            Assert.AreEqual(c1.a, grad.alphaKeys[0].alpha, 0.01f);
            Assert.AreEqual(c2.a, grad.alphaKeys[1].alpha, 0.01f);
        }

        [Test]
        public void CrushParams_DifierenPorElementoEnColorFormaOEmision()
        {
            var colors = new HashSet<Color>();
            var shapes = new HashSet<ParticleSystemShapeType>();

            foreach (var variant in AllVariants)
            {
                var p = SpellVisualModifierMath.GetCrushParams(variant, 1f, 1f);
                Assert.Greater(p.burstCount, 0, $"Crush en {variant} debe emitir partículas");
                Assert.Greater(p.sizeFactor, 0f);
                colors.Add(p.startColor);
                shapes.Add(p.shapeType);
            }

            // Cada variante tiene color distintivo
            Assert.AreEqual(6, colors.Count, "Cada elemento debe tener un color de Crush distintivo");
            Assert.GreaterOrEqual(shapes.Count, 3, "Debe haber variedad de formas en Crush");
        }

        [Test]
        public void OrbParams_DifierenPorElementoEnColorVelocidadYForma()
        {
            var colors = new HashSet<Color>();
            var orbitalSpeeds = new HashSet<float>();

            foreach (var variant in AllVariants)
            {
                var p = SpellVisualModifierMath.GetOrbParams(variant, 1f, 1f);
                Assert.Greater(p.rateFactor, 0f, $"Orb en {variant} debe tener emisión continua");
                colors.Add(p.startColor);
                orbitalSpeeds.Add(p.orbitalSpeed);
            }

            Assert.AreEqual(6, colors.Count, "Cada elemento debe tener un color de Orbe distintivo");
            Assert.GreaterOrEqual(orbitalSpeeds.Count, 4, "Debe haber dinámicas orbitales distintivas por elemento");
        }

        [Test]
        public void BillowingParams_GeneranNubesVoluminosasConColoresDistintos()
        {
            var colors = new HashSet<Color>();

            foreach (var variant in AllVariants)
            {
                var p = SpellVisualModifierMath.GetBillowingParams(variant, 1f, 1f);
                Assert.GreaterOrEqual(p.sizeFactor, 2.0f, $"Billowing en {variant} debe ser voluminoso (sizeFactor >= 2)");
                Assert.LessOrEqual(p.speedFactor, 0.5f, $"Billowing en {variant} debe ser lento y viscoso");
                colors.Add(p.startColor);
            }

            Assert.AreEqual(6, colors.Count, "Cada elemento debe tener un color de nube distintivo");
        }

        [Test]
        public void CoolParams_AportanTinteFrioPorElemento()
        {
            var colors = new HashSet<Color>();

            foreach (var variant in AllVariants)
            {
                var p = SpellVisualModifierMath.GetCoolParams(variant, 1f, 1f);
                Assert.Greater(p.rateFactor, 0f);
                // El componente azul debe ser prominente en el tinte frío (o verde auroral en luz)
                Assert.IsTrue(p.startColor.b > 0.4f || p.startColor.g > 0.8f, $"Cool en {variant} debe tener tinte gélido");
                colors.Add(p.startColor);
            }

            Assert.AreEqual(6, colors.Count, "Cada elemento debe tener un tinte de enfriamiento distintivo");
        }

        [Test]
        public void EmpowerParams_IntensificanEmisionYBrilloPorElemento()
        {
            var colors = new HashSet<Color>();

            foreach (var variant in AllVariants)
            {
                var p = SpellVisualModifierMath.GetEmpowerParams(variant, 1f, 1f);
                Assert.GreaterOrEqual(p.lightIntensityBoost, 1.8f, $"Empower en {variant} debe aumentar la luz");
                Assert.Greater(p.burstCount, 0, $"Empower en {variant} debe emitir ráfaga");
                colors.Add(p.startColor);
            }

            Assert.AreEqual(6, colors.Count, "Cada elemento debe tener color de potenciación distintivo");
        }

        [Test]
        public void EntwineParams_DefinenMovimientoHelicoidalYColoresDistintos()
        {
            var colors = new HashSet<Color>();

            foreach (var variant in AllVariants)
            {
                var p = SpellVisualModifierMath.GetEntwineParams(variant, 1f, 1f);
                Assert.Greater(p.orbitalSpeed, 0f, $"Entwine en {variant} debe rotar en espiral");
                Assert.Less(p.radialSpeed, 0f, $"Entwine en {variant} debe constreñir hacia el centro");
                colors.Add(p.startColor);
            }

            Assert.AreEqual(6, colors.Count, "Cada elemento debe tener color de zarcillo distintivo");
        }

        [Test]
        public void FocusAimParams_GeneranHazUltraAngostoYVeloz()
        {
            var colors = new HashSet<Color>();

            foreach (var variant in AllVariants)
            {
                var p = SpellVisualModifierMath.GetFocusAimParams(variant, 1f, 1f);
                Assert.AreEqual(ParticleSystemShapeType.Cone, p.shapeType);
                Assert.LessOrEqual(p.shapeAngle, 2.5f, $"Focus en {variant} debe ser un haz estrecho (<= 2.5 deg)");
                Assert.GreaterOrEqual(p.speedFactor, 2.0f, $"Focus en {variant} debe ser un rayo de alta velocidad");
                colors.Add(p.startColor);
            }

            Assert.AreEqual(6, colors.Count, "Cada elemento debe tener color de mira láser distintivo");
        }

        [Test]
        public void GatherParams_GeneranSuccionCentripetaConVelocidadNegativa()
        {
            var colors = new HashSet<Color>();

            foreach (var variant in AllVariants)
            {
                var p = SpellVisualModifierMath.GetGatherParams(variant, 1f, 1f);
                Assert.Less(p.speedFactor, 0f, $"Gather en {variant} debe atraer hacia el centro (speedFactor < 0)");
                Assert.Greater(p.shapeRadiusFactor, 2f, $"Gather en {variant} debe tener alcance exterior grande");
                colors.Add(p.startColor);
            }

            Assert.AreEqual(6, colors.Count, "Cada elemento debe tener color de recolección distintivo");
        }

        [Test]
        public void RepetitionParams_GeneranInversionTemporalYDestellos()
        {
            var colors = new HashSet<Color>();

            foreach (var variant in AllVariants)
            {
                var p = SpellVisualModifierMath.GetRepetitionParams(variant, 1f, 1f);
                Assert.Less(p.speedFactor, 0f, $"Repetition en {variant} debe invertir partículas (speedFactor < 0)");
                Assert.Greater(p.burstCount, 0, $"Repetition en {variant} debe tener ráfagas de pulso temporal");
                colors.Add(p.startColor);
            }

            Assert.AreEqual(6, colors.Count, "Cada elemento debe tener color de rebobinado distintivo");
        }

        [Test]
        public void CollectionParams_GeneranPulsosPeriodicosPorElemento()
        {
            var colors = new HashSet<Color>();

            foreach (var variant in AllVariants)
            {
                var p = SpellVisualModifierMath.GetCollectionParams(variant, 1f, 1f);
                Assert.Less(p.speedFactor, 0f, $"Collection en {variant} debe converger (speedFactor < 0)");
                Assert.Greater(p.burstCount, 0, $"Collection en {variant} debe tener pulsos periódicos de ráfaga");
                Assert.Greater(p.shapeRadiusFactor, 2f, $"Collection en {variant} debe tener alcance exterior para converger");
                colors.Add(p.startColor);
            }

            Assert.AreEqual(6, colors.Count, "Cada elemento debe tener color de acumulación distintivo");
        }

        [Test]
        public void WeaveParams_DobleHeliceConFasesYColoresComplementarios()
        {
            foreach (var variant in AllVariants)
            {
                var pA = SpellVisualModifierMath.GetWeaveParams(variant, 1f, 1f, isSecondStrand: false);
                var pB = SpellVisualModifierMath.GetWeaveParams(variant, 1f, 1f, isSecondStrand: true);

                // Emisores gemelos giran en sentido opuesto en espacio local
                Assert.AreEqual(-pA.orbitalSpeed, pB.orbitalSpeed, 0.001f, $"Weave en {variant} debe tener giro helicoidal opuesto");
                // Colores de las dos hebras son armoniosos pero diferenciados
                Assert.AreNotEqual(pA.startColor, pB.startColor, $"Hebras A y B de Weave en {variant} deben contrastar");
            }
        }
    }
}
