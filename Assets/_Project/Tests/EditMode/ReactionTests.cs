using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Compiler;
using SpellSimulator.Reactions;

namespace SpellSimulator.Tests
{
    /// <summary>Reglas del mundo reactivo (ReactionModel) y matriz elemento x material (ReactionTable).</summary>
    public class ReactionTests
    {
        private const float Dt = 1f / MagicWorld.TickRate;

        private static ReactionEvents Run(ref ReactiveState s, MaterialParams m, Exposure e, float seconds)
        {
            var all = ReactionEvents.None;
            for (float t = 0f; t < seconds; t += Dt) all |= ReactionModel.Step(ref s, m, e, Dt);
            return all;
        }

        private static Exposure Fire => new Exposure { Heat = 3f };
        private static Exposure Water => new Exposure { Heat = -2f, Wet = 1.2f };

        [Test]
        public void Wood_IgnitesUnderFire()
        {
            var s = ReactiveState.Fresh;
            var events = Run(ref s, MaterialParams.Wood, Fire, 1f);
            Assert.IsTrue((events & ReactionEvents.Ignited) != 0);
            Assert.IsTrue(s.Burning);
        }

        [Test]
        public void Stone_NeverIgnites()
        {
            var s = ReactiveState.Fresh;
            Run(ref s, MaterialParams.Stone, Fire, 5f);
            Assert.IsFalse(s.Burning);
        }

        [Test]
        public void Fire_KeepsBurningWithoutSource_UntilConsumed()
        {
            var s = ReactiveState.Fresh;
            Run(ref s, MaterialParams.Wood, Fire, 1f);
            var events = Run(ref s, MaterialParams.Wood, default, 13f);
            Assert.IsTrue((events & ReactionEvents.BurnedOut) != 0);
            Assert.IsTrue((events & ReactionEvents.Broken) != 0, "La madera consumida se rompe.");
            Assert.AreEqual(1f, s.BurnProgress);
        }

        [Test]
        public void Water_Extinguishes()
        {
            var s = ReactiveState.Fresh;
            Run(ref s, MaterialParams.Wood, Fire, 1f);
            var events = Run(ref s, MaterialParams.Wood, Water, 1f);
            Assert.IsTrue((events & ReactionEvents.Extinguished) != 0);
            Assert.IsFalse(s.Burning);
        }

        [Test]
        public void Water_ExtinguishesALongFire_StillUnderFire()
        {
            // Prueba en Play Mode 2026-10-07: tras 10 s ardiendo bajo el chorro, el calor llegaba a 10 y el agua se
            // evaporaba antes de apagarlo.
            var s = ReactiveState.Fresh;
            Run(ref s, MaterialParams.Wood, Fire, 8f);
            var both = Fire;
            both.Add(Water, 1f);
            var events = Run(ref s, MaterialParams.Wood, both, 2f);
            Assert.IsTrue((events & ReactionEvents.Extinguished) != 0);
            Assert.LessOrEqual(s.Heat, ReactionModel.MaxHeatFactor);
        }

        [Test]
        public void WetWood_DoesNotIgnite_UntilItDries()
        {
            var s = ReactiveState.Fresh;
            Run(ref s, MaterialParams.Wood, Water, 1f);
            Run(ref s, MaterialParams.Wood, Fire, 0.5f);
            Assert.IsFalse(s.Burning, "Recien mojada no prende.");
            Run(ref s, MaterialParams.Wood, Fire, 6f);
            Assert.IsTrue(s.Burning, "El fuego la seca y termina prendiendo.");
        }

        [Test]
        public void Wind_FansTheFire()
        {
            var calm = ReactiveState.Fresh;
            var windy = ReactiveState.Fresh;
            Run(ref calm, MaterialParams.Wood, Fire, 1f);
            Run(ref windy, MaterialParams.Wood, Fire, 1f);
            Run(ref calm, MaterialParams.Wood, default, 3f);
            Run(ref windy, MaterialParams.Wood, new Exposure { Fan = 1f }, 3f);
            Assert.Greater(windy.BurnProgress, calm.BurnProgress * 1.5f);
        }

        [Test]
        public void Wind_CoolsWhatIsNotBurning()
        {
            var calm = new ReactiveState { Integrity = 1f, Heat = 0.8f };
            var windy = calm;
            Run(ref calm, MaterialParams.Wood, default, 1f);
            Run(ref windy, MaterialParams.Wood, new Exposure { Fan = 1f }, 1f);
            Assert.Less(windy.Heat, calm.Heat);
        }

        [Test]
        public void Earth_BreaksWood_ButStoneLasts()
        {
            var wood = ReactiveState.Fresh;
            var stone = ReactiveState.Fresh;
            var earth = new Exposure { Impact = 1.2f };
            var woodEvents = Run(ref wood, MaterialParams.Wood, earth, 1f);
            Run(ref stone, MaterialParams.Stone, earth, 1f);
            Assert.IsTrue((woodEvents & ReactionEvents.Broken) != 0);
            Assert.IsFalse(stone.Broken);
        }

        [Test]
        public void Soaked_FiresOnce()
        {
            var s = ReactiveState.Fresh;
            var events = Run(ref s, MaterialParams.Wood, Water, 2f);
            Assert.IsTrue((events & ReactionEvents.Soaked) != 0);
            events = Run(ref s, MaterialParams.Wood, Water, 1f);
            Assert.IsFalse((events & ReactionEvents.Soaked) != 0);
        }

        [Test]
        public void ItDries_OverTime()
        {
            var s = new ReactiveState { Integrity = 1f, Wetness = 1f };
            Run(ref s, MaterialParams.Wood, default, 10f);
            Assert.Less(s.Wetness, 0.7f);
        }

        [Test]
        public void Table_ScalesByMaterialOverride()
        {
            var table = ScriptableObject.CreateInstance<ReactionTable>();
            var paper = ScriptableObject.CreateInstance<MaterialProfile>();
            table.overrides.Add(new ReactionTable.MaterialOverride { material = paper, element = ElementType.Water, multiplier = 2f });

            Assert.AreEqual(3f, table.For(ElementType.Fire, paper).Heat);
            Assert.AreEqual(2.4f, table.For(ElementType.Water, paper).Wet, 1e-5f);
            Assert.AreEqual(1.2f, table.For(ElementType.Water, null).Wet, 1e-5f);

            Object.DestroyImmediate(table);
            Object.DestroyImmediate(paper);
        }
    }
}
