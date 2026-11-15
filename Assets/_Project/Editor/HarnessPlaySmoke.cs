using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using SpellSimulator.Canvas;
using SpellSimulator.Lab;
using SpellSimulator.Learning;
using SpellSimulator.Player;
using SpellSimulator.Reactions;
using SpellSimulator.World;

namespace SpellSimulator.EditorTools
{
    /// <summary>
    /// Test de humo de Play Mode (un solo comando, sin mouse ni teclado): entra a Play en la escena abierta (Pradera),
    /// recorre lo esencial del juego (inventario de dos manos, cuaderno, grimorio, reinicio del mundo, fuego, Laboratorio,
    /// ausencia de errores en consola) y escribe el resultado en Logs/harness-playsmoke.txt ("RUNNING", "PASS ..." o "FAIL ...").
    /// Uso por MCP: HarnessPlaySmoke.Run(); despues leer el archivo hasta que no diga RUNNING (~20 s: el bloque del Laboratorio espera varios frames).
    /// Vive en el asmdef de Editor pero corre dentro del Play del editor (sobrevive a la recarga de dominio via SessionState).
    /// </summary>
    public static class HarnessPlaySmoke
    {
        private const string FlagKey = "HarnessPlaySmoke.Running";
        private const string StartFrameKey = "HarnessPlaySmoke.StartFrame";
        private const int WarmupFrames = 40;
        private static readonly string ResultPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "harness-playsmoke.txt"));

        private static readonly List<string> failures = new List<string>();
        private static readonly StringBuilder passed = new StringBuilder();
        private static int errorLogs;

        // Bloque del Laboratorio (varios frames) y su registro CSV redirigido a Logs/.
        private const float LabTimeoutSeconds = 30f;
        private static readonly string LabLogPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "harness-lab-registro.csv"));
        private static IEnumerator labRoutine;
        private static float labDeadline;

        [MenuItem("SpellSimulator/Harness/Run Play Smoke")]
        public static void Run()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();   // sin cartel de guardar escena al entrar a Play
            File.WriteAllText(ResultPath, "RUNNING");
            SessionState.SetBool(FlagKey, true);
            SessionState.SetInt(StartFrameKey, -1);
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void Hook() => EditorApplication.update += Tick;

        private static void Tick()
        {
            if (labRoutine != null)
            {
                StepLab();
                return;
            }
            if (!SessionState.GetBool(FlagKey, false) || !EditorApplication.isPlaying) return;
            int start = SessionState.GetInt(StartFrameKey, -1);
            if (start < 0)
            {
                start = Time.frameCount;
                SessionState.SetInt(StartFrameKey, start);
                Application.logMessageReceived += OnLog;
                return;
            }
            if (Time.frameCount - start < WarmupFrames) return;

            SessionState.SetBool(FlagKey, false);
            failures.Clear();
            passed.Clear();
            try { Checks(); }
            catch (System.Exception e) { failures.Add("excepcion en el test: " + e.Message); }
            // El bloque del Laboratorio necesita varios frames (enfriamientos, agrupado del registro, Destroy diferido): sigue
            // en los proximos ticks y recien despues se escribe el resultado.
            labRoutine = LabChecks();
            labDeadline = Time.realtimeSinceStartup + LabTimeoutSeconds;
        }

        /// <summary>Avanza el bloque del Laboratorio un tick; al terminar (o al agotarse el tiempo) escribe el resultado.</summary>
        private static void StepLab()
        {
            bool more;
            if (!EditorApplication.isPlaying)
            {
                failures.Add("laboratorio: se salio de Play antes de terminar");
                more = false;
            }
            else
            {
                try { more = labRoutine.MoveNext(); }
                catch (System.Exception e)
                {
                    failures.Add("excepcion en el laboratorio: " + e.Message);
                    more = false;
                }
                if (more && Time.realtimeSinceStartup > labDeadline)
                {
                    failures.Add("laboratorio: tiempo agotado");
                    more = false;
                }
            }
            if (more) return;
            labRoutine = null;
            Finish();
        }

        private static void Finish()
        {
            Application.logMessageReceived -= OnLog;
            if (errorLogs > 0) failures.Add($"{errorLogs} errores/excepciones en la consola durante la prueba");

            string result = failures.Count == 0 ? "PASS " + passed : "FAIL " + string.Join(" | ", failures) + " (ok: " + passed + ")";
            File.WriteAllText(ResultPath, result);
            Debug.Log("[PlaySmoke] " + result);
            EditorApplication.isPlaying = false;
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errorLogs++;
        }

        private static void Check(bool ok, string name)
        {
            if (ok) passed.Append(name).Append("; ");
            else failures.Add(name);
        }

        private static GameObject Find(string name) => GameObject.Find(name);

        private static void Checks()
        {
            // El registro del Laboratorio de la escena no escribe en persistentDataPath durante la prueba (los reinicios de abajo
            // agregarian filas): el bloque del Laboratorio usa uno propio redirigido a Logs/.
            var sceneLabLog = Object.FindFirstObjectByType<LabSessionLog>();
            if (sceneLabLog != null) sceneLabLog.enabled = false;

            // --- Menu principal: arranca en pantalla con la entrada cerrada; Sandbox entrega el control (el resto del
            // test asume juego activo).
            var menu = SpellSimulator.UI.MainMenu.Instance;
            Check(menu != null, "el menu principal existe");
            if (menu != null)
            {
                bool shown = SpellSimulator.UI.MainMenu.IsActive;
                var reader = Object.FindFirstObjectByType<PlayerInputReader>();
                bool gated = reader != null && reader.Gate == InputGate.None;
                menu.EnterSandbox();
                bool handed = !SpellSimulator.UI.MainMenu.IsActive && reader != null && reader.Gate == InputGate.Full;
                Check(shown && gated && handed, "el menu arranca activo y Sandbox entrega el control");
            }

            var player = Find("Player");
            Check(player != null, "jugador");
            if (player == null) return;
            var interactor = player.GetComponent<PlayerInteractor>();
            Check(interactor != null, "interactor");
            if (interactor == null) return;

            // --- Inventario de dos manos
            var pen = Find("Pen");
            var eraser = Find("Eraser");
            if (pen != null && eraser != null)
            {
                interactor.GrabItem(pen);
                interactor.GrabItem(eraser);
                interactor.SelectHotbar(0);
                Check(interactor.MainItem == pen, "lapiz equipado en la barra");
                Check(interactor.HotbarItem(1) == eraser, "goma en el slot 2");
            }
            else failures.Add("faltan Pen o Eraser en la escena");

            var notebook = Find("Notebook_A");
            if (notebook != null)
            {
                interactor.GrabItem(notebook);
                Check(interactor.SecondaryItem == notebook, "cuaderno en la mano secundaria (arrastrado)");
                var pages = notebook.GetComponent<NotebookPages>();
                Check(pages != null, "cuaderno con paginas");
                if (pages != null)
                {
                    pages.Turn(1);
                    Check(pages.Page == 1, "pasar de pagina");
                    pages.Turn(-1);
                }
                var mover = player.GetComponent<ObjectMover>();
                if (mover != null) mover.End(false);
                Check(interactor.SecondaryItem == null, "soltar lo arrastrado");
            }

            // --- Dibujo y reinicio del mundo
            var paper = Find("Paper_S");
            var world = Object.FindFirstObjectByType<MagicWorld>();
            if (paper != null && world != null)
            {
                var canvas = paper.GetComponent<DrawingCanvas>();
                HarnessSeals.Draw(canvas, "water");
                Check(canvas.Session.Strokes.Count > 0, "dibujar un sello");
                world.ResetWorld();
                Check(canvas.Session.Strokes.Count == 0, "el reinicio borra los lienzos");
            }
            else failures.Add("faltan Paper_S o MagicWorld");

            // --- Fuego del pasto
            var grass = Object.FindFirstObjectByType<GrassFire>();
            if (grass != null)
            {
                grass.Ignite(new Vector3(14f, 0f, -2f), 2f);
                Check(grass.BurningCells > 0, "el fuego prende el pasto");
                if (world != null) world.ResetWorld();
                Check(grass.BurningCells == 0 && grass.BurntCells == 0, "el reinicio apaga el pasto");
            }

            // --- Grimorio
            var book = Grimoire.Instance;
            if (book != null)
            {
                book.Open();
                Check(book.IsOpen, "abrir el grimorio");
                int buttons = book.GetComponentsInChildren<GrimoireButton>(false).Length;
                Check(buttons > 0, "el grimorio tiene botones");
                foreach (var b in book.GetComponentsInChildren<GrimoireButton>(false))
                {
                    if (b.GetPressPrompt().StartsWith("Glifos")) { b.Press(); break; }
                }
                book.Close();
                Check(!book.IsOpen, "cerrar el grimorio");
            }
            else failures.Add("falta el grimorio");

            // --- Puerta de la torre: arranca abierta, E la cierra (y anima), el reinicio la deja abierta.
            var doorGo = Find("TowerDoor");
            var door = doorGo != null ? doorGo.GetComponent<TowerDoor>() : null;
            Check(door != null, "la puerta de la torre existe");
            if (door != null)
            {
                Check(door.IsOpen, "la puerta arranca abierta");
                door.Press();
                bool closing = !door.IsOpen && door.IsMoving && door.GetPressPrompt() == "Abrir puerta";
                door.Press();
                bool reopening = door.IsOpen;
                door.Press();
                if (world != null) world.ResetWorld();
                Check(closing && reopening && door.IsOpen && !door.IsMoving, "la puerta alterna y el reinicio la deja abierta");
            }

            // --- Runas de material (bloque 4): la de metal del campo y la de oro del estudio, con su Rune y sus grietas.
            var metalGo = Find("ImpactMetal_M");
            var goldGo = Find("ImpactGold_S");
            var metalRune = metalGo != null ? metalGo.GetComponent<Rune>() : null;
            var goldRune = goldGo != null ? goldGo.GetComponent<Rune>() : null;
            Check(metalRune != null && metalRune.Kind == RuneKind.Metal && goldRune != null && goldRune.Kind == RuneKind.Gold
                && metalGo.GetComponent<RuneVisuals>() != null && goldGo.GetComponent<RuneVisuals>() != null, "runas de metal y oro con su tipo");

            // --- Fijar en el aire un objeto generico (banco, mesa...) y poder desfijarlo al volver a agarrarlo.
            var pinMover = player != null ? player.GetComponent<ObjectMover>() : null;
            if (pinMover != null)
            {
                var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                probe.transform.position = new Vector3(0f, 50f, 0f);
                var probeRb = probe.AddComponent<Rigidbody>();
                pinMover.TryBegin(probeRb, 2f);
                pinMover.End(true);
                bool pinned = probeRb.isKinematic && probe.GetComponent<PinnedBody>() != null;
                pinMover.TryBegin(probeRb, 2f);
                pinMover.End(false);
                Check(pinned && !probeRb.isKinematic && probeRb.useGravity, "un objeto generico fijado en el aire se puede desfijar");
                Object.DestroyImmediate(probe);
            }

            // --- Cinturon: suma 2 slots chicos (teclas 4 y 5) y la HUD los muestra.
            var hudCtl = Object.FindFirstObjectByType<SpellSimulator.UI.HudController>();
            if (hudCtl != null && interactor != null)
            {
                int before = hudCtl.SlotsShown;
                interactor.SetBelt(true);
                int withBelt = hudCtl.SlotsShown;
                int count5 = interactor.SlotCount;
                interactor.SetBelt(false);
                Check(before == 3 && count5 == 3 && interactor.SlotCount == 3, "el cinturon ya no cambia los 3 slots de la barra");
                Check(withBelt >= 3, "la HUD informa los slots visibles");
            }
            else failures.Add("falta HudController para el chequeo del cinturon");

            // --- HUD
            Check(Object.FindFirstObjectByType<UnityEngine.UIElements.UIDocument>() != null, "HUD UI Toolkit presente");

            // --- Slot de cartuchos: existe y su contador sigue al stock (sumar un cartucho poderoso lo cambia en el acto).
            var hud = Object.FindFirstObjectByType<SpellSimulator.UI.HudController>();
            var supply = player.GetComponent<InkSupply>();
            var hudDoc = hud != null ? hud.GetComponent<UnityEngine.UIElements.UIDocument>() : null;
            bool hasSlot = hudDoc != null && hudDoc.rootVisualElement != null && UnityEngine.UIElements.UQueryExtensions.Q(hudDoc.rootVisualElement, "cartridge") != null;
            if (hud != null && supply != null && hasSlot)
            {
                var stock = supply.Cartridges;
                // Si el stock esta lleno se gasta un cartucho entero para hacer lugar.
                stock.SetBelt(true);
                while (stock.CartridgeCount(SpellSimulator.Core.InkKind.Powerful) >= stock.MaxCartridges && stock.TryUse(out _)) { }
                int before = stock.TotalCartridges;
                bool added = stock.TryAdd(SpellSimulator.Core.InkKind.Powerful);
                Check(added && hud.CartridgeCountShown == before + 1 && hud.CartridgeCountShown == stock.TotalCartridges,
                    "el HUD muestra el slot de cartuchos y su contador cambia al sumar uno");
            }
            else failures.Add("falta el slot de cartuchos del HUD (o el InkSupply del jugador)");

            // --- Estructuras del mapa (MapStructures): los 4 portales, puntos de reaparicion y la estacion de reciclaje.
            bool portals = Portal.Find("tower-study") != null && Portal.Find("watchtower") != null
                && Portal.Find("field-circle") != null && Portal.Find("cave") != null;
            int supplyPoints = Object.FindObjectsByType<SupplyPoint>(FindObjectsSortMode.None).Length;
            bool recycling = Object.FindFirstObjectByType<RecyclingStation>() != null;
            Check(portals && supplyPoints >= 3 && recycling, "estructuras: 4 portales, 3+ puntos de reaparicion y estacion de reciclaje");

            // Portales circulares: disco atravesable (CrossTrigger es trigger) y animacion (PortalVisuals) en los cuatro.
            int crossable = 0;
            foreach (string id in new[] { "tower-study", "watchtower", "field-circle", "cave" })
            {
                var portal = Portal.Find(id);
                var cross = portal != null ? portal.transform.Find("CrossTrigger") : null;
                var trigger = cross != null ? cross.GetComponent<Collider>() : null;
                if (trigger != null && trigger.isTrigger && portal.GetComponent<PortalVisuals>() != null) crossable++;
            }
            Check(crossable == 4, $"portales: {crossable}/4 con trigger para atravesar el disco y PortalVisuals");

            // --- Tutoriales nuevos, hojas livianas, barquito estable, muros de tierra y Wall Breaker.
            Check(GameObject.Find("Struct_EarthWorkshop") != null && GameObject.Find("Struct_WaterLab") != null && GameObject.Find("Slate_WaterLab") != null,
                "tutoriales nuevos: taller de piedra y laboratorio del lago con su pizarra");
            Check(Object.FindObjectsByType<Flutter>(FindObjectsSortMode.None).Length >= 20, "tutorial de viento: muchas hojas livianas que revolotean");
            var boat = GameObject.Find("ToyBoat");
            Check(boat != null && boat.GetComponent<KeepUpright>() != null && boat.GetComponent<Rigidbody>().mass <= 0.1f, "tutorial de agua: barquito liviano que se mantiene boca arriba");

            var columns = new System.Collections.Generic.List<SpellSimulator.Compiler.SignReading>
            {
                SmokeSign("column", 0f), SmokeSign("column", 180f)
            };
            var wallSpell = new SpellSimulator.Compiler.SpellIR
            {
                element = SpellSimulator.Compiler.ElementType.Earth, inkPower = 1f, range = 0.5f, force = 0.6f,
                plan = SpellSimulator.Compiler.PlanResolver.Resolve(columns, "earth", "earth", 0.9f)
            };
            var frame = new SpellSimulator.Output.SealFrame { Origin = new Vector3(0f, 0.1f, -20f), Right = Vector3.right, Up = Vector3.forward, Normal = Vector3.up, Radius = 0.3f, Scale = 0.3f };
            int piecesBefore = SpellSimulator.Output.SolidPiece.Count;
            int pieces = SpellSimulator.Output.EarthShapes.Spawn(wallSpell, frame, null);
            Check(pieces >= 2 && SpellSimulator.Output.SolidPiece.Count == piecesBefore + pieces, $"tierra + columna levanta un muro de {pieces} piezas");
            int removed = MeshBreaker.Break(TowerWallPoint(), 1.5f, false);
            Check(removed > 0, $"Wall Breaker abre un hueco en la pared de la torre ({removed} triangulos)");
            var studyPortal = Portal.Find("tower-study");
            int safe = studyPortal != null ? MeshBreaker.Break(studyPortal.transform.position, 1.2f, false) : -1;
            Check(safe == 0, "Wall Breaker no corta cerca de un portal");
            MeshBreaker.RestoreAll();
            foreach (var piece in Object.FindObjectsByType<SpellSimulator.Output.SolidPiece>(FindObjectsSortMode.None)) Object.Destroy(piece.gameObject);
        }

        /// <summary>
        /// Bloque «Laboratorio» (Docs/LABORATORIO_PLAN.md, fases 2a y 2b): perilla, caja de 25 y de 600 kg, zona de viento,
        /// reinicio (borra lo materializado y conserva los valores), tuning del fuego y «por defecto», registro CSV redirigido a
        /// Logs/ y alternar el overlay de las lentes. Corre a lo largo de varios ticks: cada <c>yield return null</c> espera uno.
        /// </summary>
        private static IEnumerator LabChecks()
        {
            var parameters = LabParameters.Current;
            var spawner = LabSpawner.Current;
            var world = Object.FindFirstObjectByType<MagicWorld>();
            var grass = GrassFire.Current;
            Check(parameters != null && spawner != null && world != null, "laboratorio: LabParameters, LabSpawner y MagicWorld en la escena");
            if (parameters == null || spawner == null || world == null) yield break;
            spawner.ClearAll();
            parameters.ResetAll();

            // Registro propio en Logs/: overridePath se lee en Awake, por eso el componente se agrega con el objeto inactivo.
            if (File.Exists(LabLogPath)) File.Delete(LabLogPath);
            var logGo = new GameObject("HarnessLabLog");
            logGo.SetActive(false);
            var log = logGo.AddComponent<LabSessionLog>();
            var logSo = new SerializedObject(log);
            logSo.FindProperty("overridePath").stringValue = LabLogPath;
            logSo.ApplyModifiedPropertiesWithoutUndo();
            logGo.SetActive(true);
            yield return null;

            // --- Perilla: un paso de masa (default 10 -> 25 kg).
            float before = parameters.Get(LabVariableId.Mass);
            parameters.Step(LabVariableId.Mass, 1);
            Check(Mathf.Approximately(before, 10f) && Mathf.Approximately(parameters.Get(LabVariableId.Mass), 25f),
                "laboratorio: la perilla de masa sube un paso (10 -> 25 kg)");
            // El registro agrupa los pasos: la fila de la perilla sale 0,6 s despues del ultimo.
            for (float end = Time.realtimeSinceStartup + LabSessionLog.KnobGroupSeconds + 0.3f; Time.realtimeSinceStartup < end;) yield return null;

            // --- Materializar: caja con la masa de la perilla y zona de viento.
            var light = spawner.Materialize(LabSpawnKind.Box);
            var lightBody = light != null ? light.GetComponent<Rigidbody>() : null;
            Check(lightBody != null && Mathf.Approximately(lightBody.mass, 25f), "laboratorio: materializar crea una caja con Rigidbody.mass = 25");
            var wind = spawner.Materialize(LabSpawnKind.Wind);
            Check(wind != null && wind.GetComponent<LabWindZone>() != null, "laboratorio: materializar viento crea una LabWindZone");

            // Caja de 600 kg (masa maxima = Movable.maxMass), despues del enfriamiento de 1 s por boton (tiempo de juego).
            parameters.Set(LabVariableId.Mass, 600f);
            float pressedAt = Time.time;
            for (float end = Time.realtimeSinceStartup + 5f; Time.time - pressedAt < 1.2f && Time.realtimeSinceStartup < end;) yield return null;
            var heavy = spawner.Materialize(LabSpawnKind.Box);
            var heavyBody = heavy != null ? heavy.GetComponent<Rigidbody>() : null;
            Check(heavyBody != null && Mathf.Approximately(heavyBody.mass, 600f), "laboratorio: caja de 600 kg con Rigidbody.mass = 600");

            // --- Reinicio del mundo: borra lo materializado (Destroy, al final del frame) y conserva los valores de la mesa.
            world.ResetWorld();
            int resetFrame = Time.frameCount;
            while (Time.frameCount < resetFrame + 2) yield return null;
            Check(light == null && heavy == null && wind == null && Mathf.Approximately(parameters.Get(LabVariableId.Mass), 600f),
                "laboratorio: el reinicio borra lo materializado y conserva los valores");

            // --- Fuego: SetTuning se aplica en vivo y «por defecto» (ResetAll) lo restaura.
            if (grass != null)
            {
                parameters.Set(LabVariableId.FireDecay, 0.9f);
                parameters.Set(LabVariableId.FireMaxGenerations, 10f);
                bool applied = Mathf.Abs(grass.GenerationDecay - 0.9f) < 1e-3f && grass.MaxGenerations == 10;
                parameters.ResetAll();
                float defaultDecay = LabCatalog.Get(LabVariableId.FireDecay).Default;
                int defaultGenerations = Mathf.RoundToInt(LabCatalog.Get(LabVariableId.FireMaxGenerations).Default);
                bool restored = Mathf.Abs(grass.GenerationDecay - defaultDecay) < 1e-3f && grass.MaxGenerations == defaultGenerations
                    && Mathf.Approximately(parameters.Get(LabVariableId.Mass), 10f);
                Check(applied, "laboratorio: SetTuning del fuego aplicado (debilitamiento 0,90 y 10 generaciones)");
                Check(restored, "laboratorio: «por defecto» restaura el fuego (0,78 y 6) y la masa");
            }
            else failures.Add("laboratorio: falta GrassFire para el tuning del fuego");

            // --- Lentes: el overlay se prende y se apaga sin excepciones (los errores de su LateUpdate los cuenta OnLog).
            bool toggled;
            try
            {
                LabOverlay.Visible = true;
                bool on = LabOverlay.Visible;
                LabOverlay.Visible = false;
                bool off = !LabOverlay.Visible;
                LabOverlay.Visible = true;
                toggled = on && off && LabOverlay.Visible;
            }
            catch (System.Exception e)
            {
                failures.Add("laboratorio: el overlay lanzo " + e.GetType().Name + ": " + e.Message);
                toggled = false;
            }
            for (float end = Time.realtimeSinceStartup + 0.5f; Time.realtimeSinceStartup < end;) yield return null;
            LabOverlay.Visible = false;
            Check(toggled && !LabOverlay.Visible, "laboratorio: las lentes prenden y apagan el overlay");

            // --- Registro: se cierra (escribe la perilla pendiente y libera el archivo) y se cuentan las filas.
            for (float end = Time.realtimeSinceStartup + LabSessionLog.KnobGroupSeconds + 0.3f; Time.realtimeSinceStartup < end;) yield return null;
            logGo.SetActive(false);
            string[] lines = File.Exists(LabLogPath) ? File.ReadAllLines(LabLogPath) : new string[0];
            int rows = Mathf.Max(0, lines.Length - 1);
            string all = string.Join("\n", lines);
            Check(rows >= 4 && all.Contains(LabSessionLog.EventKnob) && all.Contains(LabSessionLog.EventMaterialize) && all.Contains(LabSessionLog.EventReset),
                $"laboratorio: el registro CSV en Logs/ tiene {rows} filas (perilla, materializar y reinicio)");

            Object.Destroy(logGo);
            spawner.ClearAll();
            parameters.ResetAll();
        }

        /// <summary>Un punto sobre la cara este de la pared de la torre (centro de las bounds de TowerWall, corrido al borde exterior).</summary>
        internal static Vector3 TowerWallPoint()
        {
            foreach (var breakable in Breakable.All)
            {
                if (breakable == null || breakable.name != "TowerWall") continue;
                Bounds b = breakable.Bounds;
                return new Vector3(b.max.x - 0.3f, b.min.y + 1.5f, b.center.z);
            }
            return Vector3.zero;
        }

        private static SpellSimulator.Compiler.SignReading SmokeSign(string manifestation, float bearingDeg) => new SpellSimulator.Compiler.SignReading
        {
            Id = manifestation, Manifestation = manifestation,
            At = SpellSimulator.Core.GeometryUtils.VectorFromAngleDeg(bearingDeg) * 0.8f, Length = 1f,
            Facing = SpellSimulator.Core.GeometryUtils.VectorFromAngleDeg(bearingDeg + 180f), FacingTrust = 0.9f
        };
    }
}
