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

        // Lienzos que se agarran (PhysicalCanvas): hojas, cuadernos, runas (piedra, metal, oro), lienzos flotantes.
        private static readonly string[] Grabbables =
        {
            "Paper_S", "Paper_M", "Notebook_A", "GiantPaper_Field", "ImpactStone_S", "ImpactStone_M",
            "ImpactStone_J1", "ImpactMetal_M", "ImpactGold_S", "PaperCanvas", "Canvas_Lake1", "GiantCanvas_Field",
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

            if (SessionState.GetString("HarnessCanvasMatrix.Only", "") == "RUNEJOIN")
            {
                var probe = RuneJoinProbe(spot);
                while (probe.MoveNext()) yield return probe.Current;
                yield break;
            }
            if (SessionState.GetString("HarnessCanvasMatrix.Only", "") == "HALVES")
            {
                var probe = HalvesProbe(spot);
                while (probe.MoveNext()) yield return probe.Current;
                yield break;
            }
            if (SessionState.GetString("HarnessCanvasMatrix.Only", "") == "NESTED")
            {
                var probe = NestedProbe(spot);
                while (probe.MoveNext()) yield return probe.Current;
                yield break;
            }
            if (SessionState.GetString("HarnessCanvasMatrix.Only", "") == "PORTAL")
            {
                var probe = PortalProbe(spot);
                while (probe.MoveNext()) yield return probe.Current;
                yield break;
            }
            if (SessionState.GetString("HarnessCanvasMatrix.Only", "") == "SPELLS")
            {
                var probe = SpellsProbe(spot);
                while (probe.MoveNext()) yield return probe.Current;
                yield break;
            }
            if (SessionState.GetString("HarnessCanvasMatrix.Only", "") == "CHARGE")
            {
                var probe = ChargeProbe();
                while (probe.MoveNext()) yield return probe.Current;
                yield break;
            }
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
        /// Dos runas armadas que se tocan se fusionan en un solo hechizo (la mas grande manda) y ambas quedan gastadas.
        /// Se activa con Only = "RUNEJOIN".
        /// </summary>
        private static IEnumerator RuneJoinProbe(Vector3 spot)
        {
            var a = GameObject.Find("ImpactStone_J1");
            var b = GameObject.Find("ImpactStone_J2");
            if (a == null || b == null) { failures.Add("faltan ImpactStone_J1/J2"); yield break; }
            var caster = Object.FindFirstObjectByType<Casting.SpellCaster>();
            var ca = a.GetComponent<DrawingCanvas>(); var cb = b.GetComponent<DrawingCanvas>();
            var ra = a.GetComponent<Rigidbody>(); var rb2 = b.GetComponent<Rigidbody>();
            Check(a.GetComponent<Rune>() != null && b.GetComponent<Rune>() != null && a.GetComponent<RuneJoin>() != null, "las dos runas tienen Rune y RuneJoin");
            int compiled = 0;
            void OnCompiled(Compiler.SpellIR spell, DrawingCanvas c) { if (c == ca || c == cb) compiled++; }
            caster.SpellCompiled += OnCompiled;
            foreach (var pc in new[] { a.GetComponent<PhysicalCanvas>(), b.GetComponent<PhysicalCanvas>() }) pc.ResetToInitial();
            Quaternion flat = Quaternion.Euler(90f, 0f, 0f);
            ra.isKinematic = false; rb2.isKinematic = false;
            ra.position = spot + new Vector3(0f, 0.15f, 0f); ra.rotation = flat;
            rb2.position = spot + new Vector3(0.26f, 0.15f, 0f); rb2.rotation = flat;
            ra.linearVelocity = rb2.linearVelocity = Vector3.zero;
            ca.Clear(); cb.Clear(); caster.OnSealEdited(ca); caster.OnSealEdited(cb);
            HarnessSeals.Draw(ca, "fire"); HarnessSeals.Draw(cb, "wind-directs-air", ("column", 270f), ("column", 90f));
            caster.OnStrokeFinished(ca); caster.OnStrokeFinished(cb);
            Check(caster.GetPhase(ca) == Casting.SealPhase.Armed && caster.GetPhase(cb) == Casting.SealPhase.Armed, $"las dos runas quedan armadas (fases {caster.GetPhase(ca)} y {caster.GetPhase(cb)})");
            yield return 2.5f;
            Check(compiled >= 1, $"al tocarse se lanza el hechizo fusionado (compilaciones={compiled})");
            Check(caster.GetPhase(ca) == Casting.SealPhase.Spent && caster.GetPhase(cb) == Casting.SealPhase.Spent, $"las dos quedan gastadas (fases {caster.GetPhase(ca)} y {caster.GetPhase(cb)})");
            var wa = a.GetComponent<Rune>(); var wb = b.GetComponent<Rune>();
            Check(wa.Wear == 1 && wb.Wear == 1, $"cada una gasta un uso de desgaste (desgaste {wa.Wear} y {wb.Wear})");
            caster.SpellCompiled -= OnCompiled;
        }

        /// <summary>
        /// Sello partido para cualquier lienzo: Paper_S y Paper_M, cada uno con un ARCO del anillo (con un hueco en la costura; el
        /// sigilo repartido sobre el borde comun), planos en el piso del claro. Casos: 45 % + 55 % lado a lado separados 0,05 m, 45 % +
        /// 55 % una encima de la otra con 0,04 m de solape y 40 % + 50 % pegados SE UNEN y lanzan (ambos quedan vacios); separados
        /// 0,5 m y 40 % + 40 % (80 %, bajo el 85 %) no pasa nada. Se activa con Only = "HALVES".
        /// </summary>
        private static IEnumerator HalvesProbe(Vector3 spot)
        {
            var ga = GameObject.Find("Paper_S");
            var gb = GameObject.Find("Paper_M");
            if (ga == null || gb == null) { failures.Add("faltan Paper_S/Paper_M"); yield break; }
            var ca = ga.GetComponent<DrawingCanvas>(); var cb = gb.GetComponent<DrawingCanvas>();
            var rba = ga.GetComponent<Rigidbody>(); var rbb = gb.GetComponent<Rigidbody>();
            var pca = ga.GetComponent<PhysicalCanvas>(); var pcb = gb.GetComponent<PhysicalCanvas>();
            var caster = Object.FindFirstObjectByType<Casting.SpellCaster>();
            Check(caster.GetComponent<CanvasJoinSystem>() != null, "el SpellCaster tiene su CanvasJoinSystem");

            int compiled = 0;
            void OnCompiled(Compiler.SpellIR spell, DrawingCanvas c) { if (spell.valid && spell.active) compiled++; }
            caster.SpellCompiled += OnCompiled;

            // (nombre, apiladas, hueco entre bordes (m; negativo = solape), fraccion de A, fraccion de B, se une)
            var cases = new (string name, bool stacked, float gap, float fa, float fb, bool joins)[]
            {
                ("lado a lado, separadas 0,05 m, 45 % + 55 %", false, 0.05f, 0.45f, 0.55f, true),
                ("separadas 0,5 m", false, 0.5f, 0.45f, 0.55f, false),
                ("una encima de la otra, pegadas, 45 % + 55 %", true, 0f, 0.45f, 0.55f, true),
                ("40 % + 40 % (80 %)", false, 0f, 0.40f, 0.40f, false),
                ("40 % + 50 % (90 %)", false, 0f, 0.40f, 0.50f, true),
            };
            for (int sc = 0; sc < cases.Length; sc++)
            {
                var names = cases[sc];
                pca.ResetToInitial(); pcb.ResetToInitial();
                rba.isKinematic = true; rbb.isKinematic = true;
                Quaternion flat = Quaternion.Euler(90f, 0f, 0f);
                Vector2 sa = ca.WorldSize, sb = cb.WorldSize;
                Vector3 centerA = spot + Vector3.up * 0.3f;
                PlaceFlat(ca, centerA, flat);
                Vector3 right = ca.transform.right, up = ca.transform.up;

                float gap = names.gap;
                Vector3 edgeDir = names.stacked ? up : right;
                float halfA = (names.stacked ? sa.y : sa.x) * 0.5f, halfB = (names.stacked ? sb.y : sb.x) * 0.5f;
                PlaceFlat(cb, centerA + edgeDir * (halfA + halfB + gap), flat);
                Vector3 edgeMid = centerA + edgeDir * halfA;
                Physics.SyncTransforms();

                // Un sello entero en un cuadrado unitario, repartido a cada lado del borde comun segun donde caiga cada punto.
                HarnessSeals.Draw(ca, "fire");
                var full = new List<Core.Stroke>(ca.Session.Strokes);
                Core.Stroke ringStroke = full[full.Count - 1]; // Draw agrega el anillo al final
                full.RemoveAt(full.Count - 1);
                ca.Clear(); cb.Clear();
                caster.OnSealEdited(ca); caster.OnSealEdited(cb);
                float radius = 0.45f * Mathf.Min(Mathf.Min(sa.x, sa.y), Mathf.Min(sb.x, sb.y));
                SplitSeal(ca, cb, full, edgeMid, edgeDir, edgeDir * gap, right, up, radius / HarnessSeals.RingRadius);
                // Arcos del anillo alrededor del punto medio de la costura, con 4 grados de hueco en cada costura.
                Vector3 ringCenter = edgeMid + edgeDir * (gap * 0.5f);
                float edgeAngle = Mathf.Atan2(Vector3.Dot(edgeDir, up), Vector3.Dot(edgeDir, right)) * Mathf.Rad2Deg;
                AddRingArc(ca, ringStroke, ringCenter, right, up, radius, edgeAngle + 180f, names.fa * 360f - 8f);
                AddRingArc(cb, ringStroke, ringCenter, right, up, radius, edgeAngle, names.fb * 360f - 8f);
                ca.Redraw(); cb.Redraw();

                compiled = 0;
                caster.OnStrokeFinished(ca);
                caster.OnStrokeFinished(cb);
                Check(compiled == 0, $"mitades [{names.name}]: una mitad sola no lanza nada (lanzados={compiled})");
                yield return 2.5f;

                if (!names.joins)
                    Check(compiled == 0 && !ca.Session.IsEmpty && !cb.Session.IsEmpty,
                        $"mitades [{names.name}]: no se unen, no pasa nada (lanzados={compiled}, trazos A={ca.Session.Strokes.Count} B={cb.Session.Strokes.Count})");
                else
                    Check(compiled >= 1 && ca.Session.IsEmpty && cb.Session.IsEmpty,
                        $"mitades [{names.name}]: se unen, lanzan y las dos quedan vacias (lanzados={compiled}, trazos A={ca.Session.Strokes.Count} B={cb.Session.Strokes.Count})");

                ca.Clear(); cb.Clear();
                caster.OnSealEdited(ca); caster.OnSealEdited(cb);
                Object.FindFirstObjectByType<Reactions.MagicWorld>()?.ResetWorld();
                yield return 0.3f;
            }
            caster.SpellCompiled -= OnCompiled;
            rba.isKinematic = false; rbb.isKinematic = false;
            pca.ResetToInitial(); pcb.ResetToInitial();
        }

        /// <summary>
        /// Portales (v2): tres portales de prueba con los ids reales (tower-study = A con la placa Paper_S, watchtower = B sin placa,
        /// field-circle = C con la placa Paper_M), cada uno con su volumen de cruce. Una identidad equivocada o la propia no abren;
        /// la de B (con un signo de mas) escrita en A abre la ruta A-B (Charging, Open; B abierto de vuelta); cruzar el trigger viaja,
        /// estar cerca sin cruzar no; B devuelve a A; abrir C -> B cancela A -> B; y al vencer el tiempo (OpenSecondsOverride) se cierra.
        /// Se activa con Only = "PORTAL".
        /// </summary>
        private static IEnumerator PortalProbe(Vector3 spot)
        {
            var goA = GameObject.Find("Paper_S");
            var goC = GameObject.Find("Paper_M");
            var player = GameObject.Find("Player");
            if (goA == null || goC == null || player == null) { failures.Add("faltan Paper_S/Paper_M/Player"); yield break; }
            var rbA = goA.GetComponent<Rigidbody>(); var rbC = goC.GetComponent<Rigidbody>();
            var pcA = goA.GetComponent<PhysicalCanvas>(); var pcC = goC.GetComponent<PhysicalCanvas>();
            var canvasA = goA.GetComponent<DrawingCanvas>(); var canvasC = goC.GetComponent<DrawingCanvas>();
            var playerBody = player.GetComponent<Rigidbody>();
            var caster = Object.FindFirstObjectByType<Casting.SpellCaster>();
            Place(pcA, rbA, spot + new Vector3(0f, 1.2f, 0f)); rbA.isKinematic = true;
            goA.transform.SetPositionAndRotation(spot + new Vector3(0f, 1.2f, 0f), Quaternion.identity);
            Place(pcC, rbC, spot + new Vector3(2f, 1.2f, 0f)); rbC.isKinematic = true;
            goC.transform.SetPositionAndRotation(spot + new Vector3(2f, 1.2f, 0f), Quaternion.identity);

            var recipes = Casting.PortalRecipes.LoadDefault();
            Portal.SetRecipes(recipes);
            var defA = recipes.Find("tower-study"); var defB = recipes.Find("watchtower"); var defC = recipes.Find("field-circle");
            Check(defA != null && defB != null && defC != null, "portals.json (v2) tiene tower-study, watchtower y field-circle");
            if (defA == null || defB == null || defC == null) yield break;

            // Los portales reales del mapa comparten id con los de prueba: se apagan durante la prueba y se reactivan al final.
            var reals = new List<Portal>();
            foreach (string rid in new[] { "tower-study", "watchtower", "field-circle" })
            {
                var real = Portal.Find(rid);
                if (real != null) { reals.Add(real); real.gameObject.SetActive(false); }
            }

            var created = new List<GameObject>();
            Portal MakePortal(string id, Vector3 center, DrawingCanvas plate, out Transform arrival)
            {
                var root = new GameObject("PortalProbe_" + id); created.Add(root);
                var anchor = new GameObject("Anchor").transform; anchor.SetParent(root.transform); anchor.position = center;
                arrival = new GameObject("Arrival").transform; arrival.SetParent(root.transform);
                arrival.position = center + new Vector3(0f, 0.3f, -3f);
                arrival.rotation = Quaternion.LookRotation(Vector3.back);
                var volume = new GameObject("CrossTrigger"); volume.transform.SetParent(root.transform);
                volume.transform.position = center + Vector3.up * 1.25f;
                var trigger = volume.AddComponent<BoxCollider>();
                trigger.size = new Vector3(2f, 2.5f, 0.6f);
                var portal = root.AddComponent<Portal>();
                portal.Configure(id, plate, arrival, anchor, trigger);
                return portal;
            }
            var pA = MakePortal("tower-study", spot + new Vector3(0f, 0f, 6f), canvasA, out Transform arrivalA);
            var pB = MakePortal("watchtower", spot + new Vector3(30f, 0f, 6f), null, out Transform arrivalB);
            var pC = MakePortal("field-circle", spot + new Vector3(-30f, 0f, 6f), canvasC, out Transform arrivalC);
            Check(Portal.Find("tower-study") == pA && pA.State == PortalState.Dormant && pA.Identity.Sigil == defA.Identity.Sigil && pA.Destination == null,
                "portal A registrado, dormido, con su identidad y sin destino");

            int traveled = 0, routes = 0, compiledOnPlate = 0;
            Portal lastFrom = null, lastTo = null;
            void OnTraveled(Portal from, Portal to) { traveled++; lastFrom = from; lastTo = to; }
            void OnRoute(Portal from, Portal to) { if (from == pA && to == pB) routes++; }
            void OnCompiled(Compiler.SpellIR spell, DrawingCanvas c) { if (c == canvasA) compiledOnPlate++; }
            Portal.Traveled += OnTraveled; Portal.RouteOpened += OnRoute;
            caster.SpellCompiled += OnCompiled;
            Vector3 playerStart = player.transform.position;

            void Draw(DrawingCanvas canvas, string sigil, IList<string> signIds)
            {
                var signs = new (string, float)[signIds.Count];
                for (int i = 0; i < signIds.Count; i++) signs[i] = (signIds[i], 90f + i * 360f / signIds.Count);
                canvas.Clear(); caster.OnSealEdited(canvas);
                HarnessSeals.Draw(canvas, sigil, signs);
                caster.OnStrokeFinished(canvas);
            }
            void DrawIdentity(DrawingCanvas canvas, Casting.PortalIdentity identity, string extraSign)
            {
                var ids = new List<string>(identity.Signs);
                if (extraSign != null) ids.Add(extraSign);
                Draw(canvas, identity.Sigil, ids);
            }
            void PutPlayer(Vector3 at)
            {
                playerBody.linearVelocity = Vector3.zero;
                playerBody.position = at;
                player.transform.position = at;
                Physics.SyncTransforms();
            }
            void ResetSpells() => Object.FindFirstObjectByType<Reactions.MagicWorld>()?.ResetWorld();

            // 1) una identidad incorrecta (sigilo de B con los signos de C): no abre
            Draw(canvasA, defB.Identity.Sigil, defC.Identity.Signs);
            yield return 0.4f;
            Check(pA.State == PortalState.Dormant && pA.Destination == null, $"portal: una identidad incorrecta no abre (estado={pA.State})");
            ResetSpells();

            // 2) la identidad del propio portal: no abre (avisa "Ya estas en ese portal")
            DrawIdentity(canvasA, defA.Identity, null);
            yield return 0.4f;
            Check(pA.State == PortalState.Dormant, $"portal: la identidad del propio portal no abre (estado={pA.State})");
            ResetSpells();

            // 3) la identidad de B con un signo de mas abre la ruta A -> B
            compiledOnPlate = 0;
            DrawIdentity(canvasA, defB.Identity, "weave");
            yield return 0.4f;
            Check(pA.State == PortalState.Charging && pA.Progress01 > 0f && pA.DestinationId == "watchtower",
                $"portal: la identidad de B (+1 signo) carga A hacia B (estado={pA.State}, destino={pA.DestinationId})");
            yield return 1.6f;
            Check(pA.IsOpen && pB.IsOpen && pA.DestinationId == "watchtower" && pB.DestinationId == "tower-study",
                $"portal: A abierto hacia B y B abierto de vuelta (A={pA.State}/{pA.DestinationId}, B={pB.State}/{pB.DestinationId})");
            Check(canvasA.Session.IsEmpty && compiledOnPlate == 0 && routes == 1,
                $"portal: la placa queda vacia, no lanza hechizo y se publica RouteOpened (trazos={canvasA.Session.Strokes.Count}, hechizos={compiledOnPlate}, rutas={routes})");
            Check(pA.OpenSecondsLeft > 20f && pA.OpenSecondsLeft <= 30f, $"portal: queda abierto ~30 s (quedan {pA.OpenSecondsLeft:F1} s)");

            // 4) cerca pero fuera del volumen de cruce: no viaja; dentro del volumen: viaja a la llegada de B
            PutPlayer(pA.EffectAnchor.position + new Vector3(0f, 1f, 1.8f));
            yield return 0.5f;
            Check(traveled == 0, $"portal: cerca del portal pero sin cruzarlo no viaja (viajes={traveled})");
            PutPlayer(pA.EffectAnchor.position + new Vector3(0f, 1f, 0f));
            yield return 0.5f;
            float distB = Vector3.Distance(player.transform.position, arrivalB.position);
            Check(distB < 2f && traveled == 1 && lastFrom == pA && lastTo == pB, $"portal: cruzar A lleva a la llegada de B (distancia={distB:F2} m, viajes={traveled})");

            // 5) la vuelta: cruzar B (abierto de vuelta) lleva a la llegada de A (tras el enfriamiento del viajero)
            yield return 2.2f;
            PutPlayer(pB.EffectAnchor.position + new Vector3(0f, 1f, 0f));
            yield return 0.5f;
            float distA = Vector3.Distance(player.transform.position, arrivalA.position);
            Check(distA < 2f && traveled == 2 && lastFrom == pB && lastTo == pA, $"portal: cruzar B vuelve a la llegada de A (distancia={distA:F2} m, viajes={traveled})");

            // 6) abrir C -> B cancela la ruta A -> B (A se cierra) y B queda abierto de vuelta hacia C
            Portal.OpenSecondsOverride = 3f; // la duracion se fija al abrir: se acorta antes de abrir C -> B para ver el vencimiento
            DrawIdentity(canvasC, defB.Identity, null);
            yield return 0.4f;
            Check(!pA.IsOpen && pA.State != PortalState.Charging && pA.Destination == null && pC.DestinationId == "watchtower" && pB.DestinationId == "field-circle",
                $"portal: C -> B cancela A -> B (A={pA.State}, C={pC.State}/{pC.DestinationId}, B={pB.State}/{pB.DestinationId})");
            yield return 1.6f;
            Check(pC.IsOpen && pB.IsOpen && pB.DestinationId == "field-circle", $"portal: C y B abiertos entre si (C={pC.State}, B={pB.State})");

            // 7) al vencer el tiempo (acelerado) se cierran los dos y vuelven a Dormant tras el enfriamiento corto
            yield return 3.2f;
            Check(!pC.IsOpen && !pB.IsOpen && pC.Destination == null && pB.Destination == null, $"portal: al vencer el tiempo se cierran (C={pC.State}, B={pB.State})");
            yield return 3.3f;
            Check(pC.State == PortalState.Dormant && pB.State == PortalState.Dormant, $"portal: vuelven a Dormant (C={pC.State}, B={pB.State})");

            Portal.OpenSecondsOverride = 0f;
            Portal.Traveled -= OnTraveled; Portal.RouteOpened -= OnRoute;
            caster.SpellCompiled -= OnCompiled;
            PutPlayer(playerStart);
            foreach (var g in created) Object.Destroy(g);
            Portal.SetRecipes(null);
            foreach (var real in reals) if (real != null) real.gameObject.SetActive(true);
            ResetSpells();
            canvasA.Clear(); caster.OnSealEdited(canvasA);
            canvasC.Clear(); caster.OnSealEdited(canvasC);
            pcA.ResetToInitial(); pcC.ResetToInitial();
            rbA.isKinematic = false; rbC.isKinematic = false;
        }

        /// <summary>Apoya el lienzo con el CENTRO de su superficie (no el pivote) en center.</summary>
        private static void PlaceFlat(DrawingCanvas canvas, Vector3 center, Quaternion rotation)
        {
            canvas.transform.rotation = rotation;
            canvas.transform.position += center - canvas.WorldCenter;
        }

        /// <summary>
        /// Agrega a canvas un arco del anillo (en el plano right/up, centro y radio en metros del mundo) de span grados centrado en
        /// el angulo centerAngle, con el ancho y la tinta del trazo de anillo original.
        /// </summary>
        private static void AddRingArc(DrawingCanvas canvas, Core.Stroke template, Vector3 center, Vector3 right, Vector3 up,
            float radius, float centerAngle, float span)
        {
            var arc = new Core.Stroke { Width = template.Width, Ink = template.Ink };
            int n = Mathf.Max(8, Mathf.CeilToInt(span / 4f) + 1);
            for (int i = 0; i < n; i++)
            {
                float angle = (centerAngle - span * 0.5f + span * i / (n - 1)) * Mathf.Deg2Rad;
                Vector3 world = center + right * (Mathf.Cos(angle) * radius) + up * (Mathf.Sin(angle) * radius);
                arc.AddPoint(canvas.GetUVFromWorldPosition(world));
            }
            canvas.Session.AddStroke(arc);
        }

        /// <summary>
        /// Reparte los trazos de un sello (UV de un cuadrado unitario centrado en edgeMid, de lado scale metros, ejes right/up) entre
        /// A (lado negativo de edgeDir) y B (lado positivo, corrido shiftB). Un trazo que cruza el borde se corta en trozos.
        /// </summary>
        private static void SplitSeal(DrawingCanvas ca, DrawingCanvas cb, List<Core.Stroke> full, Vector3 edgeMid, Vector3 edgeDir,
            Vector3 shiftB, Vector3 right, Vector3 up, float scale)
        {
            foreach (var source in full)
            {
                Core.Stroke current = null;
                bool currentIsA = false;
                foreach (var p in source.Points)
                {
                    Vector3 world = edgeMid + right * ((p.x - 0.5f) * scale) + up * ((p.y - 0.5f) * scale);
                    bool isA = Vector3.Dot(world - edgeMid, edgeDir) < 0f;
                    if (current == null || isA != currentIsA)
                    {
                        current = new Core.Stroke { Width = source.Width, Ink = source.Ink };
                        currentIsA = isA;
                        (isA ? ca : cb).Session.AddStroke(current);
                    }
                    current.AddPoint(isA ? ca.GetUVFromWorldPosition(world) : cb.GetUVFromWorldPosition(world + shiftB));
                }
            }
        }

        /// <summary>
        /// Hechizos del bloque 3: lanza tierra (roca), cristal, agua y viento y revisa sus cuerpos fisicos y que no haya errores en consola.
        /// Se activa con Only = "SPELLS".
        /// </summary>
        private static IEnumerator SpellsProbe(Vector3 spot)
        {
            var go = GameObject.Find("Paper_S");
            var rb = go.GetComponent<Rigidbody>();
            var pc = go.GetComponent<PhysicalCanvas>();
            var canvas = go.GetComponent<DrawingCanvas>();
            var caster = Object.FindFirstObjectByType<Casting.SpellCaster>();
            caster.GetType(); // asegura referencia
            var crate = GameObject.Find("Crate_1");

            // Todos sobre el claro: el papel de pie mirando hacia el jugador (+Z -> la normal sale hacia -Z).
            foreach (var sigil in new[] { "earth", "crystal", "water", "wind" })
            {
                Place(pc, rb, spot + new Vector3(0f, 1.2f, 0f));
                rb.isKinematic = true;
                go.transform.SetPositionAndRotation(spot + new Vector3(0f, 1.2f, 0f), Quaternion.identity);
                if (crate != null)
                {
                    var crb = crate.GetComponent<Rigidbody>();
                    if (crb != null) { crb.isKinematic = false; crb.position = spot + new Vector3(0.6f, 1.0f, 0f); crb.linearVelocity = Vector3.zero; }
                }
                yield return 0.3f;
                var spell = sigil == "wind" ? HarnessSeals.Cast("Paper_S", "wind-directs-air", ("column", 270f), ("column", 90f)) : HarnessSeals.Cast("Paper_S", sigil);
                Check(spell.valid && spell.active, $"{sigil}: el sello se compila y esta activo (estado: {spell.status})");
                yield return 2.5f;
                switch (sigil)
                {
                    case "earth":
                        Check(Object.FindObjectsByType<ExpiringBody>(FindObjectsSortMode.None).Length > 0, "tierra: se creo una roca (ExpiringBody)");
                        break;
                    case "crystal":
                        Check(Object.FindObjectsByType<Crystallized>(FindObjectsSortMode.None).Length > 0, "cristal: algo quedo cristalizado");
                        break;
                    case "water":
                        Check(Object.FindObjectsByType<Reactions.WaterDropSink>(FindObjectsSortMode.None).Length > 0, "agua: las gotas tienen su WaterDropSink");
                        break;
                    case "wind":
                        Check(Object.FindObjectsByType<Output.WindCone>(FindObjectsSortMode.None).Length > 0, "viento: el hechizo tiene su WindCone");
                        break;
                }
                Object.FindFirstObjectByType<Reactions.MagicWorld>()?.ResetWorld();
                yield return 0.3f;
            }
        }

        /// <summary>
        /// Sellos compuestos (circulos dentro de un gran circulo): exterior con 2 interiores cerrados (fuego y viento) lanza 2
        /// sub-hechizos a la vez; con UN solo interior se lee como un sello normal; con interiores abiertos no hay compuesto;
        /// cerrando el exterior con un trazo, el compuesto carga (suma de retardos) y despues lanza. Se activa con Only = "NESTED".
        /// </summary>
        private static IEnumerator NestedProbe(Vector3 spot)
        {
            var go = GameObject.Find("Paper_S");
            if (go == null) { failures.Add("falta Paper_S"); yield break; }
            var rb = go.GetComponent<Rigidbody>();
            var pc = go.GetComponent<PhysicalCanvas>();
            var canvas = go.GetComponent<DrawingCanvas>();
            var caster = Object.FindFirstObjectByType<Casting.SpellCaster>();
            Place(pc, rb, spot + new Vector3(0f, 1.2f, 0f));
            rb.isKinematic = true;
            go.transform.SetPositionAndRotation(spot + new Vector3(0f, 1.2f, 0f), Quaternion.identity);

            int compiled = 0, nestedEvents = 0, nestedLaunched = 0, charging = 0;
            float chargeSeconds = 0f;
            void OnCompiled(Compiler.SpellIR spell, DrawingCanvas c) { if (c == canvas) compiled++; }
            void OnNested(DrawingCanvas c, int launched) { if (c == canvas) { nestedEvents++; nestedLaunched = launched; } }
            void OnCharging(Casting.SealChargeInfo info) { if (info.canvas == canvas) { charging++; chargeSeconds = info.seconds; } }
            caster.SpellCompiled += OnCompiled; caster.NestedCompiled += OnNested; caster.SealCharging += OnCharging;

            var fire = ("fire", new (string, float)[0], new Vector2(-0.2f, 0f), 0.17f);
            var wind = ("wind-directs-air", new[] { ("column", 270f), ("column", 90f) }, new Vector2(0.2f, 0f), 0.17f);

            // 1) dos interiores cerrados: 2 sub-hechizos en el mismo cast
            canvas.Clear(); caster.OnSealEdited(canvas);
            HarnessSeals.DrawNested(canvas, 0.46f, fire, wind);
            var spell = caster.Cast(canvas);
            Check(nestedEvents == 1 && nestedLaunched == 2 && compiled == 2,
                $"compuesto: salen 2 sub-hechizos a la vez (eventos compuestos={nestedEvents}, lanzados={nestedLaunched}, SpellCompiled={compiled}, estado={spell.status})");
            yield return 2.5f;
            Object.FindFirstObjectByType<Reactions.MagicWorld>()?.ResetWorld();
            yield return 0.3f;

            // 2) un solo interior: se lee como un sello normal
            compiled = 0; nestedEvents = 0;
            canvas.Clear(); caster.OnSealEdited(canvas);
            HarnessSeals.DrawNested(canvas, 0.46f, ("fire", new (string, float)[0], Vector2.zero, 0.2f));
            caster.Cast(canvas);
            Check(nestedEvents == 0 && compiled <= 1, $"un solo interior: lectura normal, sin compuesto (eventos compuestos={nestedEvents}, SpellCompiled={compiled})");
            Object.FindFirstObjectByType<Reactions.MagicWorld>()?.ResetWorld();
            yield return 0.3f;

            // 3) interiores abiertos: no hay compuesto
            compiled = 0; nestedEvents = 0;
            canvas.Clear(); caster.OnSealEdited(canvas);
            HarnessSeals.DrawNested(canvas, 0.46f, 0.7f, fire, wind);
            caster.Cast(canvas);
            Check(nestedEvents == 0, $"interiores abiertos: no se lanza un compuesto (eventos compuestos={nestedEvents}, SpellCompiled={compiled})");
            Object.FindFirstObjectByType<Reactions.MagicWorld>()?.ResetWorld();
            yield return 0.3f;

            // 4) cerrar el exterior con un trazo: carga (suma de los interiores, tope 4 s) y despues lanza el compuesto
            compiled = 0; nestedEvents = 0; charging = 0;
            canvas.Clear(); caster.OnSealEdited(canvas);
            HarnessSeals.DrawNested(canvas, 0.46f, fire, wind);
            caster.OnStrokeFinished(canvas);
            Check(charging == 1 && chargeSeconds <= Casting.SealCharge.MaxNestedSeconds + 1e-3f && nestedEvents == 0,
                $"compuesto: cerrar el exterior empieza la carga (cargas={charging}, segundos={chargeSeconds:F2})");
            yield return chargeSeconds + 0.5f;
            Check(nestedEvents == 1 && nestedLaunched == 2, $"compuesto: al terminar la carga salen los 2 (eventos={nestedEvents}, lanzados={nestedLaunched})");

            caster.SpellCompiled -= OnCompiled; caster.NestedCompiled -= OnNested; caster.SealCharging -= OnCharging;
            Object.FindFirstObjectByType<Reactions.MagicWorld>()?.ResetWorld();
            canvas.Clear(); caster.OnSealEdited(canvas);
        }

        /// <summary>
        /// Retardo de lanzamiento: un sello cerrado en una hoja carga (brilla) y despues lanza; si se borra durante la carga, se cancela;
        /// un trazo nuevo reinicia la carga. Se activa con Only = "CHARGE".
        /// </summary>
        private static IEnumerator ChargeProbe()
        {
            var go = GameObject.Find("Paper_S");
            var canvas = go.GetComponent<DrawingCanvas>();
            var caster = Object.FindFirstObjectByType<Casting.SpellCaster>();
            Check(caster.GetComponent<Output.SealGlowDirector>() != null, "el SpellCaster tiene su SealGlowDirector");
            int compiled = 0, charging = 0, endedFired = 0, endedCancelled = 0;
            void OnCompiled(Compiler.SpellIR spell, DrawingCanvas c) { if (c == canvas) compiled++; }
            void OnCharging(Casting.SealChargeInfo info) { if (info.canvas == canvas) charging++; }
            void OnEnded(DrawingCanvas c, bool fired) { if (c == canvas) { if (fired) endedFired++; else endedCancelled++; } }
            caster.SpellCompiled += OnCompiled; caster.SealCharging += OnCharging; caster.SealChargeEnded += OnEnded;

            // 1) cerrar el anillo: carga y luego lanza
            canvas.Clear(); caster.OnSealEdited(canvas);
            HarnessSeals.Draw(canvas, "fire");
            caster.OnStrokeFinished(canvas);
            Check(caster.IsCharging(canvas) && charging == 1, "cerrar el anillo empieza la carga (evento SealCharging)");
            Check(compiled == 0, "durante la carga todavia no se lanzo");
            yield return 2.6f;
            Check(!caster.IsCharging(canvas) && endedFired == 1 && compiled >= 1, $"al terminar la carga se lanza el hechizo (lanzados={compiled}, fin con disparo={endedFired})");

            // 2) borrar durante la carga cancela
            compiled = 0; charging = 0; endedFired = 0; endedCancelled = 0;
            canvas.Clear(); caster.OnSealEdited(canvas);
            HarnessSeals.Draw(canvas, "fire");
            caster.OnStrokeFinished(canvas);
            yield return 0.2f;
            canvas.Clear(); caster.OnSealEdited(canvas);
            yield return 2.6f;
            Check(compiled == 0 && endedCancelled >= 1 && !caster.IsCharging(canvas), $"borrar durante la carga la cancela (lanzados={compiled}, cancelaciones={endedCancelled})");

            caster.SpellCompiled -= OnCompiled; caster.SealCharging -= OnCharging; caster.SealChargeEnded -= OnEnded;
            canvas.Clear(); caster.OnSealEdited(canvas);
        }

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
