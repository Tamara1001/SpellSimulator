using UnityEditor;
using UnityEngine;

namespace SpellSimulator.EditorTools.Map
{
    /// <summary>
    /// Cinturon de cuero (bloque 5 de la octava tanda): una tira marron enrollada en espiral sobre una mesa, con la punta
    /// suelta que termina en una hebilla de laton y dos bolsitas cosidas por fuera. Mide ~0.35 m de lado. Se apoya en la
    /// mesa de alquimia del segundo piso de la torre: a la vista, pero hay que subir a buscarlo. Idempotente: borra el
    /// anterior por nombre. Sin luz ni brillo propio (es un objeto comun, no magico).
    /// </summary>
    public static class MapBelt
    {
        public const string ObjectName = "Belt";

        /// <summary>
        /// Lugar libre en la cubierta de la mesa de alquimia (local a la mesa, pivote en el piso): extremo +X, lado +Z,
        /// lejos de los frascos (x &lt;= 0.35) y del portatubos (x 0.4-0.8, z -0.3). Ver TowerProps.Alchemy.
        /// </summary>
        private static readonly Vector3 AlchemyTableLocalSpot = new Vector3(0.8f, 0.822f, 0.2f);
        private const string AlchemyTableName = "AlchemyTable";
        private const float AlchemyTableYaw = 0f;
        private const float YawOnTable = -35f;

        // Forma de la tira enrollada.
        private const float StrapHeight = 0.042f, StrapThickness = 0.009f;
        private const float CoilInner = 0.045f, CoilOuter = 0.12f, CoilTurns = 1.75f;
        private const int SegmentsPerTurn = 16;
        private const float TailLength = 0.11f;

        private static readonly Color Leather = new Color(0.47f, 0.28f, 0.15f);
        private static readonly Color LeatherTop = new Color(0.58f, 0.37f, 0.21f);
        private static readonly Color LeatherDark = new Color(0.33f, 0.19f, 0.1f);
        private static readonly Color PouchLeather = new Color(0.55f, 0.36f, 0.2f);
        private static readonly Color Stitch = new Color(0.86f, 0.78f, 0.6f);
        private static readonly Color Brass = new Color(0.84f, 0.64f, 0.24f);
        private static readonly Color BrassTop = new Color(0.96f, 0.8f, 0.38f);

        /// <summary>Punto de apoyo del cinturon (cubierta de la mesa de alquimia; si no existe, la misma cuenta desde la torre).</summary>
        public static Vector3 AlchemyTableSpot()
        {
            var table = GameObject.Find(AlchemyTableName);
            if (table != null) return table.transform.TransformPoint(AlchemyTableLocalSpot);
            return MapBuilder.TowerOrigin() + new Vector3(0f, TowerBuilder.FloorHeight * 2f, 0f)
                + Quaternion.Euler(0f, AlchemyTableYaw, 0f) * AlchemyTableLocalSpot;
        }

        public static void Build(Transform map, Material material, Vector3 origin)
        {
            var old = GameObject.Find(ObjectName);
            if (old != null) Object.DestroyImmediate(old);

            var b = new LowPolyMesh();
            Vector3 tailEnd, tailDir;
            Coil(b, out tailEnd, out tailDir);
            Buckle(b, tailEnd, tailDir);
            // Dos bolsitas por fuera de la vuelta exterior, separadas para que se lean como dos.
            Pouch(b, 0.95f * Mathf.PI, 0);
            Pouch(b, 1.55f * Mathf.PI, 1);

            Mesh mesh = MapBuilder.SaveMesh(b.Build("Belt"));
            float yaw = YawOnTable;
            var table = GameObject.Find(AlchemyTableName);
            if (table != null) yaw += table.transform.eulerAngles.y;
            var go = MapBuilder.Place(ObjectName, map, mesh, material, origin, Quaternion.Euler(0f, yaw, 0f));

            // Collider un poco mas alto que la tira para que sea facil de apuntar.
            Bounds bounds = mesh.bounds;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(bounds.center.x, Mathf.Max(bounds.extents.y, 0.04f), bounds.center.z);
            box.size = new Vector3(bounds.size.x, Mathf.Max(bounds.size.y, 0.08f), bounds.size.z);

            go.AddComponent<SpellSimulator.World.Belt>();
            Undo.RegisterCreatedObjectUndo(go, ObjectName);
        }

        // ---------------------------------------------------------------- piezas

        private static float CoilRadius(float angle) => CoilInner + (CoilOuter - CoilInner) * angle / (CoilTurns * Mathf.PI * 2f);

        private static Vector3 CoilPoint(float angle, float y)
        {
            float r = CoilRadius(angle);
            return new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);
        }

