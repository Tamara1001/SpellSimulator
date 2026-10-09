using System.Collections.Generic;
using NUnit.Framework;
using SpellSimulator.World;

namespace SpellSimulator.Tests
{
    public class BudgetMathTests
    {
        private static BudgetMath.Entry E(float birth, string type, bool prot = false)
            => new BudgetMath.Entry { birthTime = birth, typeKey = type, isProtected = prot };

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
    }
}
