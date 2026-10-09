using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using SpellSimulator.Canvas;
using SpellSimulator.Player;
using SpellSimulator.World;

namespace SpellSimulator.EditorTools
{
    /// <summary>
    /// Prueba con ENTRADA REAL (teclado y mouse virtuales del Input System): agarra un lienzo con E (la misma ruta del interactor),
    /// aprieta Tab para la vista completa y dibuja con el mouse, igual que una persona. Sirve para cazar bugs de la cadena completa
    /// (interactor + vista completa + puntero + lapiz) que las pruebas que llaman a las APIs directo no ven.
    /// Menu: SpellSimulator/Harness/Run Real Input Probe. Resultado en Logs/harness-realinput.txt.
    /// </summary>
    public static class HarnessRealInput
    {
        private const string FlagKey = "HarnessRealInput.Running";
        private const string StartFrameKey = "HarnessRealInput.StartFrame";
        private const int WarmupFrames = 60;
        private static readonly string ResultPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "harness-realinput.txt"));

        private static readonly List<string> failures = new List<string>();
        private static readonly List<string> notes = new List<string>();
        private static int passed, errorLogs;
        private static IEnumerator routine;
        private static float waitUntil;
        private static Keyboard keyboard;
        private static Mouse mouse;

        [MenuItem("SpellSimulator/Harness/Run Real Input Probe")]
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
                failures.Clear(); notes.Clear(); passed = 0; errorLogs = 0; routine = null;
                Application.logMessageReceived += OnLog;
                return;
            }
            if (Time.frameCount - start < WarmupFrames) return;
            if (routine == null) routine = Probe();
            if (Time.time < waitUntil) return;
            bool more;
            try
            {
                more = routine.MoveNext();
                if (more && routine.Current is float seconds) waitUntil = Time.time + seconds;
            }
            catch (System.Exception e) { failures.Add("excepcion: " + e); more = false; }
            if (more) return;

            SessionState.SetBool(FlagKey, false);
            Application.logMessageReceived -= OnLog;
            if (keyboard != null) { InputSystem.RemoveDevice(keyboard); keyboard = null; }
            if (mouse != null) { InputSystem.RemoveDevice(mouse); mouse = null; }
            string result = (failures.Count == 0 ? $"PASS ({passed} chequeos)" : $"FAIL {failures.Count} de {passed + failures.Count}: " + string.Join(" | ", failures))
                            + (notes.Count > 0 ? "\nNOTAS: " + string.Join(" | ", notes) : "");
            File.WriteAllText(ResultPath, result);
            Debug.Log("[RealInput] " + result);
            EditorApplication.isPlaying = false;
        }

        private static void OnLog(string c, string s, LogType t) { if (t == LogType.Error || t == LogType.Exception) errorLogs++; }
        private static void Check(bool ok, string name) { if (ok) passed++; else failures.Add(name); }

        private static void Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            InputSystem.Update();
        }
        private static void ReleaseKeys()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
        }
        private static void MouseAt(Vector2 position, bool left)
        {
            var state = new MouseState { position = position };
            if (left) state = state.WithButton(MouseButton.Left);
            InputSystem.QueueStateEvent(mouse, state);
            InputSystem.Update();
        }

        private static IEnumerator Probe()
        {
            var menu = UI.MainMenu.Instance;
            if (menu != null) menu.EnterSandbox();
            var player = GameObject.Find("Player");
            var interactor = player != null ? player.GetComponent<PlayerInteractor>() : null;
            var mover = player != null ? player.GetComponent<ObjectMover>() : null;
            var pen = GameObject.Find("Pen");
            var cam = Camera.main != null ? Camera.main.transform : null;
            if (interactor == null || mover == null || pen == null || cam == null) { failures.Add("faltan Player/Interactor/Mover/Pen/camara"); yield break; }

            var settings = InputSystem.settings;
            var oldBackground = settings.backgroundBehavior;
            var oldEditor = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>("RealInputProbeKeyboard");
            mouse = InputSystem.AddDevice<Mouse>("RealInputProbeMouse");

            Vector3 spot = FindSpot();
            if (spot == Vector3.zero) { failures.Add("sin terreno plano"); yield break; }
            var penTool = pen.GetComponent<PhysicalTool>();

            foreach (string name in new[] { "Paper_M", "ImpactStone_S", "Notebook_A", "ImpactStone_M", "ImpactGold_S", "ImpactMetal_M" })
            {
                var go = GameObject.Find(name);
                if (go == null) { notes.Add(name + " no existe"); continue; }
                var rb = go.GetComponent<Rigidbody>();
                var pc = go.GetComponent<PhysicalCanvas>();
                var canvas = go.GetComponent<DrawingCanvas>();
                if (rb == null || pc == null || canvas == null) { notes.Add(name + " sin componentes"); continue; }

                // jugador parado en el claro mirando al frente, lapiz equipado
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.transform.SetPositionAndRotation(spot + new Vector3(0f, 0.1f, -2f), Quaternion.identity);
                if (cc != null) cc.enabled = true;
                cam.localRotation = Quaternion.identity;
                Physics.SyncTransforms();
                if (interactor.MainItem != pen) interactor.GrabItem(pen);
                pc.ResetToInitial(); canvas.Clear();
                rb.isKinematic = false; rb.useGravity = true; rb.linearVelocity = Vector3.zero;
                rb.position = spot + new Vector3(0f, 1.2f, 0.5f);
                rb.rotation = Quaternion.identity;
                yield return 0.5f;

                // E: agarrar lo que se mira (misma ruta del interactor)
                interactor.GrabItem(go);
                yield return 0.7f;
                Check(mover.IsHolding && mover.HeldBody == rb, $"{name}: E lo agarra con el movedor");

                // Tab: vista completa (tecla real)
                Press(Key.Tab); yield return 0.05f; ReleaseKeys();
                yield return 0.5f;
                Check(pc.Focused && FocusMode.CanvasView, $"{name}: Tab abre la vista completa (enfocado={pc.Focused}, vista={FocusMode.CanvasView}, canvasEnfocado={(FocusMode.FocusedCanvas != null ? FocusMode.FocusedCanvas.name : "-")})");

                // el rayo del puntero en el centro de pantalla: ¿que ve el lapiz?
                Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                MouseAt(center, false);
                yield return 0.1f;
                var ray = Camera.main.ScreenPointToRay(center);
                bool sees = penTool != null && penTool.TryShootRay(ray, 5f, out _, out var seen) && seen == canvas;
                string hits = "";
                foreach (var h in Physics.RaycastAll(ray, 5f, ~0, QueryTriggerInteraction.Collide)) hits += h.collider.name + "(" + h.distance.ToString("F2") + ") ";
                Check(sees, $"{name}: el puntero del lapiz ve el lienzo en el centro (golpes: {hits})");

                // dibujar con el mouse real: clic izquierdo y un circulo
                MouseAt(center, true);
                yield return 0.05f;
                for (int i = 0; i <= 24; i++)
                {
                    float a = i / 24f * Mathf.PI * 2f;
                    MouseAt(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 40f, true);
                    yield return 0.03f;
                }
                MouseAt(center, false);
                yield return 0.3f;
                int strokes = canvas.Session.Strokes.Count;
                Check(strokes > 0, $"{name}: se puede dibujar en vista completa con el mouse (trazos={strokes})");

                // salir: Tab otra vez y soltar
                Press(Key.Tab); yield return 0.05f; ReleaseKeys();
                yield return 0.3f;
                Check(!pc.Focused && !FocusMode.CanvasView, $"{name}: Tab otra vez cierra la vista completa");
                mover.End(false);
                yield return 0.3f;
                canvas.Clear();
            }

            settings.backgroundBehavior = oldBackground;
            settings.editorInputBehaviorInPlayMode = oldEditor;
        }

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
