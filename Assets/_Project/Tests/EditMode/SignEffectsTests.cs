using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Compiler;
using SpellSimulator.Core;
using SpellSimulator.Output;
using SpellSimulator.World;

namespace SpellSimulator.Tests
{
    /// <summary>Signos con efecto propio (crush, collection, billowing, repetition, weave, region), formas de tierra y recorte de mallas.</summary>
    public class SignEffectsTests
    {
        private static SignReading Sign(string manifestation, float bearingDeg = 270f, float length = 1f) => new SignReading
        {
            Id = manifestation,
            Manifestation = manifestation,
            At = GeometryUtils.VectorFromAngleDeg(bearingDeg) * 0.8f,
            Length = length,
            Facing = GeometryUtils.VectorFromAngleDeg(bearingDeg + 180f),
            FacingTrust = 0.9f
        };

        private static SpellPlan Plan(params SignReading[] signs) =>
            PlanResolver.Resolve(new List<SignReading>(signs), "earth", "earth", 0.9f);

        [Test]
        public void CadaSignoDeAreaQuedaEnElPlan()
        {
            var plan = Plan(Sign("crush"), Sign("collection", 0f, 2f), Sign("billowing", 90f), Sign("repetition", 180f), Sign("directed", 45f));
            Assert.AreEqual(1f, plan.Crush, 1e-4f);
            Assert.AreEqual(2f, plan.Collection, 1e-4f);
            Assert.AreEqual(1f, plan.Billow, 1e-4f);
            Assert.AreEqual(1f, plan.Repetition, 1e-4f);
            Assert.AreEqual(1f, plan.Directed, 1e-4f);
            Assert.IsTrue(plan.HasAreaSigns);
            Assert.IsFalse(plan.Notes.Exists(n => n.StartsWith("unmodeled-")), "ya estan modelados");
        }

        [Test]
        public void SinSignosNoHayEfectosDeArea()
        {
            var plan = Plan();
            Assert.IsFalse(plan.HasAreaSigns);
            Assert.AreEqual(0f, plan.Crush);
            Assert.AreEqual(0f, plan.Weave);
        }

        [Test]
        public void WeaveSumaGiroYHaceUnVortice()
        {
            var plan = Plan(Sign("weave", 270f, 1f));
            Assert.AreEqual(PlanResolver.WeaveSwirl, plan.Circulation, 1e-4f);
            Assert.IsTrue(plan.Has(PlanPrimitive.Vortex));
        }

        [Test]
        public void ColumnaHaceUnaFormaSolidaDeTierra()
        {
            var plan = Plan(Sign("column", 0f), Sign("column", 180f));
            Assert.IsTrue(EarthShapes.IsShape(plan));
            Assert.IsFalse(EarthShapes.IsShape(Plan()));
            Assert.IsFalse(EarthShapes.IsShape(null));
        }

        [Test]
        public void LaConvergenciaFijaLaForma()
        {
            Assert.IsFalse(RockMath.IsPermanent(1f));
            Assert.IsTrue(RockMath.IsPermanent(Plan(Sign("convergence", 270f, 1f)).Focus));
        }

        [Test]
        public void MurosPuentesYEstacasTienenLimites()
        {
            Assert.AreEqual(1.4f, RockMath.WallWidth(0.01f, 1f), 1e-4f);
            Assert.AreEqual(6f, RockMath.WallWidth(5f, 1f), 1e-4f);
            Assert.Greater(RockMath.WallWidth(0.4f, 8f), RockMath.WallWidth(0.4f, 1f));
            Assert.AreEqual(1.2f, RockMath.WallHeight(0f), 1e-4f);
            Assert.AreEqual(5f, RockMath.WallHeight(100f), 1e-4f);
            Assert.AreEqual(2f, RockMath.BridgeLength(0f), 1e-4f);
            Assert.AreEqual(7f, RockMath.BridgeLength(100f), 1e-4f);
            Assert.AreEqual(2, RockMath.WallRocks(0.1f));
            Assert.AreEqual(7, RockMath.WallRocks(50f));
            Assert.AreEqual(6, RockMath.SpikeCount(0.01f));
            Assert.AreEqual(14, RockMath.SpikeCount(5f));
            Assert.Greater(RockMath.LaunchSpeed(1f), RockMath.LaunchSpeed(0f));
        }

        [Test]
        public void ElRecorteSacaSoloLoQueEstaDentroDeLaEsfera()
        {
            // Dos triangulos: uno en el origen, otro lejos.
            var verts = new List<Vector3>
            {
                new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f),
                new Vector3(20f, 0f, 0f), new Vector3(21f, 0f, 0f), new Vector3(20f, 1f, 0f),
            };
            var tris = new List<int> { 0, 1, 2, 3, 4, 5 };
            var kept = new List<int>();
            var centroids = new List<Vector3>();
            int removed = MeshCut.Select(verts, tris, Vector3.zero, 2f, null, 0f, kept, centroids, 10);
            Assert.AreEqual(1, removed);
            Assert.AreEqual(new List<int> { 3, 4, 5 }, kept);
            Assert.AreEqual(1, centroids.Count);
        }

        [Test]
        public void ElRecorteRespetaLoProtegido()
        {
            var verts = new List<Vector3> { new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(0f, 1f, 0f) };
            var tris = new List<int> { 0, 1, 2 };
            var kept = new List<int>();
            int removed = MeshCut.Select(verts, tris, Vector3.zero, 5f, new List<Vector3> { new Vector3(0.5f, 0.5f, 0f) }, 1.6f, kept, null, 10);
            Assert.AreEqual(0, removed, "cerca de un libro o portal no se corta");
            Assert.AreEqual(3, kept.Count);
        }

        [Test]
        public void ElRecorteLimitaLosEscombros()
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < 20; i++)
            {
                verts.Add(new Vector3(i * 0.01f, 0f, 0f)); verts.Add(new Vector3(i * 0.01f + 0.1f, 0f, 0f)); verts.Add(new Vector3(i * 0.01f, 0.1f, 0f));
                tris.Add(i * 3); tris.Add(i * 3 + 1); tris.Add(i * 3 + 2);
            }
            var centroids = new List<Vector3>();
            int removed = MeshCut.Select(verts, tris, Vector3.zero, 3f, null, 0f, new List<int>(), centroids, 5);
            Assert.AreEqual(20, removed);
            Assert.AreEqual(5, centroids.Count);
        }
    }
}
