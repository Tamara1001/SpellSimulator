using System.Collections.Generic;
using NUnit.Framework;
using SpellSimulator.World;
using UnityEngine;

namespace SpellSimulator.Tests
{
    public class TutorialTraceRulesTests
    {
        private static List<List<Vector2>> Line(Vector2 a, Vector2 b, int n = 20)
        {
            var s = new List<Vector2>();
            for (int i = 0; i <= n; i++) s.Add(Vector2.Lerp(a, b, i / (float)n));
            return new List<List<Vector2>> { s };
        }

        private static List<List<Vector2>> Scaled(List<List<Vector2>> src, float k, Vector2 offset, float jitter = 0f)
        {
            var rnd = new System.Random(7);
            var r = new List<List<Vector2>>();
            foreach (var s in src)
            {
                var p = new List<Vector2>();
                foreach (var v in s)
                    p.Add(v * k + offset + new Vector2((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f) * jitter);
                r.Add(p);
            }
            return r;
        }

        private static IReadOnlyList<IReadOnlyList<Vector2>> Ro(List<List<Vector2>> l)
        {
            var r = new List<IReadOnlyList<Vector2>>();
            foreach (var s in l) r.Add(s);
            return r;
        }

        // Una "tierra" de juguete: una L y una raya, dos trazos.
        private static List<List<Vector2>> Toy() => new List<List<Vector2>>
        {
            new List<Vector2> { new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f) },
            new List<Vector2> { new Vector2(0.2f, 0.5f), new Vector2(0.8f, 0.5f) },
        };

        [Test]
        public void Circulo_DibujadoGrandeYDesplazado_Coincide()
        {
            var drawn = Scaled(TutorialTraceRules.Circle(), 0.6f, new Vector2(0.3f, 0.35f), 0.01f);
            Assert.IsTrue(TutorialTraceRules.Matches(Ro(drawn), Ro(TutorialTraceRules.Circle()), out float e), "error " + e);
        }

        [Test]
        public void Circulo_ContraUnaLinea_NoCoincide()
        {
            var drawn = Line(new Vector2(0.1f, 0.1f), new Vector2(0.8f, 0.7f));
            Assert.IsFalse(TutorialTraceRules.Matches(Ro(drawn), Ro(TutorialTraceRules.Circle()), out _));
        }

        [Test]
        public void Columna_ConTemblor_Coincide_YEnOtroSentido_Tambien()
        {
            var template = Line(new Vector2(0.5f, 0.1f), new Vector2(0.5f, 0.9f));
            var drawn = Scaled(Line(new Vector2(0.52f, 0.9f), new Vector2(0.49f, 0.12f)), 1f, Vector2.zero, 0.02f);
            Assert.IsTrue(TutorialTraceRules.Matches(Ro(drawn), Ro(template), out float e), "error " + e);
        }

        [Test]
        public void Columna_Inclinada_NoCoincide()
        {
            var template = Line(new Vector2(0.5f, 0.1f), new Vector2(0.5f, 0.9f));
            var drawn = Line(new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.9f));
            // Una diagonal contra una vertical: el error medio ronda 0,2 del lado mayor.
            Assert.IsFalse(TutorialTraceRules.Matches(Ro(drawn), Ro(template), out _));
        }

        [Test]
        public void Sigilo_OtroOrdenDeTrazos_YOtroTamaño_Coincide()
        {
            var template = Toy();
            var swapped = new List<List<Vector2>> { template[1], template[0] };
            var drawn = Scaled(swapped, 0.5f, new Vector2(0.2f, 0.25f), 0.01f);
            Assert.IsTrue(TutorialTraceRules.Matches(Ro(drawn), Ro(template), out float e), "error " + e);
        }

        [Test]
        public void Sigilo_AlQueLeFaltaCasiTodo_NoCoincide()
        {
            var template = Toy();
            var drawn = new List<List<Vector2>> { template[1] };
            Assert.IsFalse(TutorialTraceRules.Matches(Ro(drawn), Ro(template), out _));
        }

        [Test]
        public void GarabatoDiminuto_NoCuenta()
        {
            var drawn = Scaled(TutorialTraceRules.Circle(), 0.05f, new Vector2(0.5f, 0.5f));
            Assert.IsFalse(TutorialTraceRules.Matches(Ro(drawn), Ro(TutorialTraceRules.Circle()), out _));
        }

        [Test]
        public void SinTrazos_NoCuenta()
        {
            Assert.IsFalse(TutorialTraceRules.Matches(Ro(new List<List<Vector2>>()), Ro(TutorialTraceRules.Circle()), out _));
            Assert.IsFalse(TutorialTraceRules.Matches(null, Ro(TutorialTraceRules.Circle()), out _));
        }

        [Test]
        public void FitToSquare_CentraYEscalaPorElLadoMayor()
        {
            var fitted = TutorialTraceRules.FitToSquare(Ro(Scaled(Toy(), 3f, new Vector2(5f, 7f))), 0.6f);
            Assert.AreEqual(0.6f, TutorialTraceRules.Extent(Ro(fitted)), 1e-4f);
            float minX = float.MaxValue, maxX = float.MinValue;
            foreach (var s in fitted) foreach (var p in s) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); }
            Assert.AreEqual(0.5f, (minX + maxX) * 0.5f, 1e-4f);
        }
    }
}
