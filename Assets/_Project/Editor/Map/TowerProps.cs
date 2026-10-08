using UnityEditor;
using UnityEngine;
using SpellSimulator.World;

namespace SpellSimulator.EditorTools.Map
{
    /// <summary>
    /// Mas cosas dentro de la torre para experimentar: mesas de trabajo con herramientas (plumas, pinceles, gomas,
    /// trapos y tinteros) en la biblioteca y en el estudio, un taller de alquimia en el segundo piso (mesa con
    /// probetas y frascos, atril con libro, sillas, estante, baules, cajas y alfombra), estampas extra y cajas movibles.
    /// Las herramientas se clonan de las del campo (ya con el modelo nuevo) para heredar su configuracion.
    /// </summary>
    public static class TowerProps
    {
        private static readonly Color WoodDark = new Color(0.36f, 0.22f, 0.15f);
        private static readonly Color WoodLight = new Color(0.45f, 0.3f, 0.2f);
        private static readonly Color Glass = new Color(0.85f, 0.92f, 0.95f);
        private static readonly Color Cork = new Color(0.6f, 0.42f, 0.25f);
        private static readonly Color Paper = new Color(0.96f, 0.92f, 0.8f);

        /// <summary>Mesa de la torre: centro local (respecto del origen), yaw, ancho (X local) y fondo (Z local).</summary>
        private readonly struct Desk
        {
            public readonly Vector3 Center;
            public readonly float Yaw;
            public readonly float Width;
            public readonly float Depth;

            public Desk(Vector3 center, float yaw, float width, float depth)
            {
                Center = center;
                Yaw = yaw;
                Width = width;
                Depth = depth;
            }
        }

        public static void Build(Transform map, Material material, Vector3 origin)
        {
            var tower = GameObject.Find("Tower");
            if (tower == null) return;
            float floor = TowerBuilder.FloorHeight;

            // Mesa de trabajo de la biblioteca (planta baja): herramientas en dos filas dentro de la cubierta.
            var library = new Desk(new Vector3(0.4f, 0f, -2.35f), 0f, 2.4f, 0.8f);
            TowerBuilder.Table(tower.transform, material, "Workbench_Library", library.Center, library.Yaw, library.Width, library.Depth);
            Tools(origin, library, new[] { "Pen", "Pen", "Brush", "Brush", "Eraser", "Rag", "Inkwell", "Inkwell" }, 0.25f);

            // Estudio (primer piso): otra mesa con herramientas y tinta.
            var study = new Desk(new Vector3(-0.5f, floor, 2.1f), 170f, 1.8f, 0.7f);
            TowerBuilder.Table(tower.transform, material, "Workbench_Study", study.Center, study.Yaw, study.Width, study.Depth);
            Tools(origin, study, new[] { "Pen", "Brush", "Eraser", "Rag", "Inkwell" }, 0.3f);

            // Segundo piso: taller de alquimia.
            Alchemy(map, tower.transform, material, origin);

            // Cajas y baules movibles (se pueden cargar, apilar y quemar).
            Crate(map, material, "Chest_Library_A", origin + new Vector3(-1.3f, 0.2f, 2.0f), new Vector3(0.7f, 0.4f, 0.45f), 15f);
            Crate(map, material, "Crate_Library_A", origin + new Vector3(-0.7f, 0.25f, 2.3f), Vector3.one * 0.5f, -10f);
            Crate(map, material, "Crate_Library_B", origin + new Vector3(-0.7f, 0.76f, 2.3f), Vector3.one * 0.4f, 20f);
            Crate(map, material, "Chest_Study_A", origin + new Vector3(1.4f, floor + 0.2f, -1.6f), new Vector3(0.7f, 0.4f, 0.45f), 160f);
            Crate(map, material, "Crate_Study_A", origin + new Vector3(1.9f, floor + 0.25f, -1.0f), Vector3.one * 0.5f, 5f);
        }

