using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using SpellSimulator.World;
using SpellSimulator.EditorTools.Map;

namespace SpellSimulator.EditorTools
{
    /// <summary>
    /// Verifica en la escena (sin Play) que la terraza-tutorial no se puede saltear: inunda una grilla con una capsula de jugador
    /// en tres posturas (de pie, agachado, con salto) y comprueba que la bajada solo se alcanza usando las dos. Resultado en Logs/harness-terrace.txt.
    /// </summary>
    public static class HarnessTerraceReach
    {
        public static string Run()
        {
            var tutorial = Object.FindFirstObjectByType<TowerTutorial>(FindObjectsInactive.Include);
            if (tutorial == null) return Write("FAIL no hay TowerTutorial");
            var gate = tutorial.transform.Find("TutorialActive/TerraceGate");
            var activeRoot = tutorial.transform.Find("TutorialActive");
            if (gate == null) return Write("FAIL no hay TerraceGate");
            bool wasActive = activeRoot.gameObject.activeSelf;
            activeRoot.gameObject.SetActive(true);
            Physics.SyncTransforms();
            Vector3 o = tutorial.transform.position;
            float floor = o.y + TowerBuilder.TerraceY;
            var sb = new StringBuilder();
            bool walk = Reach(o, floor, true, false, false), crouch = Reach(o, floor, true, true, false), jump = Reach(o, floor, true, false, true), all = Reach(o, floor, true, true, true);
            // Sin reja la bajada tiene que ser alcanzable con las dos posturas: si no, el chequeo no mide nada.
            gate.gameObject.SetActive(false);
            Physics.SyncTransforms();
            bool noGate = Reach(o, floor, true, true, true);
            gate.gameObject.SetActive(true);
            activeRoot.gameObject.SetActive(wasActive);
            sb.AppendLine($"solo caminar: {walk} | agachado: {crouch} | saltando: {jump} | agachado+saltando: {all} | sin reja (agachado+saltando): {noGate}");
            bool ok = !walk && !crouch && !jump && !all && noGate;
            return Write((ok ? "PASS " : "FAIL ") + "(esperado: todo false salvo la ultima; la reja cierra la bajada y la viga/el muro no se rodean)\n" + sb);
        }

        /// <summary>
        /// Piso 2 y piso 1: desde la llegada (este) NO se alcanza la boca de la rampa de bajada (oeste) mientras la pila / la tela esten puestas,
        /// ni agachado ni saltando, y SI se alcanza al quitarlas. Resultado en Logs/harness-floors.txt.
        /// </summary>
        public static string RunFloors()
        {
            var tutorial = Object.FindFirstObjectByType<TowerTutorial>(FindObjectsInactive.Include);
            if (tutorial == null) return Write2("FAIL no hay TowerTutorial");
            Vector3 o = tutorial.transform.position;
            var props = tutorial.transform.Find(MapTutorial.PropsName);
            var sb = new StringBuilder();
            bool ok = true;
            foreach (var (label, level, names) in new[]
            {
                ("piso 2 (pila)", 2, new[] { "TutorialCrate_A", "TutorialCrate_B", "TutorialCrate_C", "TutorialBox_A", "TutorialBox_B", "TutorialBarrel" }),
                ("piso 1 (tela)", 1, new[] { MapTutorial.ClothName }),
            })
            {
                float floor = o.y + TowerBuilder.FloorHeight * level;
                Physics.SyncTransforms();
                bool blocked = ReachFloor(o, floor, true, true);
                foreach (string n in names) { var t = props.Find(n); if (t != null) t.gameObject.SetActive(false); }
                Physics.SyncTransforms();
                bool freed = ReachFloor(o, floor, true, true);
                foreach (string n in names) { var t = props.Find(n); if (t != null) t.gameObject.SetActive(true); }
                Physics.SyncTransforms();
                sb.AppendLine($"{label}: con el obstaculo {blocked} (esperado False) | sin el obstaculo {freed} (esperado True) {(freed ? "" : LastBest)}");
                ok &= !blocked && freed;
            }
            return Write2((ok ? "PASS " : "FAIL ") + sb);
        }

