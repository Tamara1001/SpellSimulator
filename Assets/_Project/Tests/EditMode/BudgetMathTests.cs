using System.Collections.Generic;
using NUnit.Framework;
using SpellSimulator.World;

namespace SpellSimulator.Tests
{
    public class BudgetMathTests
    {
        private static BudgetMath.Entry E(float birth, string type, bool prot = false)
            => new BudgetMath.Entry { birthTime = birth, typeKey = type, isProtected = prot };

        private static BudgetMath.Entry Own(float birth, string type, int owner, bool prot = false)
            => new BudgetMath.Entry { birthTime = birth, typeKey = type, isProtected = prot, ownerId = owner };

        [Test]
        public void UnderLimits_EvictsNothing()
        {
            var entries = new List<BudgetMath.Entry> { E(1, "a"), E(2, "b"), E(3, "a") };
            var result = new List<int>();
            BudgetMath.SelectEvictions(entries, 40, 12, result);
            Assert.IsEmpty(result);
        }

        [Test]
        public void TotalOverflow_EvictsOldestFirst()
        {
            var entries = new List<BudgetMath.Entry> { E(5, "a"), E(1, "b"), E(3, "c"), E(2, "d") };
            var result = new List<int>();
            BudgetMath.SelectEvictions(entries, 2, 12, result);
            CollectionAssert.AreEquivalent(new[] { 1, 3 }, result);
        }

        [Test]
        public void PerTypeOverflow_OnlyTrimsThatType()
        {
            var entries = new List<BudgetMath.Entry> { E(1, "a"), E(2, "a"), E(3, "a"), E(0, "b") };
            var result = new List<int>();
            BudgetMath.SelectEvictions(entries, 40, 2, result);
            CollectionAssert.AreEqual(new[] { 0 }, result);
        }

        [Test]
        public void ProtectedEntries_AreNeverEvicted()
        {
            var entries = new List<BudgetMath.Entry> { E(1, "a", true), E(2, "a"), E(3, "a") };
            var result = new List<int>();
            BudgetMath.SelectEvictions(entries, 40, 1, result);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, result);

            entries = new List<BudgetMath.Entry> { E(1, "a", true), E(2, "a", true) };
            BudgetMath.SelectEvictions(entries, 1, 1, result);
            Assert.IsEmpty(result);
        }

        [Test]
        public void PerTypeEvictionsCountTowardsTotal()
        {
            // 3 de "a" (tope 2) + 2 de "b": sale la "a" mas vieja y el total (4) ya cumple el maximo de 4.
            var entries = new List<BudgetMath.Entry> { E(1, "a"), E(2, "a"), E(3, "a"), E(4, "b"), E(5, "b") };
            var result = new List<int>();
            BudgetMath.SelectEvictions(entries, 4, 2, result);
            CollectionAssert.AreEqual(new[] { 0 }, result);
        }

        [Test]
        public void TotalOverflow_SkipsProtectedAndTakesNextOldest()
        {
            var entries = new List<BudgetMath.Entry> { E(1, "a", true), E(2, "b"), E(3, "c") };
            var result = new List<int>();
            BudgetMath.SelectEvictions(entries, 2, 12, result);
            CollectionAssert.AreEqual(new[] { 1 }, result);
        }

        [Test]
        public void PerOwnerOverflow_TrimsOnlyThatOwner_OldestFirst()
        {
            // Dueno 1 tiene 3 (tope 2) en tipos distintos; dueno 2 tiene 1.
            var entries = new List<BudgetMath.Entry> { Own(3, "a", 1), Own(1, "b", 1), Own(2, "c", 1), Own(0, "d", 2) };
            var result = new List<int>();
            BudgetMath.SelectEvictions(entries, 40, 12, result, 2);
            CollectionAssert.AreEqual(new[] { 1 }, result);
        }

        [Test]
        public void PerOwnerOverflow_SkipsProtected()
        {
            var entries = new List<BudgetMath.Entry> { Own(1, "a", 1, true), Own(2, "b", 1), Own(3, "c", 1) };
            var result = new List<int>();
            BudgetMath.SelectEvictions(entries, 40, 12, result, 1);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, result);
        }

        [Test]
        public void PerOwner_ZeroMeansNoLimit()
        {
            var entries = new List<BudgetMath.Entry>();
            for (int i = 0; i < 20; i++) entries.Add(Own(i, "t" + i, 0));
            var result = new List<int>();
            BudgetMath.SelectEvictions(entries, 40, 12, result, 0);
            Assert.IsEmpty(result);
            BudgetMath.SelectEvictions(entries, 40, 12, result); // por defecto: 12 por dueno
            Assert.AreEqual(8, result.Count);
        }

        [Test]
        public void PerOwnerEvictions_CountTowardsTotal()
        {
            // 4 del dueno 1 (tope 2) + 1 del dueno 2: salen 2 por dueno y el total (3) ya cumple el maximo de 3.
            var entries = new List<BudgetMath.Entry> { Own(1, "a", 1), Own(2, "b", 1), Own(3, "c", 1), Own(4, "d", 1), Own(5, "e", 2) };
            var result = new List<int>();
            BudgetMath.SelectEvictions(entries, 3, 12, result, 2);
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, result);
        }
    }
}
