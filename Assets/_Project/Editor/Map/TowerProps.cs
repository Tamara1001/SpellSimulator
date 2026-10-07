using UnityEditor;
using UnityEngine;
using SpellSimulator.World;

namespace SpellSimulator.EditorTools.Map
{
    /// <summary>
    /// Mas cosas dentro de la torre para experimentar: una mesa de trabajo con herramientas (plumas, pinceles, gomas,
    /// trapos y tinteros) en la biblioteca y otra en el estudio, estampas extra y cajas y baules movibles.
    /// Las herramientas se clonan de las del campo (ya con el modelo nuevo) para heredar su configuracion.
    /// </summary>
    public static class TowerProps
    {
        public static void Build(Transform map, Material material, Vector3 origin)
        {
            var tower = GameObject.Find("Tower");
            if (tower == null) return;
            float floor = TowerBuilder.FloorHeight;

            // Mesa de trabajo de la biblioteca (planta baja): herramientas en fila.
            TowerBuilder.Table(tower.transform, material, "Workbench_Library", new Vector3(0.4f, 0f, -2.35f), 0f, 2.4f, 0.8f);
            Vector3 top = origin + new Vector3(0.4f, 0.84f, -2.35f);
            Row(origin, top, new[] { "Pen", "Pen", "Brush", "Brush", "Eraser", "Rag" }, 0.1f, 0.18f, 0.22f);
            Row(origin, top, new[] { "Inkwell", "Inkwell" }, -0.25f, 0.9f, 0.2f);

            // Estudio (primer piso): otra mesa con herramientas y tinta.
            TowerBuilder.Table(tower.transform, material, "Workbench_Study", new Vector3(-0.5f, floor, 2.1f), 170f, 1.8f, 0.7f);
            Vector3 studyTop = origin + new Vector3(-0.5f, floor + 0.84f, 2.1f);
            Row(origin, studyTop, new[] { "Pen", "Brush", "Eraser", "Rag", "Inkwell" }, 0.05f, 0.3f, 0.18f, -0.7f);

            // Cajas y baules movibles (se pueden cargar, apilar y quemar).
            Crate(map, material, "Chest_Library_A", origin + new Vector3(-1.3f, 0.2f, 2.0f), new Vector3(0.7f, 0.4f, 0.45f), 15f);
            Crate(map, material, "Crate_Library_A", origin + new Vector3(-0.7f, 0.25f, 2.3f), Vector3.one * 0.5f, -10f);
            Crate(map, material, "Crate_Library_B", origin + new Vector3(-0.7f, 0.76f, 2.3f), Vector3.one * 0.4f, 20f);
            Crate(map, material, "Chest_Study_A", origin + new Vector3(1.4f, floor + 0.2f, -1.6f), new Vector3(0.7f, 0.4f, 0.45f), 160f);
            Crate(map, material, "Crate_Study_A", origin + new Vector3(1.9f, floor + 0.25f, -1.0f), Vector3.one * 0.5f, 5f);
        }

        /// <summary>Clona herramientas del campo y las deja alineadas sobre una mesa.</summary>
        private static void Row(Vector3 origin, Vector3 tableTop, string[] templates, float xStart, float xStep, float z, float zRow = 0f)
        {
            int index = 0;
            foreach (var template in templates)
            {
                var source = GameObject.Find(template);
                if (source == null) continue;
                string name = $"{template}_{(int)(tableTop.y * 100)}_{index}";
                var old = GameObject.Find(name);
                if (old != null) Object.DestroyImmediate(old);
                var clone = Object.Instantiate(source);
                clone.name = name;
                // Echada sobre la mesa, apuntando hacia un costado; cae unos milimetros y se asienta sola.
                float x = xStart + index * xStep * 0.5f;
                // Plumas, pinceles y demas quedan echados; los tinteros, parados.
                bool upright = template == "Inkwell";
                clone.transform.SetPositionAndRotation(
                    tableTop + new Vector3(x, upright ? 0.01f : 0.04f, zRow + (index % 2 == 0 ? z : -z) * 0.5f),
                    Quaternion.Euler(0f, 90f + index * 15f, upright ? 0f : 90f));
                var respawn = clone.GetComponent<Respawnable>();
                if (respawn != null) Object.DestroyImmediate(respawn);
                clone.AddComponent<Respawnable>();
                Undo.RegisterCreatedObjectUndo(clone, name);
                index++;
            }
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