        private static bool ReachFloor(Vector3 o, float floor, bool allowCrouch, bool allowJump)
        {
            const float cell = 0.2f, radius = 0.3f, extent = 6.4f;
            int n = Mathf.CeilToInt(extent * 2f / cell);
            var seen = new bool[n, n];
            var queue = new Queue<Vector2Int>();
            Vector2Int Cell(float x, float z) => new Vector2Int(Mathf.RoundToInt((x + extent) / cell), Mathf.RoundToInt((z + extent) / cell));
            Vector3 World(Vector2Int c) => new Vector3(c.x * cell - extent + o.x, 0f, c.y * cell - extent + o.z);
            bool Free(Vector3 p)
            {
                var postures = new List<(float bottom, float top)> { (0.1f, 1.9f) };
                if (allowCrouch) postures.Add((0.1f, 1.1f));
                if (allowJump) { postures.Add((0.85f, 2.35f)); if (allowCrouch) postures.Add((0.85f, 1.9f)); }
                foreach (var (bottom, top) in postures)
                    if (!Physics.CheckCapsule(new Vector3(p.x, floor + bottom + radius, p.z), new Vector3(p.x, floor + top - radius, p.z), radius, ~0, QueryTriggerInteraction.Ignore))
                        return true;
                return false;
            }
            float bestAng = 999f, bestR = 0f;
            Vector3 s = TowerBuilder.Polar(4.9f, 350f, 0f);
            Vector2Int start = Cell(s.x, s.z);
            seen[start.x, start.y] = true;
            queue.Enqueue(start);
            var dirs = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                Vector3 w = World(c);
                Vector2 rel = new Vector2(w.x - o.x, w.z - o.z);
                float r = rel.magnitude, ang = Mathf.Repeat(Mathf.Atan2(rel.y, rel.x) * Mathf.Rad2Deg, 360f);
                if (r > 4.3f && r < 5.5f && ang > 172f && ang < 190f) return true;
                if (r > 4.3f && r < 5.5f && ang > 172f && ang < 300f && ang < bestAng) { bestAng = ang; bestR = r; }
                foreach (var d in dirs)
                {
                    var nc = c + d;
                    if (nc.x < 0 || nc.y < 0 || nc.x >= n || nc.y >= n || seen[nc.x, nc.y]) continue;
                    Vector3 nw = World(nc);
                    if (new Vector2(nw.x - o.x, nw.z - o.z).magnitude > 5.6f) continue;
                    if (!Free(nw)) continue;
                    seen[nc.x, nc.y] = true;
                    queue.Enqueue(nc);
                }
            }
            LastBest = $"mas cerca: angulo {bestAng:F0} radio {bestR:F2}";
            return false;
        }

        public static string LastBest = "";

        private static string Write2(string text)
        {
            File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "harness-floors.txt")), text);
            return text;
        }

        private static bool Reach(Vector3 o, float floor, bool stand, bool allowCrouch, bool allowJump)
        {
            const float cell = 0.2f, radius = 0.3f, extent = 8.2f;
            int n = Mathf.CeilToInt(extent * 2f / cell);
            var seen = new bool[n, n];
            var queue = new Queue<Vector2Int>();
            Vector2Int Cell(float x, float z) => new Vector2Int(Mathf.RoundToInt((x + extent) / cell), Mathf.RoundToInt((z + extent) / cell));
            Vector3 World(Vector2Int c) => new Vector3(c.x * cell - extent + o.x, 0f, c.y * cell - extent + o.z);
            bool Free(Vector3 p)
            {
                // Pies a +0.4 m (escalon). De pie: cabeza a 1,9; agachado: a 1,1; salto: pies a 0,85 (pasa un muro de 0,7, no una baranda de 1).
                var postures = new List<(float bottom, float top)>();
                postures.Add((0.4f, 1.9f));
                if (allowCrouch) postures.Add((0.4f, 1.1f));
                if (allowJump) { postures.Add((0.85f, 2.35f)); if (allowCrouch) postures.Add((0.85f, 1.9f)); }
                foreach (var (bottom, top) in postures)
                    if (!Physics.CheckCapsule(new Vector3(p.x, floor + bottom + radius, p.z), new Vector3(p.x, floor + top - radius, p.z), radius, ~0, QueryTriggerInteraction.Ignore))
                        return true;
                return false;
            }
            Vector2 spawn = SpellSimulator.World.Map.SpawnRules.TerraceOffset;
            Vector2Int start = Cell(spawn.x, spawn.y);
            seen[start.x, start.y] = true;
            queue.Enqueue(start);
            var dirs = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                Vector3 w = World(c);
                Vector2 rel = new Vector2(w.x - o.x, w.z - o.z);
                float r = rel.magnitude, ang = Mathf.Repeat(Mathf.Atan2(rel.y, rel.x) * Mathf.Rad2Deg, 360f);
                // La bajada: la boca de la rampa (oeste, 180 grados), mas alla de la reja (195).
                if (r > 4.3f && r < 5.6f && ang > 170f && ang < 190f) return true;
                foreach (var d in dirs)
                {
                    var nc = c + d;
                    if (nc.x < 0 || nc.y < 0 || nc.x >= n || nc.y >= n || seen[nc.x, nc.y]) continue;
                    Vector3 nw = World(nc);
                    if (new Vector2(nw.x - o.x, nw.z - o.z).magnitude > 7.7f) continue;
                    if (!Free(nw)) continue;
                    seen[nc.x, nc.y] = true;
                    queue.Enqueue(nc);
                }
            }
            return false;
        }

        private static string Write(string text)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "harness-terrace.txt"));
            File.WriteAllText(path, text);
            return text;
        }
    }
}
