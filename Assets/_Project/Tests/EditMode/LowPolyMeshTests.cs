using NUnit.Framework;
using UnityEngine;
using SpellSimulator.Output;

namespace SpellSimulator.Tests
{
    /// <summary>Mallas procedurales de tierra: caben en el cubo unitario, caras planas hacia afuera, deterministas.</summary>
    public class LowPolyMeshTests
    {
        private static void AssertWellFormed(Mesh mesh)
        {
            var verts = mesh.vertices;
            var normals = mesh.normals;
            var tris = mesh.triangles;
            Assert.Greater(tris.Length, 0);
            Assert.AreEqual(0, tris.Length % 3);
            Assert.AreEqual(verts.Length, normals.Length);
            foreach (var v in verts)
            {
                Assert.LessOrEqual(Mathf.Abs(v.x), 0.5f + 1e-4f);
                Assert.LessOrEqual(Mathf.Abs(v.y), 0.5f + 1e-4f);
                Assert.LessOrEqual(Mathf.Abs(v.z), 0.5f + 1e-4f);
            }
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                Assert.Greater(Vector3.Dot(n, normals[tris[i]]), 0.99f, "La normal plana debe coincidir con el orden del triangulo.");
            }
        }

        [Test]
        public void Shard_IsWellFormed() => AssertWellFormed(LowPolyMeshes.Shard(7));

        [Test]
        public void Stake_IsWellFormedAndSpansHeight()
        {
            var mesh = LowPolyMeshes.Stake(17);
            AssertWellFormed(mesh);
            Assert.AreEqual(-0.5f, mesh.bounds.min.y, 1e-4f);
            Assert.AreEqual(0.5f, mesh.bounds.max.y, 1e-4f);
        }

        [Test]
        public void SameSeed_SameMesh()
        {
            Assert.AreEqual(LowPolyMeshes.Shard(3).vertices, LowPolyMeshes.Shard(3).vertices);
        }
    }
}
