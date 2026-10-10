using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Learning;

namespace SpellSimulator.Tests
{
    public class SpellLibraryTests
    {
        private static List<List<Vector2>> SomeStrokes()
        {
            return new List<List<Vector2>>
            {
                new List<Vector2> { new Vector2(-0.2f, 0f), new Vector2(0f, 0.3f), new Vector2(0.2f, 0f) },
                SpellStrokeBuilder.Ring(34f),
            };
        }

        [Test]
        public void AgregaConNombreAutomaticoYNoRepite()
        {
            var lib = new SpellLibrary();
            SpellDef a = lib.Add(null, "fire", SomeStrokes(), 34f);
            SpellDef b = lib.Add(null, "fire", SomeStrokes(), 34f);
            Assert.AreEqual("Fuego propio", a.Name);
            Assert.AreEqual("Fuego propio 2", b.Name);
            Assert.AreEqual(SpellLibrary.PlayerOrigin, a.Origin);
            Assert.IsTrue(a.IsInitial);
        }

        [Test]
        public void RenombrarLimpiaYRechazaVacios()
        {
            var lib = new SpellLibrary();
            SpellDef a = lib.Add(null, "water", SomeStrokes(), 34f);
            Assert.IsTrue(lib.Rename(a.Id, "  <b>Mi ola</b>  "));
            Assert.AreEqual("bMi ola/b", a.Name);
            Assert.IsFalse(lib.Rename(a.Id, "   "));
            Assert.IsTrue(lib.Rename(a.Id, new string('x', 80)));
            Assert.AreEqual(SpellLibrary.MaxNameLength, a.Name.Length);
        }

        [Test]
        public void BorraYRespetaElTope()
        {
            var lib = new SpellLibrary();
            for (int i = 0; i < SpellLibrary.MaxSpells; i++) Assert.IsNotNull(lib.Add(null, "wind", SomeStrokes(), 34f));
            Assert.IsNull(lib.Add(null, "wind", SomeStrokes(), 34f));
            Assert.IsTrue(lib.Remove(lib.Items[0].Id));
            Assert.IsTrue(lib.CanAdd);
            Assert.IsFalse(lib.Remove("no-existe"));
        }

        [Test]
        public void SinTrazosNoSeGuarda()
        {
            var lib = new SpellLibrary();
            Assert.IsNull(lib.Add(null, "fire", new List<List<Vector2>>(), 34f));
            Assert.IsNull(lib.Add(null, "fire", null, 34f));
        }

        [Test]
        public void IdaYVueltaPorJsonConservaTrazos()
        {
            var lib = new SpellLibrary();
            SpellDef a = lib.Add("Chispa", "fire", SomeStrokes(), 34f);
            SpellLibrary back = SpellLibrary.FromJson(lib.ToJson());
            Assert.AreEqual(1, back.Count);
            SpellDef b = back.Items[0];
            Assert.AreEqual(a.Id, b.Id);
            Assert.AreEqual("Chispa", b.Name);
            Assert.AreEqual("fire", b.Element);
            Assert.AreEqual(a.Strokes.Count, b.Strokes.Count);
            Assert.AreEqual(a.Strokes[0][1].y, b.Strokes[0][1].y, 1e-3f);
        }

        [Test]
        public void UnaEntradaInvalidaNoTiraAbajoLasDemas()
        {
            string json = "{\"schemaVersion\":1,\"spells\":[{\"id\":\"\",\"name\":\"\"},{\"id\":\"player-1\",\"name\":\"Ok\",\"element\":\"earth\",\"strokes\":[[[0,0],[1,1]]]}]}";
            var errors = new List<string>();
            SpellLibrary lib = SpellLibrary.FromJson(json, errors.Add);
            Assert.AreEqual(1, lib.Count);
            Assert.AreEqual(1, errors.Count);
        }

        [Test]
        public void ElCatalogoAgregaYQuitaSoloLosPropios()
        {
            var catalog = SpellCatalog.FromJsons(new[] { "{\"id\":\"gust\",\"name\":\"Gust\",\"element\":\"wind\",\"sigil\":\"wind-directs-air\",\"book\":\"inicial\"}" });
            var lib = new SpellLibrary();
            SpellDef own = lib.Add(null, "fire", SomeStrokes(), 34f);
            Assert.IsTrue(catalog.AddPlayer(own));
            Assert.IsFalse(catalog.AddPlayer(own));
            Assert.IsFalse(catalog.RemovePlayer("gust"));
            Assert.IsTrue(catalog.RemovePlayer(own.Id));
            Assert.IsNull(catalog.Get(own.Id));
            Assert.IsNotNull(catalog.Get("gust"));
        }

        [Test]
        public void ElRegistroQuitaElAnilloCerradoYPoneUnoAbierto()
        {
            var circle = new List<Vector2>();
            for (int i = 0; i <= 48; i++) { float a = i / 48f * Mathf.PI * 2f; circle.Add(new Vector2(0.5f + 0.2f * Mathf.Cos(a), 0.5f + 0.2f * Mathf.Sin(a))); }
            var glyph = new List<Vector2> { new Vector2(0.45f, 0.5f), new Vector2(0.5f, 0.55f), new Vector2(0.55f, 0.5f) };
            var strokes = new List<List<Vector2>> { glyph, circle };
            var result = SpellRecording.ToRingUnits(strokes, new Vector2(0.5f, 0.5f), new Vector2(0.4f, 0.4f), 0.08f, 34f);
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count); // el glifo y el anillo nuevo (abierto)
            Assert.AreEqual(SpellStrokeBuilder.RingSegments + 1, result[1].Count);
            Assert.AreEqual(1f, result[1][0].magnitude, 1e-3f);
            // el glifo: 0.05 UV * 0.4 m / 0.08 m = 0.25 unidades de anillo
            Assert.AreEqual(0.25f, Mathf.Abs(result[0][0].x), 1e-3f);
        }

        [Test]
        public void UnTrazoCortoNoEsElAnillo()
        {
            var arc = new List<Vector2>();
            for (int i = 0; i <= 20; i++) { float a = i / 20f * Mathf.PI * 0.5f; arc.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a))); }
            Assert.IsFalse(SpellRecording.IsRingLike(arc));
        }
    }
}
