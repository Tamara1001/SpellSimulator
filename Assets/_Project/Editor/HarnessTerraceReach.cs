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

        private static bool Reach(Vector3 o, float floor, bool stand, bool allowCrouch, bool allowJump)
        {
            const float cell = 0.2f, radius = 0.3f, extent = 7.2f;
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
                // La bajada: dentro del canal de la rampa, mas alla de la reja (angulos menores a 252).
                if (r > 3.1f && r < 4.7f && ang < 252f && ang > 100f) return true;
                foreach (var d in dirs)
                {
                    var nc = c + d;
                    if (nc.x < 0 || nc.y < 0 || nc.x >= n || nc.y >= n || seen[nc.x, nc.y]) continue;
                    Vector3 nw = World(nc);
                    if (new Vector2(nw.x - o.x, nw.z - o.z).magnitude > 7.1f) continue;
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