        /// <summary>
        /// Clona herramientas del campo sobre una mesa, en dos filas ordenadas y centradas. Las posiciones se calculan
        /// en el sistema local de la mesa (girado por su yaw) y quedan dentro de la cubierta: en X, +-(ancho/2 - 0.1);
        /// en Z, +-(fondo/2 - 0.08). Plumas y pinceles van echados; los tinteros, parados.
        /// </summary>
        private static void Tools(Vector3 origin, Desk desk, string[] templates, float spacing)
        {
            Vector3 deskAt = origin + desk.Center;
            float halfX = desk.Width / 2f - 0.1f;
            float halfZ = desk.Depth / 2f - 0.08f;
            int columns = Mathf.CeilToInt(templates.Length / 2f);
            float step = Mathf.Min(spacing, 2f * halfX / columns);
            Quaternion deskRotation = Quaternion.Euler(0f, desk.Yaw, 0f);
            for (int index = 0; index < templates.Length; index++)
            {
                string template = templates[index];
                var source = GameObject.Find(template);
                if (source == null) continue;
                string name = $"{template}_{(int)((deskAt.y + 0.84f) * 100)}_{index}";
                var old = GameObject.Find(name);
                if (old != null) Object.DestroyImmediate(old);
                var clone = Object.Instantiate(source);
                clone.name = name;
                // Fila 0 (lado de atras) y fila 1 (lado de adelante); columnas centradas sobre la mesa.
                int row = index & 1, column = index >> 1;
                float x = (column - (columns - 1) * 0.5f) * step;
                float z = (row == 0 ? -0.5f : 0.5f) * halfZ;
                // Plumas, pinceles y demas quedan echados; los tinteros, parados (sin inclinacion en X ni Z).
                bool upright = template == "Inkwell";
                clone.transform.SetPositionAndRotation(
                    deskAt + deskRotation * new Vector3(x, 0f, z) + new Vector3(0f, 0.84f + (upright ? 0.01f : 0.04f), 0f),
                    Quaternion.Euler(0f, desk.Yaw + 90f + index * 15f, upright ? 0f : 90f));
                var respawn = clone.GetComponent<Respawnable>();
                if (respawn != null) Object.DestroyImmediate(respawn);
                clone.AddComponent<Respawnable>();
                Undo.RegisterCreatedObjectUndo(clone, name);
            }
        }

        /// <summary>
        /// Taller de alquimia en el segundo piso (y = 2 * FloorHeight). Todo cae en el area central libre (radio menor
        /// que 2.6 desde el eje): la rampa y el hueco de la escalera viven en el anillo exterior (radio desde 2.9).
        /// </summary>
        private static void Alchemy(Transform map, Transform tower, Material material, Vector3 origin)
        {
            float y = TowerBuilder.FloorHeight * 2f;
            float top = y + 0.82f;   // cubierta de la mesa (ver TowerBuilder.Table)

            // Mesa grande, dos sillas enfrentadas (norte y sur) y estante exento contra el oeste (libros hacia el centro).
            TowerBuilder.Table(tower, material, "AlchemyTable", new Vector3(0f, y, 0f), 0f, 2.2f, 1f);
            TowerBuilder.Chair(tower, material, new Vector3(0f, y, 1f), 180f);
            TowerBuilder.Chair(tower, material, new Vector3(0f, y, -1f), 0f);
            TowerBuilder.Shelf(tower, material, 180f, y, 2.15f);

            // Baules y cajas movibles; la caja apilada queda sobre el segundo baul.
            Crate(map, material, "Chest_Tower2_A", origin + new Vector3(-1.2f, y + 0.2f, -1.6f), new Vector3(0.7f, 0.4f, 0.45f), 30f);
            Crate(map, material, "Chest_Tower2_B", origin + new Vector3(1.5f, y + 0.2f, -1f), new Vector3(0.7f, 0.4f, 0.45f), -20f);
            Crate(map, material, "Crate_Tower2_B", origin + new Vector3(1.5f, y + 0.6f, -1f), Vector3.one * 0.4f, 20f);
            Crate(map, material, "Crate_Tower2_A", origin + new Vector3(1.5f, y + 0.25f, 0.3f), Vector3.one * 0.5f, -10f);

            var b = new LowPolyMesh();

            // Alfombra circular chata (3 cm) bajo la mesa y las sillas.
            b.Frustum(Matrix4x4.Translate(new Vector3(0f, y + 0.01f, 0f)), 2.1f, 2.1f, 0f, 0.03f, 24, new Color(0.5f, 0.22f, 0.18f));

            // Atril con libro abierto (decorativo), en el lado noreste.
            var lectern = Matrix4x4.TRS(new Vector3(0.95f, y, 1.65f), Quaternion.Euler(0f, 240f, 0f), Vector3.one);
            b.Frustum(lectern, 0.25f, 0.12f, 0f, 0.1f, 6, WoodDark);
            b.Box(lectern * Matrix4x4.Translate(new Vector3(0f, 0.6f, 0f)), new Vector3(0.1f, 1.1f, 0.1f), WoodDark);
            var desk = lectern * Matrix4x4.TRS(new Vector3(0f, 1.15f, 0f), Quaternion.Euler(-25f, 0f, 0f), Vector3.one);
            b.Box(desk, new Vector3(0.6f, 0.04f, 0.45f), WoodLight);
            b.Box(desk * Matrix4x4.TRS(new Vector3(-0.14f, 0.04f, 0f), Quaternion.Euler(0f, 0f, 6f), Vector3.one), new Vector3(0.26f, 0.025f, 0.36f), Paper);
            b.Box(desk * Matrix4x4.TRS(new Vector3(0.14f, 0.04f, 0f), Quaternion.Euler(0f, 0f, -6f), Vector3.one), new Vector3(0.26f, 0.025f, 0.36f), Paper);

            // Matraces y frascos de colores sobre la mesa.
            Flask(b, new Vector3(-0.7f, top, 0.15f), 0.075f, new Color(0.25f, 0.7f, 0.35f));
            Flask(b, new Vector3(-0.42f, top, -0.2f), 0.09f, new Color(0.3f, 0.45f, 0.9f));
            Flask(b, new Vector3(-0.1f, top, 0.22f), 0.06f, new Color(0.95f, 0.75f, 0.2f));
            Flask(b, new Vector3(0.28f, top, 0.05f), 0.07f, new Color(0.85f, 0.25f, 0.2f));

            // Gradilla con cuatro probetas y un vaso de precipitados.
            b.Box(Matrix4x4.Translate(new Vector3(0.6f, top + 0.02f, -0.3f)), new Vector3(0.42f, 0.04f, 0.12f), WoodDark);
            Color[] tubes = { new Color(0.85f, 0.25f, 0.2f), new Color(0.95f, 0.75f, 0.2f), new Color(0.25f, 0.7f, 0.35f), new Color(0.3f, 0.45f, 0.9f) };
            for (int i = 0; i < tubes.Length; i++)
                b.Frustum(Matrix4x4.Translate(new Vector3(0.47f + i * 0.09f, top + 0.04f, -0.3f)), 0.02f, 0.02f, 0f, 0.14f, 8, tubes[i], true, true);
            b.Frustum(Matrix4x4.Translate(new Vector3(-0.1f, top, -0.3f)), 0.045f, 0.045f, 0f, 0.1f, 10, new Color(0.4f, 0.8f, 0.9f), true, true);

            // Un solo mesh decorativo en coordenadas locales de la torre (el origen es la posicion de la torre).
            var mesh = MapBuilder.SaveMesh(b.Build("TowerAlchemy"));
            MapBuilder.Place("TowerAlchemy", tower, mesh, material, origin, Quaternion.identity).isStatic = true;
        }

