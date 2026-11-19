using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using SpellSimulator.Casting;
using SpellSimulator.Lab;
using SpellSimulator.World;
using SpellSimulator.EditorTools.Map;

namespace SpellSimulator.EditorTools
{
    /// <summary>
    /// Prueba de hechizos reales sobre la torre-tutorial (T3/T4): entra a Play, pone el papel del banco del laboratorio frente a la tela
    /// y a cada traba de la puerta, lanza recetas reales con SpellCaster.Cast y mide si se sueltan. Resultado en Logs/harness-spelltest.txt.
    /// Uso: HarnessSpellTest.Run() y leer el archivo hasta que no diga RUNNING. Es una herramienta de ajuste, no un test de CI.
    /// </summary>
    public static class HarnessSpellTest
    {
        private const string FlagKey = "HarnessSpellTest.Running";
        private static readonly string ResultPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "harness-spelltest.txt"));
        private static IEnumerator routine;
        private static readonly StringBuilder report = new StringBuilder();
        private static int startFrame = -1;

        [MenuItem("SpellSimulator/Harness/Run Spell Test (torre)")]
        public static void Run()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            File.WriteAllText(ResultPath, "RUNNING");
            SessionState.SetBool(FlagKey, true);
            startFrame = -1;
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void Hook() => EditorApplication.update += Tick;

        private static void Tick()
        {
            if (routine != null)
            {
                bool more;
                try { more = routine.MoveNext(); }
                catch (System.Exception e) { report.AppendLine("EXCEPCION " + e); more = false; }
                if (!more)
                {
                    routine = null;
                    File.WriteAllText(ResultPath, "DONE\n" + report);
                    EditorApplication.isPlaying = false;
                }
                return;
            }
            if (!SessionState.GetBool(FlagKey, false) || !EditorApplication.isPlaying) return;
            if (startFrame < 0) { startFrame = Time.frameCount; return; }
            if (Time.frameCount - startFrame < 60) return;
            SessionState.SetBool(FlagKey, false);
            report.Clear();
            routine = Scenarios();
        }

        private static T Field<T>(object o, string name) =>
            (T)o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);

        private static IEnumerable Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        private static IEnumerator Scenarios()
        {
            var bench = Object.FindFirstObjectByType<LabSealBench>();
            var tutorial = TowerTutorial.Instance;
            if (bench == null || tutorial == null) { report.AppendLine("falta el banco o el tutorial"); yield break; }
            var caster = Field<SpellCaster>(bench, "caster");
            var rebuild = typeof(LabSealBench).GetMethod("Rebuild", BindingFlags.Public | BindingFlags.Instance);
            var ensure = typeof(LabSealBench).GetMethod("EnsureReady", BindingFlags.NonPublic | BindingFlags.Instance);
            ensure.Invoke(bench, null);
            Vector3 o = tutorial.transform.position;
            Vector3 Polar(float r, float a, float y) => o + new Vector3(r * Mathf.Cos(a * Mathf.Deg2Rad), y, r * Mathf.Sin(a * Mathf.Deg2Rad));

            var props = tutorial.transform.Find(MapTutorial.PropsName);
            var cloth = props.Find(MapTutorial.ClothName).GetComponent<TutorialCloth>();
            var latches = props.Find(MapTutorial.DoorLatchesName);
            var bar = latches.Find("DoorLatch_Bar").GetComponent<TutorialCloth>();
            var weight = latches.Find("DoorLatch_Weight").GetComponent<TutorialCloth>();
            var stone = GameObject.Find(MapTutorial.DoorStoneName).GetComponent<Breakable>();

            // Cada escenario: receta, centro del papel, direccion de emision, quien tiene que ceder.
            float y1 = TowerBuilder.FloorHeight;
            // Geometria de la torre v2: todo pegado al muro. La tela esta entre la baranda y el muro; se le dispara desde el lado sur (angulos
            // mayores), de frente a su cara (tangente), como lo hace quien dibuja en el caballete.
            float clothR = (TowerBuilder.RampInner + TowerBuilder.InnerRadius) * 0.5f;
            Vector3 clothC = Polar(clothR, MapTutorial.ClothAngle, y1 + 1.6f);
            Vector3 clothDir = (Polar(clothR, MapTutorial.ClothAngle - 30f, 0f) - Polar(clothR, MapTutorial.ClothAngle + 30f, 0f)); // tangente hacia el oeste
            clothDir.y = 0f; clothDir.Normalize();
            Vector3 barC = Polar(TowerBuilder.WallRadius(MapTutorial.LatchAngle, 0.12f) - MapTutorial.LatchGap, MapTutorial.LatchAngle, 1.5f);
            Vector3 barIn = (barC - o); barIn.y = 0f; barIn.Normalize();
            Vector3 weightC = Polar(TowerBuilder.WallRadius(MapTutorial.WeightAngle, 0.36f), MapTutorial.WeightAngle, 1.0f);
            Vector3 stoneC = Polar(TowerBuilder.WallRadius(MapTutorial.StoneAngle, 0.5f), MapTutorial.StoneAngle, 0.5f);
            Vector3 stoneIn = (stoneC - o); stoneIn.y = 0f; stoneIn.Normalize();

            foreach (string recipe in new[] { "gust", "wind_fan", "wind_breath" })
                foreach (float dist in new[] { 0.8f, 1.2f, 1.8f, 2.6f })
                {
                    foreach (var step in Shoot("tela", recipe, clothC - clothDir * dist, clothDir, dist, () => cloth.IsOpen, () => tutorial.Begin(), bench, caster, rebuild)) yield return step;
                    foreach (var step in Shoot("pestillo", recipe, barC - barIn * dist, barIn, dist, () => bar.IsOpen, () => tutorial.Begin(), bench, caster, rebuild)) yield return step;
                }
            // Piso 1 (tierra): una roca lanzada empuja la puerta de pared a baranda.
            foreach (string recipe in new[] { "thrown_rock", "earth_spike", "stone_wall" })
                foreach (float dist in new[] { 1.0f, 1.8f, 2.6f })
                    foreach (var step in Shoot("puerta+tierra", recipe, clothC - clothDir * dist, clothDir, dist, () => cloth.IsOpen, () => tutorial.Begin(), bench, caster, rebuild)) yield return step;
            foreach (string recipe in new[] { "updraft", "water_jet", "wind_breath", "gust" })
                foreach (float dist in new[] { 0.5f, 1.0f })
                    foreach (var step in Shoot("contrapeso", recipe, weightC - Vector3.up * dist, Vector3.up, dist, () => weight.IsOpen, () => tutorial.Begin(), bench, caster, rebuild)) yield return step;
            foreach (string recipe in new[] { "earth_spike", "stone_wall", "thrown_rock" })
                foreach (float dist in new[] { 0.8f, 1.5f, 2.5f })
                    foreach (var step in Shoot("piedra+crush", recipe, stoneC - stoneIn * dist, stoneIn, dist, () => stone.IsBroken, () => tutorial.Begin(), bench, caster, rebuild, true)) yield return step;
        }

        private static IEnumerable Shoot(string target, string recipe, Vector3 center, Vector3 dir, float dist, System.Func<bool> released, System.Action reset,
            LabSealBench bench, SpellCaster caster, MethodInfo rebuild, bool crush = false)
        {
            Object.FindFirstObjectByType<SpellSimulator.Reactions.MagicWorld>().ResetWorld();
            reset();
            foreach (var w in Wait(0.7f)) yield return w;
            bench.ResetSpec();
            bench.ApplyRecipe(recipe);
            if (crush)
            {
                int crushAt = -1;
                for (int i = 0; i < bench.SignIds.Count; i++) if (bench.SignIds[i] == "crush") crushAt = i;
                if (crushAt >= 0) bench.SetVar(LabSealVarId.SignType, crushAt);
            }
            bench.SetVar(LabSealVarId.Tremor, 0f);
            rebuild.Invoke(bench, null);
            var canvas = bench.Canvas;
            Transform t = canvas.transform;
            t.rotation = Quaternion.FromToRotation(canvas.FaceNormal, dir) * t.rotation;
            canvas.SetInkSign(1);
            if (Vector3.Dot(canvas.FaceNormal * canvas.InkSign, dir) < 0.5f) canvas.SetInkSign(-1);
            t.position = center - t.rotation * canvas.CenterOffsetLocalScaled;
            Physics.SyncTransforms();
            var spell = caster.Cast(canvas);
            float t0 = Time.time;
            bool opened = false;
            float openedAt = -1f;
            while (Time.time - t0 < 3.5f)
            {
                if (!opened && released()) { opened = true; openedAt = Time.time - t0; }
                yield return null;
            }
            report.AppendLine($"{target,-10} {recipe,-12} dist {dist:0.0} valido={spell.valid} elemento={spell.element} -> {(opened ? $"SUELTA a {openedAt:0.00}s" : "no cede")}");
            canvas.SetInkSign(1);
        }
    }
}