        /// <summary>Espiral de tramos rectos de canto (la tira parada) y la punta recta que sale de la ultima vuelta.</summary>
        private static void Coil(LowPolyMesh b, out Vector3 tailEnd, out Vector3 tailDir)
        {
            int segments = Mathf.CeilToInt(SegmentsPerTurn * CoilTurns);
            float total = CoilTurns * Mathf.PI * 2f;
            float y = StrapHeight * 0.5f;
            Vector3 prev = CoilPoint(0f, y);
            for (int i = 1; i <= segments; i++)
            {
                Vector3 next = CoilPoint(total * i / segments, y);
                Strap(b, prev, next, LowPolyMesh.Jitter(Leather, 0.06f, i * 13), LowPolyMesh.Jitter(LeatherTop, 0.05f, i * 7));
                prev = next;
            }
            // La punta sigue la tangente de la ultima vuelta, con los agujeros de la hebilla.
            Vector3 before = CoilPoint(total * (segments - 1) / segments, y);
            tailDir = (prev - before).normalized;
            tailEnd = prev + tailDir * TailLength;
            Strap(b, prev, tailEnd, Leather, LeatherTop);
            Vector3 outward = Vector3.Cross(Vector3.up, tailDir).normalized;
            for (int k = 0; k < 3; k++)
            {
                Vector3 hole = prev + tailDir * (0.03f + k * 0.022f) + outward * (StrapThickness * 0.5f + 0.0005f);
                b.Box(Matrix4x4.TRS(hole, Quaternion.LookRotation(tailDir), Vector3.one), new Vector3(0.002f, 0.009f, 0.007f), LeatherDark);
            }
            // Costura clara a lo largo de la punta (detalle que la hace leer como cuero).
            Vector3 seamFrom = prev + outward * (StrapThickness * 0.5f + 0.0005f) + Vector3.up * (StrapHeight * 0.32f);
            b.Box(Matrix4x4.TRS(seamFrom + tailDir * 0.012f, Quaternion.LookRotation(tailDir), Vector3.one), new Vector3(0.0015f, 0.003f, 0.02f), Stitch);
        }

        private static void Strap(LowPolyMesh b, Vector3 from, Vector3 to, Color side, Color top)
        {
            Vector3 d = to - from;
            float length = d.magnitude;
            if (length < 1e-4f) return;
            var m = Matrix4x4.TRS((from + to) * 0.5f, Quaternion.LookRotation(d / length, Vector3.up), Vector3.one);
            // Un pelito mas largo para tapar las juntas entre tramos.
            b.Box(m, new Vector3(StrapThickness, StrapHeight, length + 0.004f), side, top);
        }

        /// <summary>Hebilla de laton: marco rectangular parado en la punta, con su pasador.</summary>
        private static void Buckle(LowPolyMesh b, Vector3 at, Vector3 dir)
        {
            const float length = 0.046f, height = 0.06f, bar = 0.008f, depth = 0.016f;
            // "at" ya esta a media altura de la tira: el marco queda centrado en ella.
            Matrix4x4 m = Matrix4x4.TRS(at + dir * (length * 0.5f - bar * 0.5f), Quaternion.LookRotation(dir, Vector3.up), Vector3.one);
            Matrix4x4 Local(float y, float z) => m * Matrix4x4.Translate(new Vector3(0f, y, z));
            b.Box(Local(height * 0.5f - bar * 0.5f, 0f), new Vector3(depth, bar, length), Brass, BrassTop);
            b.Box(Local(-height * 0.5f + bar * 0.5f, 0f), new Vector3(depth, bar, length), Brass, BrassTop);
            b.Box(Local(0f, length * 0.5f - bar * 0.5f), new Vector3(depth, height, bar), Brass, BrassTop);
            b.Box(Local(0f, -length * 0.5f + bar * 0.5f), new Vector3(depth, height, bar), Brass, BrassTop);
            // Pasador: barrita que cruza el marco.
            b.Box(Local(0f, 0f), new Vector3(0.006f, 0.006f, length - bar), BrassTop);
        }

        /// <summary>Bolsita cosida por fuera de la ultima vuelta: cuerpo, tapa caida y un boton de laton.</summary>
        private static void Pouch(LowPolyMesh b, float turnAngle, int seed)
        {
            // Angulo sobre la vuelta exterior (la ultima vuelta completa empieza en (CoilTurns - 1) * 2pi).
            float angle = Mathf.Min((CoilTurns - 1f) * Mathf.PI * 2f + turnAngle, CoilTurns * Mathf.PI * 2f - 0.2f);
            Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            const float width = 0.075f, height = 0.062f, depth = 0.038f;
            Vector3 center = outward * (CoilRadius(angle) + StrapThickness * 0.5f + depth * 0.5f) + Vector3.up * (height * 0.5f);
            Matrix4x4 m = Matrix4x4.TRS(center, Quaternion.LookRotation(outward, Vector3.up), Vector3.one);
            Color body = LowPolyMesh.Jitter(PouchLeather, 0.05f, 31 + seed * 17);

            b.Box(m, new Vector3(width, height, depth), body, LeatherTop);
            // Tapa: plano superior que asoma y cae por el frente.
            b.Box(m * Matrix4x4.Translate(new Vector3(0f, height * 0.5f + 0.004f, 0.002f)), new Vector3(width + 0.006f, 0.008f, depth + 0.006f), LeatherDark);
            b.Box(m * Matrix4x4.Translate(new Vector3(0f, height * 0.5f - 0.012f, depth * 0.5f + 0.004f)), new Vector3(width + 0.006f, 0.03f, 0.007f), LeatherDark);
            // Boton de laton y costura del frente.
            b.Box(m * Matrix4x4.Translate(new Vector3(0f, height * 0.5f - 0.024f, depth * 0.5f + 0.009f)), new Vector3(0.012f, 0.012f, 0.006f), Brass, BrassTop);
            b.Box(m * Matrix4x4.Translate(new Vector3(0f, -height * 0.5f + 0.008f, depth * 0.5f + 0.0008f)), new Vector3(width - 0.014f, 0.002f, 0.001f), Stitch);
        }
    }
}
