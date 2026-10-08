using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SpellSimulator.EditorTools
{
    /// <summary>
    /// Sonda de la estacion de viento: en Play lanza un hechizo de viento sobre la pizarra de la torre y mide cuanto se
    /// movieron las hojas, plumas y banderines en 4 s. Resultado en Logs/harness-windprobe.txt. Uso por MCP: HarnessWindProbe.Run().
    /// </summary>
    public static class HarnessWindProbe
    {
        private const string FlagKey = "HarnessWindProbe.State";
        private static readonly string ResultPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "harness-windprobe.txt"));
        private static Vector3[] start;
        private static string[] names;
        private static float castTime;
        private static string castInfo = "";

        public static void Run()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            File.WriteAllText(ResultPath, "RUNNING");
            EditorSceneManagerSave();
            SessionState.SetInt(FlagKey, 1);
            EditorApplication.isPlaying = true;
        }

        private static void EditorSceneManagerSave() => UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

        [InitializeOnLoadMethod]
        private static void Hook() => EditorApplication.update += Tick;

        private static void Tick()
        {
            int state = SessionState.GetInt(FlagKey, 0);
            if (state == 0 || !EditorApplication.isPlaying) return;
            if (state == 1)
            {
                if (Time.frameCount < 60) return;
                var list = new System.Collections.Generic.List<string>();
                foreach (var go in Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (go.name.StartsWith("WindSheet_") || go.name.StartsWith("Feather_") || go.name.StartsWith("Pennant_") || go.name.StartsWith("Candle_Wind")) list.Add(go.name);
                names = list.ToArray();
                start = new Vector3[names.Length];
                for (int i = 0; i < names.Length; i++) start[i] = GameObject.Find(names[i]).transform.position;
                var board = GameObject.Find("WallBoard_Library");
                var canvas = board != null ? board.GetComponent<SpellSimulator.Canvas.DrawingCanvas>() : null;
                if (canvas == null) { Finish("FAIL sin pizarra WallBoard_Library"); return; }
                var spell = HarnessSeals.Cast("WallBoard_Library", "wind-directs-air", ("column", 270f), ("column", 90f));
                castInfo = $"hechizo valido={spell.valid} elemento={spell.element} direccion={spell.direction}";
                castTime = Time.time;
                SessionState.SetInt(FlagKey, 2);
            }
            else if (state == 2 && Time.time - castTime > 4f)
            {
                var sb = new StringBuilder("RESULT " + castInfo + "\n");
                for (int i = 0; i < names.Length; i++)
                {
                    var go = GameObject.Find(names[i]);
                    if (go == null) { sb.AppendLine($"{names[i]}: desaparecio"); continue; }
                    sb.AppendLine($"{names[i]}: movio {Vector3.Distance(start[i], go.transform.position):0.00} m");
                }
                Finish(sb.ToString());
            }
        }

        private static void Finish(string text)
        {
            SessionState.SetInt(FlagKey, 0);
            File.WriteAllText(ResultPath, text);
            EditorApplication.isPlaying = false;
        }
    }
}
