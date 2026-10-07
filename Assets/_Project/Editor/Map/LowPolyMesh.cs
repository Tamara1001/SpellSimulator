using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpellSimulator.EditorTools.Map
{
    /// <summary>
    /// Constructor de mallas low-poly: cada triangulo tiene sus propios vertices (normal plana) y un color. Las
    /// primitivas (prisma, cono, caja, anillo) se agregan con una matriz para ubicarlas.
    /// </summary>
    public class LowPolyMesh
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();

        public int TriangleCount => triangles.Count / 3;

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            int i = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            colors.Add(color); colors.Add(color); colors.Add(color);
            triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
        }

        /// <summary>Cuadrilatero a-b-c-d en sentido horario visto desde afuera.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            Tri(a, b, c, color);
            Tri(a, c, d, color);
        }

        /// <summary>Caja centrada en el origen local con tamaño size, transformada por m.</summary>
        public void Box(Matrix4x4 m, Vector3 size, Color color, Color? top = null)
        {
            Vector3 h = size * 0.5f;
            Vector3 P(float x, float y, float z) => m.MultiplyPoint3x4(new Vector3(x * h.x, y * h.y, z * h.z));
            Color t = top ?? color;
            Quad(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), t);       // arriba
            Quad(P(-1, -1, 1), P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), color); // abajo
            Quad(P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1), color); // -z
            Quad(P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), P(-1, -1, 1), color);    // +z
            Quad(P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), P(-1, -1, -1), color); // -x
            Quad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), color);    // +x
        }

        /// <summary>Tronco de cono (prisma si r0 == r1) sobre el eje Y, de y0 a y1, con n lados.</summary>
        public void Frustum(Matrix4x4 m, float r0, float r1, float y0, float y1, int sides, Color color, bool capTop = true, bool capBottom = false)
        {
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                Vector3 b0 = m.MultiplyPoint3x4(new Vector3(Mathf.Cos(a0) * r0, y0, Mathf.Sin(a0) * r0));
                Vector3 b1 = m.MultiplyPoint3x4(new Vector3(Mathf.Cos(a1) * r0, y0, Mathf.Sin(a1) * r0));
                Vector3 t0 = m.MultiplyPoint3x4(new Vector3(Mathf.Cos(a0) * r1, y1, Mathf.Sin(a0) * r1));
                Vector3 t1 = m.MultiplyPoint3x4(new Vector3(Mathf.Cos(a1) * r1, y1, Mathf.Sin(a1) * r1));
                if (r1 > 1e-4f) Quad(b0, t0, t1, b1, color);
                else Tri(b0, t0, b1, color);
                if (capTop && r1 > 1e-4f) Tri(m.MultiplyPoint3x4(new Vector3(0, y1, 0)), t1, t0, color);
                if (capBottom) Tri(m.MultiplyPoint3x4(new Vector3(0, y0, 0)), b0, b1, color);
            }
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Variacion de color determinista (para que cada cara no sea identica).</summary>
        public static Color Jitter(Color c, float amount, int seed)
        {
            float k = 1f + (Mathf.PerlinNoise(seed * 0.173f, seed * 0.311f) - 0.5f) * 2f * amount;
            return new Color(c.r * k, c.g * k, c.b * k, c.a);
        }
    }
}
