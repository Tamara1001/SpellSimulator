using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpellSimulator.EditorTools.Map
{
    /// <summary>
    /// Conserva los ajustes manuales de la escena frente a MapBuilder.Build() (que regenera todo desde codigo).
    /// Al EMPEZAR un Build compara la escena abierta con la "linea base" (posiciones al terminar el Build anterior,
    /// Logs/MapBaseline.json): lo que la usuaria movio a mano se guarda en Data/MapOverrides.json (se versiona). Al TERMINAR el Build
    /// se reaplican esos overrides sobre los objetos regenerados (mismo camino jerarquico) y se escribe la nueva linea base.
    /// Menus: SpellSimulator/Map/Capturar mi layout manual, Borrar overrides del layout.
    /// </summary>
    public static class MapLayout
    {
        private const string OverridesPath = "Assets/_Project/Data/MapOverrides.json";
        private static readonly string BaselinePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "MapBaseline.json"));
        private const float PositionEpsilon = 0.02f, AngleEpsilon = 1f, ScaleEpsilon = 0.01f;

        [System.Serializable]
        public struct Entry
        {
            public string path;
            // Valores LOCALES (si se mueve un padre a mano, los hijos no figuran como cambiados).
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
        }

        [System.Serializable]
        private class Table { public List<Entry> entries = new List<Entry>(); }

        // Raices que no se siguen (cantidad enorme y siempre regeneradas: terreno, vegetacion, agua).
        private static readonly string[] IgnoredRoots = { "Terrain", "Trees", "Grass", "Rocks", "Lake", "River", "Waterfall", "Water" };

        // ---------------------------------------------------------------- API usada por MapBuilder

        /// <summary>Al empezar el Build: detecta lo movido a mano desde la ultima linea base y lo suma a los overrides.</summary>
        public static int CaptureManualEdits(Scene scene)
        {
            var baseline = Load(BaselinePath, absolute: true);
            if (baseline.Count == 0) return 0;
            var current = Snapshot(scene);
            var overrides = Load(OverridesPath, absolute: false);
            int changed = 0;
            foreach (var kv in current)
            {
                if (!baseline.TryGetValue(kv.Key, out var old) || !Differs(old, kv.Value)) continue;
                overrides[kv.Key] = kv.Value;
                changed++;
            }
            if (changed > 0) Save(OverridesPath, overrides, absolute: false);
            return changed;
        }

        /// <summary>Al terminar el Build: reaplica los overrides y escribe la nueva linea base.</summary>
        public static int ApplyAndRecord(Scene scene)
        {
            var overrides = Load(OverridesPath, absolute: false);
            int applied = 0;
            if (overrides.Count > 0)
            {
                var byPath = Index(scene);
                foreach (var kv in overrides)
                {
                    if (!byPath.TryGetValue(kv.Key, out var go) || go == null) continue;
                    go.transform.localPosition = kv.Value.position;
                    go.transform.localRotation = kv.Value.rotation;
                    go.transform.localScale = kv.Value.scale;
                    var rb = go.GetComponent<Rigidbody>();
                    if (rb != null && !rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                    applied++;
                }
            }
            Save(BaselinePath, Snapshot(scene), absolute: true);
            return applied;
        }

        // ---------------------------------------------------------------- menus

        [MenuItem("SpellSimulator/Map/Capturar mi layout manual")]
        public static void CaptureNow()
        {
            int n = CaptureManualEdits(SceneManager.GetActiveScene());
            Debug.Log($"[MapLayout] {n} objetos movidos a mano guardados en {OverridesPath}.");
        }

        [MenuItem("SpellSimulator/Map/Borrar overrides del layout")]
        public static void ClearOverrides()
        {
            AssetDatabase.DeleteAsset(OverridesPath);
            Debug.Log("[MapLayout] Overrides borrados: el proximo Build vuelve al layout generado.");
        }

        /// <summary>Siembra la linea base desde otra escena (p. ej. la ultima version commiteada) para detectar ediciones previas.</summary>
        public static void SeedBaselineFrom(Scene scene) => Save(BaselinePath, Snapshot(scene), absolute: true);

        // ---------------------------------------------------------------- internos

        private static bool Tracked(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
                foreach (var ignored in IgnoredRoots)
                    if (p.name == ignored || p.name.StartsWith(ignored + "_")) return false;
            return t.GetComponent<Camera>() == null && t.GetComponent<Light>() == null;
        }

        private static string PathOf(Transform t, Dictionary<Transform, string> cache)
        {
            if (cache.TryGetValue(t, out var cached)) return cached;
            string local = t.name;
            if (t.parent != null)
            {
                // Indice entre hermanos con el mismo nombre (para distinguir duplicados).
                int same = 0;
                for (int i = 0; i < t.GetSiblingIndex(); i++)
                    if (t.parent.GetChild(i).name == t.name) same++;
                if (same > 0) local += "#" + same;
                local = PathOf(t.parent, cache) + "/" + local;
            }
            else
            {
                var roots = t.gameObject.scene.GetRootGameObjects();
                int same = 0;
                for (int i = 0; i < roots.Length; i++)
                {
                    if (roots[i].transform == t) break;
                    if (roots[i].name == t.name) same++;
                }
                if (same > 0) local += "#" + same;
            }
            cache[t] = local;
            return local;
        }

        private static Dictionary<string, Entry> Snapshot(Scene scene)
        {
            var result = new Dictionary<string, Entry>();
            var cache = new Dictionary<Transform, string>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!Tracked(t)) continue;
                    string path = PathOf(t, cache);
                    result[path] = new Entry { path = path, position = t.localPosition, rotation = t.localRotation, scale = t.localScale };
                }
            return result;
        }

        private static Dictionary<string, GameObject> Index(Scene scene)
        {
            var result = new Dictionary<string, GameObject>();
            var cache = new Dictionary<Transform, string>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    result[PathOf(t, cache)] = t.gameObject;
            return result;
        }

        private static bool Differs(Entry a, Entry b)
        {
            return (a.position - b.position).sqrMagnitude > PositionEpsilon * PositionEpsilon
                || Quaternion.Angle(a.rotation, b.rotation) > AngleEpsilon
                || (a.scale - b.scale).sqrMagnitude > ScaleEpsilon * ScaleEpsilon;
        }

        private static Dictionary<string, Entry> Load(string path, bool absolute)
        {
            var dict = new Dictionary<string, Entry>();
            string full = absolute ? path : Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
            if (!File.Exists(full)) return dict;
            var table = JsonUtility.FromJson<Table>(File.ReadAllText(full));
            if (table?.entries == null) return dict;
            foreach (var e in table.entries) dict[e.path] = e;
            return dict;
        }

        private static void Save(string path, Dictionary<string, Entry> data, bool absolute)
        {
            var table = new Table();
            foreach (var e in data.Values) table.entries.Add(e);
            table.entries.Sort((a, b) => string.CompareOrdinal(a.path, b.path));
            string full = absolute ? path : Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, JsonUtility.ToJson(table, absolute == false));
            if (!absolute) AssetDatabase.ImportAsset(path);
        }
    }
}
