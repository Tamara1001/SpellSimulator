using NUnit.Framework;
using UnityEngine;
using SpellSimulator.World.Map;

namespace SpellSimulator.Tests
{
    /// <summary>Postes con flechas del paso 9 (D31): fuera de los caminos, una palabra por flecha y apuntando a su destino.</summary>
    public class SignpostsTests
    {
        [Test]
        public void Yaw_PointsAlongTheCompass()
        {
            Assert.AreEqual(0f, Signposts.Yaw(Vector2.zero, new Vector2(0f, 5f)), 1e-4f);
            Assert.AreEqual(90f, Signposts.Yaw(Vector2.zero, new Vector2(5f, 0f)), 1e-4f);
            Assert.AreEqual(180f, Mathf.Abs(Signposts.Yaw(Vector2.zero, new Vector2(0f, -5f))), 1e-4f);
            Assert.AreEqual(-90f, Signposts.Yaw(Vector2.zero, new Vector2(-5f, 0f)), 1e-4f);
        }

        [Test]
        public void Posts_AreOffThePaths_AndArrowsAreOneWordWithDistinctTargets()
        {
            Assert.AreEqual(2, Signposts.All.Length);
            foreach (Signposts.Post post in Signposts.All)
            {
                Assert.GreaterOrEqual(ValleyShape.PathDistance(post.Position.x, post.Position.y), Signposts.PathClearance, $"{post.Id} pisa un camino");
                Assert.GreaterOrEqual(post.Arrows.Length, 3, post.Id);
                for (int i = 0; i < post.Arrows.Length; i++)
                {
                    Signposts.Arrow a = post.Arrows[i];
                    Assert.IsFalse(string.IsNullOrWhiteSpace(a.Word) || a.Word.Contains(" "), $"{post.Id}: «{a.Word}» tiene que ser una palabra");
                    Assert.Greater(Vector2.Distance(post.Position, a.Target), 5f, $"{post.Id}: {a.Word} apunta a su propio poste");
                    for (int j = i + 1; j < post.Arrows.Length; j++)
                        Assert.AreNotEqual(a.Word, post.Arrows[j].Word, post.Id);
                }
            }
        }

        [Test]
        public void ArchivePost_IsInsideTheRotunda_AndPointsToTheFourFields()
        {
            Signposts.Post archive = System.Array.Find(Signposts.All, p => p.Id == "Archive");
            Assert.Less(Vector2.Distance(archive.Position, ValleyShape.ArchiveCenter), 8f);
            foreach (string word in new[] { "Agua", "Fuego", "Tierra", "Viento" })
                Assert.IsTrue(System.Array.Exists(archive.Arrows, a => a.Word == word), "falta " + word);
        }

        [Test]
        public void TextOn_ContrastsWithTheBoard()
        {
            Assert.Less(Signposts.TextOn(new Color(0.9f, 0.85f, 0.6f)).r, 0.5f);
            Assert.Greater(Signposts.TextOn(new Color(0.2f, 0.3f, 0.5f)).r, 0.5f);
        }
    }
}
