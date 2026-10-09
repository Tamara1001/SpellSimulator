using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Player;
using SpellSimulator.World;

namespace SpellSimulator.EditorTools
{
    /// <summary>
    /// Matriz de pruebas de lienzos en Play (PC). Cada lienzo agarrable pasa por: soltar, vista completa (V) + dibujar + soltar,
    /// fijar en el aire y desfijar, volver a agarrar, y arrojarlo fuerte contra el piso (no debe atravesarlo). Los lienzos fijos
    /// (caballetes, pizarras, mesas) pasan por vista completa + dibujar + salir. Resultado en Logs/harness-canvasmatrix.txt (PASS/FAIL).
    /// Casos y lo que solo se prueba a mano: Docs/TESTING.md.
    /// </summary>
    public static class HarnessCanvasMatrix
    {
        private const string FlagKey = "HarnessCanvasMatrix.Running";
        private const string StartFrameKey = "HarnessCanvasMatrix.StartFrame";
        private const int WarmupFrames = 40;
        private static readonly string ResultPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "harness-canvasmatrix.txt"));

        private static readonly List<string> failures = new List<string>();
        private static int passedCount, errorLogs;
        private static IEnumerator routine;
        private static float waitUntil;

        // Lienzos que se agarran (PhysicalCanvas): hojas, cuadernos, runas, mitades de sello, lienzos flotantes.
        private static readonly string[] Grabbables =
        {
            "Paper_S", "Paper_M", "Notebook_A", "GiantPaper_Field", "ImpactStone_S", "ImpactStone_M",
            "SplitHalf_A_L", "SplitHalf_A_R", "PaperCanvas", "Canvas_Lake1", "GiantCanvas_Field",
        };

        // Lienzos fijos: solo vista completa (V), dibujo y salida.
        private static readonly string[] Fixed =
        {
            "Easel_Study_Canvas", "DraftingTable_Canvas", "WallBoard_Library", "WallPaper", "DeskPaper",
            "BigCanvas_Library", "Easel_Field_Canvas", "PressurePlate",
        };

        [MenuItem("SpellSimulator/Harness/Run Canvas Matrix")]
        public static void Run()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            File.WriteAllText(ResultPath, "RUNNING");
            SessionState.SetBool(FlagKey, true);
            SessionState.SetInt(StartFrameKey, -1);
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void Hook() => EditorApplication.update += Tick;

        private static void Tick()
        {
            if (!SessionState.GetBool(FlagKey, false) || !EditorApplication.isPlaying) return;
            int start = SessionState.GetInt(StartFrameKey, -1);
            if (start < 0)
            {
                SessionState.SetInt(StartFrameKey, Time.frameCount);
                failures.Clear();
                passedCount = 0;
                errorLogs = 0;
                routine = null;
                Application.logMessageReceived += OnLog;
                return;
            }
            if (Time.frameCount - start < WarmupFrames) return;

            if (routine == null) routine = Matrix();
            if (Time.time < waitUntil) return;
            bool more;
            try
            {
                more = routine.MoveNext();
                if (more && routine.Current is float seconds) waitUntil = Time.time + seconds;
            }
            catch (System.Exception e)
            {
                failures.Add("excepcion: " + e.Message);
                more = false;
            }
            if (more) return;

            SessionState.SetBool(FlagKey, false);
            Application.logMessageReceived -= OnLog;
            if (errorLogs > 0) failures.Add(errorLogs + " errores en la consola durante la prueba");
            string result = failures.Count == 0 ? $"PASS ({passedCount} chequeos)" : $"FAIL {failures.Count} de {passedCount + failures.Count}: " + string.Join(" | ", failures);
            File.WriteAllText(ResultPath, result);
            Debug.Log("[CanvasMatrix] " + result);
            EditorApplication.isPlaying = false;
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errorLogs++;
        }

        private static void Check(bool ok, string name)
        {
            if (ok) passedCount++;
            else failures.Add(name);
        }

        // ---------------------------------------------------------------- matriz

        private static IEnumerator Matrix()
        {
            var menu = UI.MainMenu.Instance;
            if (menu != null) menu.EnterSandbox();

            var player = GameObject.Find("Player");
            var mover = player != null ? player.GetComponent<ObjectMover>() : null;
            var cam = Camera.main != null ? Camera.main.transform : null;
            if (mover == null || cam == null) { failures.Add("faltan Player/ObjectMover/camara"); yield break; }

            Vector3 spot = FindSpot();
            if (spot == Vector3.zero) { failures.Add("no se encontro un terreno plano para las pruebas"); yield break; }
            float ground = spot.y;

            if (SessionState.GetString("HarnessCanvasMatrix.Only", "") == "FOCUSRAY")
            {
                var probe = FocusRayProbe(player, mover, cam);
                while (probe.MoveNext()) yield return probe.Current;
                yield break;
            }
            if (SessionState.GetString("HarnessCanvasMatrix.Only", "") == "RUNE")
            {
                var probe = RuneFireProbe(player, mover, cam, spot);
                while (probe.MoveNext()) yield return probe.Current;
                yield break;
            }

            // --- Lienzos que se agarran
            foreach (string name in Grabbables)
            {
                if (!Wanted(name)) continue;
                var go = GameObject.Find(name);
                if (go == null) { failures.Add(name + ": no existe en la escena"); continue; }
                var rb = go.GetComponent<Rigidbody>();
                var pc = go.GetComponent<PhysicalCanvas>();
                var canvas = go.GetComponent<DrawingCanvas>();
                if (rb == null || pc == null || canvas == null) { failures.Add(name + ": le falta Rigidbody/PhysicalCanvas/DrawingCanvas"); continue; }

                // 1) agarrar y soltar: cae sin quedar pegado a nada
                Place(pc, rb, spot + Vector3.up * 1.2f);
                Check(mover.TryBegin(rb, 2f), name + ": agarrar");
                yield return 0.3f;
                mover.End(false);
                yield return 1.5f;
                Check(go.transform.parent == null && !rb.isKinematic && rb.position.y > ground - 0.1f, name + ": soltar cae y queda libre " + Info(rb, ground));

                // 2) vista completa (V) + dibujar + soltar con la vista abierta (bug: quedaba hijo de la camara)
                Place(pc, rb, spot + Vector3.up * 1.2f);
                mover.TryBegin(rb, 2f);
                yield return 0.2f;
                pc.SetFocus(cam, true);
                Check(pc.Focused && FocusMode.CanvasView, name + ": V abre la vista completa");
                HarnessSeals.Draw(canvas, "fire");
                Check(canvas.Session.Strokes.Count > 0, name + ": dibujar en vista completa");
                mover.End(false);
                yield return 0.4f;
                Check(go.transform.parent == null && !pc.Focused && !FocusMode.Active && !rb.isKinematic, name + ": soltar con V abierta libera todo");
                yield return 1.2f;

                // 3) V + fijar en el aire + volver a agarrar y soltar
                Place(pc, rb, spot + Vector3.up * 1.2f);
                mover.TryBegin(rb, 2f);
                yield return 0.2f;
                pc.SetFocus(cam, true);
                mover.End(true);
                yield return 0.4f;
                Check(go.transform.parent == null && !pc.Focused && !FocusMode.Active && rb.isKinematic, name + ": fijar con V abierta queda flotando libre");
                Check(mover.TryBegin(rb, 2f), name + ": volver a agarrar lo fijado");
                yield return 0.2f;
                Check(!rb.isKinematic, name + ": lo agarrado vuelve a ser fisico");
                mover.End(false);
                yield return 1.5f;
                Check(go.transform.parent == null && !rb.isKinematic && rb.position.y > ground - 0.1f, name + ": soltar tras desfijar cae " + Info(rb, ground));

                // 4) arrojarlo fuerte contra el piso desde varios angulos: no debe atravesarlo
                for (int i = 0; i < 3; i++)
                {
                    Place(pc, rb, spot + Vector3.up * (1.5f + i * 0.5f));
                    mover.TryBegin(rb, 2f);
                    yield return 0.1f;
                    mover.End(false);
                    Vector3 dir = (Vector3.down * 2f + new Vector3(i - 1, 0f, 1f - i * 0.5f)).normalized;
                    rb.linearVelocity = dir * (30f + i * 15f);
                    yield return 1.5f;
                    Check(rb.position.y > ground - 0.15f, name + ": arrojado fuerte (" + i + ") no atraviesa el piso " + Info(rb, ground));
                }

                canvas.Clear();
                pc.ResetToInitial();
            }

            // --- Lienzos fijos: vista completa (V) + dibujar + salir
            foreach (string name in Fixed)
            {
                if (!Wanted(name)) continue;
                var go = GameObject.Find(name);
                var canvas = go != null ? go.GetComponent<DrawingCanvas>() : null;
                if (canvas == null) { failures.Add(name + ": no existe o sin DrawingCanvas"); continue; }
                Vector3 camLocal = cam.localPosition;
                Quaternion camLocalRot = cam.localRotation;
                Check(SurfaceFocus.ShowCanvas(cam, canvas), name + ": V abre la vista completa");
                yield return 0.2f;
                Check(SurfaceFocus.IsActive && FocusMode.CanvasView, name + ": vista completa activa");
                HarnessSeals.Draw(canvas, "fire");
                Check(canvas.Session.Strokes.Count > 0, name + ": dibujar en vista completa");
                SurfaceFocus.Close();
                yield return 0.1f;
                Check(!SurfaceFocus.IsActive && !FocusMode.Active && (cam.localPosition - camLocal).sqrMagnitude < 1e-4f
                      && Quaternion.Angle(cam.localRotation, camLocalRot) < 0.1f, name + ": salir devuelve la camara");
                canvas.Clear();
            }

            // --- Cualquier objeto fisico (herramientas, sellos, movibles): no atraviesan el piso al caer fuerte y se pueden fijar/desfijar
            var bodies = new List<Rigidbody>();
            foreach (string n in new[] { "Pen", "Eraser" }) AddBody(bodies, GameObject.Find(n));
            foreach (var s in Object.FindObjectsByType<Stamp>(FindObjectsSortMode.None)) AddBody(bodies, s.gameObject);
            // Solo cuerpos raiz: las partes de un muñeco (hijas con articulaciones) no se mueven solas.
            foreach (var m in Object.FindObjectsByType<Movable>(FindObjectsSortMode.None)) if (m.GetComponentInParent<TrainingDummy>() == null) AddBody(bodies, m.gameObject);
            foreach (var rb in bodies)
            {
                string name = rb.name;
                if (!Wanted(name)) continue;
                bool wasKinematic = rb.isKinematic;
                rb.isKinematic = false;
                rb.position = spot + Vector3.up * 2f;
                rb.linearVelocity = Vector3.down * 45f;
                yield return 1.5f;
                Check(rb.position.y > ground - 0.15f, name + ": caida fuerte no atraviesa el piso " + Info(rb, ground));

                if (rb.GetComponentInParent<Interfaces.IRemoteGrabbable>() == null && rb.GetComponent<PhysicalTool>() == null && rb.GetComponent<Stamp>() == null)
                {
                    // Objeto generico: fijar en el aire y desfijar.
                    rb.position = spot + Vector3.up * 2f;
                    rb.linearVelocity = Vector3.zero;
                    if (mover.TryBegin(rb, 2f))
                    {
                        yield return 0.1f;
                        mover.End(true);
                        yield return 0.3f;
                        Check(rb.isKinematic, name + ": fijado en el aire");
                        mover.TryBegin(rb, 2f);
                        yield return 0.1f;
                        mover.End(false);
                        // El movedor lo lleva hacia la camara antes de soltarlo: se compara con la altura al soltar.
                        float yRelease = rb.position.y;
                        yield return 1.2f;
                        // Puede estar cerca de algo que lo sostiene: se pide fisica restaurada (no cinematico, con gravedad), no una caida minima.
                        Check(!rb.isKinematic && rb.useGravity && rb.GetComponent<PinnedBody>() == null, name + ": desfijado recupera la fisica " + Info(rb, yRelease));
                    }
                }
                rb.isKinematic = wasKinematic;
            }
        }

        /// <summary>Filtro opcional (SessionState "HarnessCanvasMatrix.Only", nombres separados por coma) para repetir solo algunos casos.</summary>
        private static bool Wanted(string name)
        {
            string only = SessionState.GetString("HarnessCanvasMatrix.Only", "");
            return only.Length == 0 || System.Array.IndexOf(only.Split(','), name) >= 0;
        }

        private static string Info(Rigidbody rb, float ground) =>
            $"[y={rb.position.y:F2} suelo={ground:F2} padre={(rb.transform.parent != null ? rb.transform.parent.name : "-")} cin={rb.isKinematic}]";

        /// <summary>
        /// Lienzos agarrables en su lugar ORIGINAL (torre, mesas...): se agarran desde 4 lados, se abre la vista completa y se tira el rayo
        /// del puntero (centro de pantalla) igual que PhysicalTool: lo primero que golpea debe ser el lienzo, no un mueble o pared.
        /// Se activa con Only = "FOCUSRAY".
        /// </summary>
        private static IEnumerator FocusRayProbe(GameObject player, ObjectMover mover, Transform cam)
        {
            var camera = cam.GetComponent<Camera>();
            Vector3[] dirs = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            foreach (string name in Grabbables)
            {
                var go = GameObject.Find(name);
                // El papel gigante es enorme y Respawnable lo reaparece al acercarse (artefacto de la prueba).
                if (go == null || name == "GiantPaper_Field") continue;
                var rb = go.GetComponent<Rigidbody>();
                var pc = go.GetComponent<PhysicalCanvas>();
                var canvas = go.GetComponent<DrawingCanvas>();
                if (rb == null || pc == null || canvas == null) continue;
                pc.ResetToInitial();
                Vector3 origin = go.transform.position;
                foreach (var dir in dirs)
                {
                    var cc = player.GetComponent<CharacterController>();
                    if (cc != null) cc.enabled = false;
                    Vector3 p = origin + dir * 1.3f;
                    // Los pies sobre el piso real de ese lugar (si no, el jugador cae y Respawnable lo reaparece con el lienzo).
                    p.y = Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out RaycastHit floor, 4f, ~0, QueryTriggerInteraction.Ignore) ? floor.point.y + 0.05f : origin.y;
                    player.transform.SetPositionAndRotation(p, Quaternion.LookRotation(-dir));
                    if (cc != null) cc.enabled = true;
                    Physics.SyncTransforms();
                    yield return 0.3f;
                    if (!mover.TryBegin(rb, 1.3f)) continue;
                    yield return 0.6f;
                    pc.SetFocus(cam, true);
                    yield return 0.2f;
                    var ray = camera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
                    string what = "nada";
                    bool ok = false;
                    var pen = GameObject.Find("Pen")?.GetComponent<PhysicalTool>();
                    if (pen != null && pen.TryShootRay(ray, 5f, out _, out var hitCanvas))
                    {
                        ok = hitCanvas == canvas;
                        what = hitCanvas.name;
                    }
                    else if (Physics.Raycast(ray, out RaycastHit hit, 5f, ~0, QueryTriggerInteraction.Collide)) what = hit.collider.name + " a " + hit.distance.ToString("F2") + " m";
                    Check(ok, $"{name} desde {dir}: el puntero en vista completa dibuja en el lienzo (ve: {what}; enfocado={pc.Focused}, vista={FocusMode.CanvasView}, canvasEnfocado={(FocusMode.FocusedCanvas != null ? FocusMode.FocusedCanvas.name : "-")}, sosteniendo={mover.IsHolding})");
                    mover.End(false);
                    yield return 0.3f;
                    pc.ResetToInitial();
                }
            }
        }

        /// <summary>
        /// Una runa armada (anillo cerrado) lanza su hechizo al golpear (impacto >= 3 m/s). Compara soltarla con la vista completa
        /// abierta, soltarla tras cerrarla y soltarla sin vista: en los tres debe salir el hechizo. Se activa con Only = "RUNE".
        /// </summary>
        private static IEnumerator RuneFireProbe(GameObject player, ObjectMover mover, Transform cam, Vector3 spot)
        {
            var go = GameObject.Find("ImpactStone_S");
            var rb = go.GetComponent<Rigidbody>();
            var pc = go.GetComponent<PhysicalCanvas>();
            var canvas = go.GetComponent<DrawingCanvas>();
            var caster = Object.FindFirstObjectByType<Casting.SpellCaster>();
            int compiled = 0;
            void OnCompiled(Compiler.SpellIR spell, DrawingCanvas c) { if (c == canvas) compiled++; }
            caster.SpellCompiled += OnCompiled;

            string[] scenarios = { "sin vista (E directo)", "V, E", "V, V, E" };
            for (int sc = 0; sc < scenarios.Length; sc++)
            {
                // El jugador parado en el claro, mirando al frente, como en la prueba a mano.
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.transform.SetPositionAndRotation(spot + new Vector3(0f, 0.1f, -3f), Quaternion.identity);
                if (cc != null) cc.enabled = true;
                Physics.SyncTransforms();
                yield return 0.5f;

                Place(pc, rb, spot + Vector3.up * 1.2f);
                canvas.Clear();
                caster.OnSealEdited(canvas);
                HarnessSeals.Draw(canvas, "fire");
                compiled = 0;
                mover.TryBegin(rb, 2f);
                yield return 0.3f;
                if (sc >= 1) { pc.SetFocus(cam, true); yield return 0.3f; }
                if (sc == 2) { pc.SetFocus(cam, false); yield return 0.5f; }
                // Dibujar el circulo en vista completa termina el trazo: el sello queda armado.
                caster.OnStrokeFinished(canvas);
                var phase = caster.GetPhase(canvas);
                Vector3 releasePos = rb.position;
                mover.End(false);
                float minY = releasePos.y;
                for (float t = 0f; t < 2.5f; t += 0.1f) { yield return 0.1f; minY = Mathf.Min(minY, rb.position.y); }
                Check(compiled > 0, $"runa [{scenarios[sc]}]: sale el hechizo (fase al soltar={phase}, fase final={caster.GetPhase(canvas)}, y suelta={releasePos.y - spot.y:F2} sobre el piso, y minima={minY - spot.y:F2}, camara y={cam.position.y - spot.y:F2})");
            }
            caster.SpellCompiled -= OnCompiled;
        }

        private static void AddBody(List<Rigidbody> list, GameObject go)
        {
            if (go == null) return;
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null && !list.Contains(rb)) list.Add(rb);
        }

        private static void Place(PhysicalCanvas pc, Rigidbody rb, Vector3 position)
        {
            pc.ResetToInitial();
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = position;
            rb.rotation = Quaternion.identity;
            rb.transform.SetPositionAndRotation(position, Quaternion.identity);
        }

        /// <summary>Primer claro plano (radio 3 m, sin desniveles) de terreno fuera del agua; Vector3.zero si no hay.</summary>
        private static Vector3 FindSpot()
        {
            for (int x = -90; x <= 90; x += 5)
                for (int z = -90; z <= 90; z += 5)
                {
                    if (!TerrainAt(x, z, out Vector3 center) || center.y < World.Map.ValleyShape.WaterLevel + 0.5f) continue;
                    bool flat = true;
                    for (int k = 0; k < 8 && flat; k++)
                    {
                        float a = k * Mathf.PI / 4f;
                        flat = TerrainAt(x + Mathf.Cos(a) * 3f, z + Mathf.Sin(a) * 3f, out Vector3 p) && Mathf.Abs(p.y - center.y) < 0.15f;
                    }
                    if (flat) return center;
                }
            return Vector3.zero;
        }

        private static bool TerrainAt(float x, float z, out Vector3 point)
        {
            point = default;
            if (!Physics.Raycast(new Vector3(x, 200f, z), Vector3.down, out RaycastHit hit, 400f, ~0, QueryTriggerInteraction.Ignore)
                || hit.collider.name != "Terrain" || hit.normal.y < 0.97f) return false;
            point = hit.point;
            return true;
        }
    }
}