        /// <summary>Matraz de liquido de color: esfera, cuello de vidrio y tapon.</summary>
        private static void Flask(LowPolyMesh b, Vector3 at, float radius, Color liquid)
        {
            Sphere(b, at + Vector3.up * radius, radius, liquid);
            b.Frustum(Matrix4x4.Translate(at + Vector3.up * (radius * 1.8f)), radius * 0.3f, radius * 0.3f, 0f, radius * 1.2f, 8, Glass);
            b.Frustum(Matrix4x4.Translate(at + Vector3.up * (radius * 3f)), radius * 0.35f, radius * 0.35f, 0f, radius * 0.25f, 8, Cork);
        }

        /// <summary>Esfera low-poly (4 franjas, 8 meridianos) con las caras hacia afuera.</summary>
        private static void Sphere(LowPolyMesh b, Vector3 center, float radius, Color color)
        {
            const int lat = 4, lon = 8;
            Vector3 P(int i, int j)
            {
                float theta = Mathf.PI * i / lat, phi = 2f * Mathf.PI * j / lon;
                return center + new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi)) * radius;
            }
            for (int i = 0; i < lat; i++)
                for (int j = 0; j < lon; j++)
                    b.Quad(P(i, j), P(i, j + 1), P(i + 1, j + 1), P(i + 1, j), color);
        }

        /// <summary>Caja de madera movible (copia de las del campo de practica: perfil de material, audio y reacciones).</summary>
        private static void Crate(Transform map, Material material, string name, Vector3 position, Vector3 scale, float yaw)
        {
            var source = GameObject.Find("Crate_0");
            if (source == null) return;
            var old = GameObject.Find(name);
            if (old != null) Object.DestroyImmediate(old);
            var clone = Object.Instantiate(source);
            clone.name = name;
            clone.transform.SetParent(map, true);
            clone.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            clone.transform.localScale = scale;
            var rb = clone.GetComponent<Rigidbody>();
            if (rb != null) rb.mass = Mathf.Max(0.5f, scale.x * scale.y * scale.z * 16f);
            var respawn = clone.GetComponent<Respawnable>();
            if (respawn != null) Object.DestroyImmediate(respawn);
            clone.AddComponent<Respawnable>();
            Undo.RegisterCreatedObjectUndo(clone, name);
        }
    }
}
