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
        private const float LabTimeoutSeconds = 60f;
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

            // --- Piso 1 del tutorial: lienzo con plantilla fantasma (lineas aparte, no son trazos) y rayo de apunte
            var windPaper = Find("TutorialPaper_Wind");
            if (windPaper != null)
            {
                Transform ghost = windPaper.transform.Find("GhostTemplate");
                int ghostStrokes = ghost != null ? ghost.childCount : 0;
                bool hasAim = windPaper.transform.Find("AimGuide") != null;
                var windCanvas = windPaper.GetComponent<DrawingCanvas>();
                bool noInk = windCanvas != null && windCanvas.Session.Strokes.Count == 0;
                // Captura de la plantilla vista desde arriba (para revisarla a ojo).
                {
                    var camGo = new GameObject("smokeCam");
                    var cam = camGo.AddComponent<Camera>();
                    cam.fieldOfView = 40f;
                    cam.nearClipPlane = 0.05f;
                    var rt = new RenderTexture(900, 900, 24);
                    cam.targetTexture = rt;
                    camGo.transform.position = windPaper.transform.position + Vector3.up * 1.1f;
                    camGo.transform.LookAt(windPaper.transform.position);
                    cam.Render();
                    RenderTexture.active = rt;
                    var shot = new Texture2D(900, 900, TextureFormat.RGB24, false);
                    shot.ReadPixels(new Rect(0, 0, 900, 900), 0, 0);
                    shot.Apply();
                    System.IO.File.WriteAllBytes("Docs/Capturas/piso1_plantilla.png", shot.EncodeToPNG());
                    RenderTexture.active = null;
                    cam.targetTexture = null;
                    Object.DestroyImmediate(shot);
                    Object.DestroyImmediate(rt);
                    Object.DestroyImmediate(camGo);
                }
                Check(ghostStrokes >= 2 && hasAim && noInk, $"tutorial piso 1: el lienzo trae plantilla fantasma y rayo de apunte, sin trazos propios (trazos fantasma {ghostStrokes}, rayo {hasAim}, sin tinta {noInk})");
            }
            else failures.Add("falta TutorialPaper_Wind");

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
        /// Bloque «Laboratorio» (Docs/LABORATORIO_PLAN.md fases 2a/2b y LABORATORIO_V2_PLAN.md): paso de masa, caja de 25 y de
        /// 600 kg, zona de viento, reinicio (borra lo materializado y conserva los valores), tuning del fuego y «por defecto»,
        /// overlay de las lentes; v2: banco de sellos (SetVar/GetVar, cada receta lanzada con temblor 0, gust obligatoria),
        /// controles enlazados, un LabControl que mueve su variable, LabWindVisual con zona; registro CSV redirigido a Logs/
        /// (con la fila «sello»). Corre a lo largo de varios ticks: cada <c>yield return null</c> espera uno.
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

            // --- Laboratorio v2: banco de sellos, controles enlazados y viento visible.
            var bench = LabSealBench.Current;
            Check(bench != null && bench.Canvas != null && GameObject.Find("LabBench") != null, "laboratorio v2: banco de sellos (LabBench) con lienzo de prueba");
            if (bench != null && bench.Canvas != null)
            {
                // SetVar cambia GetVar (y deja una fila «sello» en el registro, agrupada como las perillas).
                float fill = bench.GetVar(LabSealVarId.RingFill);
                float fillTarget = fill > 0.6f ? 0.5f : 0.7f;
                bench.SetVar(LabSealVarId.RingFill, fillTarget);
                Check(Mathf.Abs(bench.GetVar(LabSealVarId.RingFill) - fillTarget) < 1e-3f, "laboratorio v2: SetVar cambia GetVar (llenado del anillo)");
                for (float end = Time.realtimeSinceStartup + LabSessionLog.KnobGroupSeconds + 0.3f; Time.realtimeSinceStartup < end;) yield return null;

                // Cada receta con temblor 0: Launch() compila valido y con el elemento de su sigilo. Solo gust es obligatoria
                // (riesgo 1: el ONNX puede no leer bien trazos sinteticos); el resto se cuenta y se reporta.
                var smokeCaster = Object.FindFirstObjectByType<SpellSimulator.Casting.SpellCaster>();
                var glyphs = smokeCaster != null ? smokeCaster.Glyphs : null;
                var recipes = new List<string>(bench.RecipeIds);
                int total = 0, valid = 0, matching = 0;
                bool gustSeen = false, gustOk = false;
                var missed = new StringBuilder();
                foreach (string id in recipes)
                {
                    if (id == LabSealBenchMath.FreeRecipeId) continue;
                    bench.ApplyRecipe(id);
                    bench.SetVar(LabSealVarId.Tremor, 0f);
                    bench.Launch();
                    total++;
                    var expected = ExpectedElement(glyphs, bench.Spec.sigilId);
                    bool ok = bench.LastCastValid && (expected == SpellSimulator.Compiler.ElementType.None || bench.LastElement == expected);
                    if (bench.LastCastValid) valid++;
                    if (ok) matching++;
                    else missed.Append(id).Append(' ');
                    if (id == "gust")
                    {
                        gustSeen = true;
                        gustOk = ok;
                    }
                    yield return null;
                }
                Check(gustSeen && gustOk, "laboratorio v2: la receta gust (temblor 0) compila valida y de viento");
                string recipeReport = $"laboratorio v2: {matching}/{total} recetas compilan con su elemento ({valid} validas)" +
                                      (missed.Length > 0 ? " [fallan: " + missed.ToString().Trim() + "]" : "");
                passed.Append(recipeReport).Append("; ");
                Debug.Log("[PlaySmoke] " + recipeReport);
                bench.ResetSpec();
                world.ResetWorld();   // borra lo que dejaron los hechizos de prueba en la pista
                int recipesFrame = Time.frameCount;
                while (Time.frameCount < recipesFrame + 2) yield return null;
            }

            // --- Laboratorio v4: papel del campo en cada atajo y en pose libre, dibujo del atril vs. sello de los controles.
            if (bench != null && bench.Canvas != null)
            {
                int posesOk = 0, presets = LabSealSpec.PaperPoseCount - 1;
                for (int pose = 0; pose < presets; pose++)
                {
                    bench.ResetSpec();
                    bench.ApplyRecipe("gust");
                    bench.SetVar(LabSealVarId.Tremor, 0f);
                    bench.SetVar(LabSealVarId.PaperPose, pose);
                    bench.Launch();
                    if (bench.LastCastValid) posesOk++;
                    yield return null;
                }
                string poseReport = $"laboratorio v4: {posesOk}/{presets} atajos del papel lanzan valido";
                passed.Append(poseReport).Append("; ");
                Debug.Log("[PlaySmoke] " + poseReport);
                Check(posesOk >= 1, "laboratorio v4: al menos un atajo del papel lanza valido");

                // Pose libre: en el piso, de canto hacia la derecha; el atajo pasa a «Personalizada» y el papel no se entierra.
                bench.SetVar(LabSealVarId.PaperHeight, 0f);
                bench.SetVar(LabSealVarId.PaperYaw, 90f);
                bench.SetVar(LabSealVarId.PaperX, 2f);
                bench.Rebuild();
                float lift = bench.Canvas.WorldCenter.y - bench.PaperGroundWorld.y;
                Check(bench.Spec.paperPose == LabSealSpec.FreePaperPose && lift > 0.1f && lift < 0.4f,
                    $"laboratorio v4: pose libre en el piso (atajo personalizado, centro a {lift:0.00} m del suelo)");

                // Atril v5: lienzo normal (la lapicera lo ve, Tab abre su vista completa, nunca se lanza solo), vista previa de los
                // controles en el atril y en el papel del campo, el primer trazo a mano la limpia y «Borrar dibujo» la repone.
                var pad = bench.SketchPad;
                Check(pad != null && pad.Canvas != null, "laboratorio v5: atril de dibujo (LabSketchPad) con lienzo");
                if (pad != null && pad.Canvas != null)
                {
                    var padCol = pad.Canvas.GetComponent<Collider>();
                    var gate = pad.Canvas.GetComponentInParent<SpellSimulator.Interfaces.ISealGate>();
                    var penTool = Object.FindFirstObjectByType<PhysicalTool>();
                    bool penSees = false;
                    if (penTool != null)
                    {
                        Vector3 normal = pad.Canvas.FaceNormal;
                        penSees = penTool.TryShootRay(new Ray(pad.Canvas.WorldCenter + normal * 0.4f, -normal), 1f, out _, out var seen) && seen == pad.Canvas;
                    }
                    Check(padCol != null && padCol.enabled && !padCol.isTrigger && pad.Canvas.GetComponent<PhysicalCanvas>() == null
                          && gate != null && gate.SealBlocked && penSees,
                        $"laboratorio v5: el atril es un lienzo normal (collider solido, no se agarra, compuerta bloqueada, la lapicera lo ve: {penSees})");
                    var cam = Camera.main;
                    bool tabView = false;
                    if (cam != null)
                    {
                        tabView = SurfaceFocus.ShowCanvas(cam.transform, pad.Canvas) && SurfaceFocus.IsActive;
                        SurfaceFocus.Close();
                        int closeFrame = Time.frameCount;
                        while (Time.frameCount < closeFrame + 2) yield return null;
                    }
                    Check(tabView, "laboratorio v5: Tab (SurfaceFocus.ShowCanvas) abre la vista completa del atril");

                    bench.ResetSpec();
                    bench.ApplyRecipe("gust");
                    bench.SetVar(LabSealVarId.Tremor, 0f);
                    bench.SetVar(LabSealVarId.PaperPose, 0f);
                    pad.Clear();
                    bench.Rebuild();
                    int fieldStrokes = bench.Canvas.Session.Strokes.Count;
                    bool previewBoth = !pad.HandDrawn && fieldStrokes > 0 && pad.Canvas.Session.Strokes.Count == fieldStrokes
                                       && !LabSketchPreview.HasHandStroke(pad.Canvas.Session.Strokes);
                    Check(previewBoth, $"laboratorio v5: vista previa de los controles en el atril y en el papel del campo ({fieldStrokes} trazos)");

                    // Dibujo a mano (como la lapicera: trazos sin dueño de vista previa) = el sello sintetico copiado.
                    var copies = new List<SpellSimulator.Core.Stroke>(fieldStrokes);
                    foreach (var stroke in bench.Canvas.Session.Strokes)
                        copies.Add(new SpellSimulator.Core.Stroke(new List<Vector2>(stroke.Points)) { Width = stroke.Width, Ink = stroke.Ink });
                    foreach (var stroke in copies) pad.Canvas.Session.AddStroke(stroke);
                    bool handClears = pad.HasDrawing && pad.Canvas.Session.Strokes.Count == copies.Count;
                    Check(handClears, "laboratorio v5: el primer trazo a mano limpia la vista previa del atril");

                    // El atril nunca se lanza solo al cerrar el anillo (compuerta): el lanzador no compila nada.
                    var padCaster = Object.FindFirstObjectByType<SpellSimulator.Casting.SpellCaster>();
                    int autoCasts = 0;
                    System.Action<SpellSimulator.Compiler.SpellIR, SpellSimulator.Canvas.DrawingCanvas> onCast = (castSpell, castCanvas) => { if (castCanvas == pad.Canvas) autoCasts++; };
                    if (padCaster != null)
                    {
                        padCaster.SpellCompiled += onCast;
                        padCaster.OnStrokeFinished(pad.Canvas);
                        for (float end = Time.realtimeSinceStartup + 0.5f; Time.realtimeSinceStartup < end;) yield return null;
                        padCaster.SpellCompiled -= onCast;
                    }
                    Check(padCaster != null && autoCasts == 0 && !padCaster.IsCharging(pad.Canvas), "laboratorio v5: el atril no se lanza solo al cerrar el anillo");

                    bench.Launch();
                    bool fromDrawing = bench.LastLaunchFromDrawing, drawingValid = bench.LastCastValid;
                    pad.Clear();
                    bench.Launch();
                    bool fromControls = !bench.LastLaunchFromDrawing;
                    bool previewBack = !pad.HandDrawn && pad.Canvas.Session.Strokes.Count > 0;
                    Check(fromDrawing && fromControls && previewBack,
                        "laboratorio v5: LANZAR usa el dibujo a mano si lo hay; «Borrar dibujo» vuelve a la vista previa y a los controles");
                    string drawReport = $"laboratorio v5: el dibujo copiado del sello gust lanza {(drawingValid ? "valido" : "INVALIDO")}";
                    passed.Append(drawReport).Append("; ");
                    Debug.Log("[PlaySmoke] " + drawReport);
                }

                // Consola v6: sin paginas ni pestañas (todas las variables a la vez) y sin las placas fijas de lentes.
                var pageSets = Object.FindObjectsByType<LabPageSet>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                bool noPages = pageSets.Length == 0 && GameObject.Find("LabLensResult") == null && GameObject.Find("LabLensPad") == null
                    && GameObject.Find("LabLensCounters") == null;
                var wingSeal = GameObject.Find("LabPanel_Seal");
                var wingField = GameObject.Find("LabPanel_Field");
                bool wingsOk = wingSeal != null && wingField != null && wingSeal.GetComponentsInChildren<LabControlBinding>(true).Length == 14
                    && wingField.GetComponentsInChildren<LabControlBinding>(true).Length == 13;
                Check(noPages && wingsOk, $"laboratorio v6: alas sin paginas (LabPageSet {pageSets.Length}), 14 controles a la izquierda y 13 a la derecha");

                // Cartel «Laboratorio» de la entrada: estetica de los carteles del mundo, de cara a la torre.
                var labSign = GameObject.Find("Sign_Lab");
                bool signFacesTower = false;
                if (labSign != null)
                {
                    Vector2 towerXZ = SpellSimulator.World.Map.ValleyShape.TowerCenter;
                    Vector3 toTower = new Vector3(towerXZ.x - labSign.transform.position.x, 0f, towerXZ.y - labSign.transform.position.z);
                    // CanvasDesigns.Sign: el texto se lee desde -Z.
                    signFacesTower = toTower.sqrMagnitude > 1f && Vector3.Dot(-labSign.transform.forward, toTower.normalized) > 0.9f;
                }
                Check(signFacesTower, "laboratorio v5: el cartel «Laboratorio» de la entrada mira hacia la torre");
                bench.ResetSpec();
                bench.SetVar(LabSealVarId.PaperPose, 0f);
                world.ResetWorld();
                int poseFrame = Time.frameCount;
                while (Time.frameCount < poseFrame + 2) yield return null;
            }

            // --- Laboratorio v4: objetos del campo (papel elegido de entrada, hasta 4 cajas, elegir y mover).
            var field = LabField.Current;
            Check(field != null && field.Selected == LabField.PaperSlot, "laboratorio v4: LabField con el papel elegido de entrada");
            if (field != null)
            {
                spawner.ClearAll();
                for (int i = 0; i < LabSpawner.MaxBoxCount + 1; i++)
                {
                    float boxAt = Time.time;
                    for (float end = Time.realtimeSinceStartup + 5f; Time.time - boxAt < 1.05f && Time.realtimeSinceStartup < end;) yield return null;
                    spawner.Materialize(LabSpawnKind.Box);
                    yield return null;
                }
                for (float end = Time.realtimeSinceStartup + 0.8f; Time.realtimeSinceStartup < end;) yield return null;
                Check(field.BoxCount == LabSpawner.MaxBoxCount && field.Selected == LabSpawner.MaxBoxCount,
                    $"laboratorio v4: {field.BoxCount} cajas en el campo (tope {LabSpawner.MaxBoxCount}) y la ultima elegida");
                field.Select(1);
                field.Set(LabFieldVar.X, 2f);
                field.Set(LabFieldVar.Distance, 4f);
                yield return null;
                Check(field.Selected == 1 && Mathf.Abs(field.Get(LabFieldVar.X) - 2f) < 0.25f && Mathf.Abs(field.Get(LabFieldVar.Distance) - 4f) < 0.25f,
                    "laboratorio v4: los controles mueven la caja elegida (X y distancia)");
                field.Select(LabField.PaperSlot);
                field.DeleteAll();
                yield return null;
                Check(field.BoxCount == 0 && field.Selected == LabField.PaperSlot, "laboratorio v4: Borrar todo deja solo el papel");
            }

            // --- Laboratorio v6: las placas fijas con lentes (resultado, atril, mediciones) ya no existen (B.3): son etiquetas por mirada
            // (Scripts/Lab/LabGazeTags, las comprueba su propio bloque). Aca solo se apagan las lentes.
            LabOverlay.Visible = false;
            for (float end = Time.realtimeSinceStartup + 0.3f; Time.realtimeSinceStartup < end;) yield return null;
            // --- Nexo y tutorial (paso N): el jugador puede empezar en la terraza o en el nexo, y reaparece en el nexo.
            var spawn = Object.FindFirstObjectByType<SpellSimulator.Player.PlayerSpawn>();
            bool spawnOk = spawn != null;
            if (spawn != null)
            {
                Vector3 spawnBack = spawn.transform.position;
                Quaternion spawnBackRot = spawn.transform.rotation;
                var spawnBody = spawn.GetComponent<Rigidbody>();
                spawn.Begin(false);
                Vector3 onTerrace = spawn.transform.position;
                bool terraceFloor = Physics.Raycast(onTerrace, Vector3.down, out RaycastHit terraceHit, 3f, ~0, QueryTriggerInteraction.Ignore)
                    && terraceHit.collider.GetComponentInParent<SpellSimulator.Player.PlayerSpawn>() == null;
                // Torre-tutorial (T1): la barrera se pone al empezar, la palanca saltea y la barrera se apaga.
                var tutorial = SpellSimulator.World.TowerTutorial.Instance;
                var lever = Object.FindFirstObjectByType<SpellSimulator.World.TutorialSkipLever>();
                bool barrierUp = tutorial != null && tutorial.IsActive && tutorial.BarrierUp && lever != null;
                // Un punto por fuera del muro, a la altura de la terraza: ahi solo esta la barrera.
                Vector3 outside = tutorial != null ? tutorial.transform.position + new Vector3(0f, 15f, -(SpellSimulator.World.Map.TutorialRules.BarrierRadius + 0.15f)) : Vector3.zero;
                bool caged = tutorial != null && Physics.CheckSphere(outside, 0.2f, ~0, QueryTriggerInteraction.Ignore);
                // Piso 2 (T2): la pila tapa el descanso, la baranda cierra el hueco y «Hacer el tutorial» otra vez rearma la pila.
                var pileRoot = tutorial != null ? tutorial.transform.Find(SpellSimulator.EditorTools.Map.MapTutorial.PropsName) : null;
                var pileBodies = pileRoot != null ? pileRoot.GetComponentsInChildren<Rigidbody>() : new Rigidbody[0];
                float floor2 = SpellSimulator.EditorTools.Map.TowerBuilder.FloorHeight * 2f;
                Vector3 towerAt = tutorial != null ? tutorial.transform.position : Vector3.zero;
                Vector3 landing = towerAt + SpellSimulator.EditorTools.Map.TowerBuilder.Polar(5.45f, SpellSimulator.EditorTools.Map.MapTutorial.PileAngle, floor2 + 0.5f);
                bool Piled() { foreach (var c in Physics.OverlapSphere(landing, 0.9f)) if (c.attachedRigidbody != null && c.transform.IsChildOf(pileRoot)) return true; return false; }
                bool piled = pileBodies.Length >= 5 && Piled();
                bool railed = Physics.Raycast(towerAt + SpellSimulator.EditorTools.Map.TowerBuilder.Polar(SpellSimulator.EditorTools.Map.TowerBuilder.RampInner - 0.6f, 100f, floor2 + 0.5f),
                    SpellSimulator.EditorTools.Map.TowerBuilder.Polar(1f, 100f, 0f), out RaycastHit railHit, 1.2f, ~0, QueryTriggerInteraction.Ignore) && railHit.collider.name.StartsWith("Tutorial_Rail");
                foreach (var body in pileBodies) { body.position += Vector3.up * 40f; body.transform.position = body.position; }
                Physics.SyncTransforms();
                bool cleared = pileBodies.Length > 0 && !Piled();
                spawn.Begin(false);
                Physics.SyncTransforms();
                bool rearmed = pileBodies.Length > 0 && Piled();
                Check(piled && railed && cleared && rearmed, $"tutorial piso 2: la pila tapa el descanso, hay baranda y se rearma (pila {piled}, baranda {railed}, despejada {cleared}, rearmada {rearmed})");
                // Geometria de la torre (rework 2026-11-17): rampa de media vuelta por el norte, hueco en el piso solo sobre ella y plaza sur con piso.
                bool GroundIs(float r, float a, float yFrom, string expected) =>
                    Physics.Raycast(towerAt + SpellSimulator.EditorTools.Map.TowerBuilder.Polar(r, a, yFrom), Vector3.down, out RaycastHit gh, 3f, ~0, QueryTriggerInteraction.Ignore) && gh.collider.name == expected;
                float midRing = (SpellSimulator.EditorTools.Map.TowerBuilder.RampInner + SpellSimulator.EditorTools.Map.TowerBuilder.RampOuter) * 0.5f;
                bool rampNorth = GroundIs(midRing, 90f, 2.25f + 1.2f, "TowerRamp_0");
                bool holeOverRamp = GroundIs(midRing, 120f, floor2 + 0.5f, "TowerRamp_1");
                bool plazaSouth = GroundIs(midRing, 270f, floor2 + 0.5f, "TowerFloor_2") && GroundIs(midRing, 225f, SpellSimulator.EditorTools.Map.TowerBuilder.FloorHeight + 0.5f, "TowerFloor_1");
                bool centerDisc = GroundIs(0f, 0f, floor2 + 0.5f, "TowerFloor_2");
                Check(rampNorth && holeOverRamp && plazaSouth && centerDisc, $"torre v2: rampa por el norte, hueco solo sobre la rampa, plaza sur y disco central con piso (rampa {rampNorth}, hueco {holeOverRamp}, plaza {plazaSouth}, disco {centerDisc})");
                // Terraza (v2): la reja de la bajada solo se apaga cuando se hicieron las dos acciones (agacharse y saltar) y el rearme la vuelve a poner.
                bool gateUp = tutorial != null && tutorial.TerraceGateUp && !tutorial.Crouched && !tutorial.JumpedWall;
                if (tutorial != null) tutorial.MarkTerraceDone(true, false);
                bool halfDone = tutorial != null && tutorial.TerraceGateUp && tutorial.Crouched;
                if (tutorial != null) tutorial.MarkTerraceDone(false, true);
                bool gateDown = tutorial != null && !tutorial.TerraceGateUp;
                spawn.Begin(false);
                bool gateBack = tutorial != null && tutorial.TerraceGateUp && !tutorial.Crouched && !tutorial.JumpedWall;
                Check(gateUp && halfDone && gateDown && gateBack, $"tutorial terraza: la reja de la bajada se apaga al agacharse y saltar, y se rearma (puesta {gateUp}, con una sola {halfDone}, apagada {gateDown}, rearmada {gateBack})");
                // Piso 2 (v2): 3 ejercicios opcionales (flotar, de canto, diana) que encienden su farol y el rearme los apaga; y el lienzo de practica existe.
                var exercises = tutorial != null ? tutorial.GetComponentsInChildren<SpellSimulator.World.TutorialExercise>(true) : new SpellSimulator.World.TutorialExercise[0];
                bool threeKinds = exercises.Length == 3;
                bool noneDone = true, allDone = true, rearmedEx = true;
                foreach (var ex in exercises) noneDone &= !ex.Done;
                foreach (var ex in exercises) ex.Complete();
                foreach (var ex in exercises) allDone &= ex.Done;
                spawn.Begin(false);
                foreach (var ex in exercises) rearmedEx &= !ex.Done;
                Check(threeKinds && noneDone && allDone && rearmedEx, $"tutorial piso 2: ejercicios opcionales (3 ejercicios {threeKinds}, apagados {noneDone}, se cumplen {allDone}, rearme {rearmedEx})");
                // Piso 1 (T3): la tela pesada tapa la bajada (un rayo tangente la toca), el libro del viento esta, y al soltarla el paso queda libre y se rearma.
                var cloth = pileRoot != null ? pileRoot.GetComponentInChildren<SpellSimulator.World.TutorialCloth>(true) : null;
                float floor1 = SpellSimulator.EditorTools.Map.TowerBuilder.FloorHeight;
                Vector3 clothFrom = towerAt + SpellSimulator.EditorTools.Map.TowerBuilder.Polar(5.5f, SpellSimulator.EditorTools.Map.MapTutorial.ClothAngle + 12f, floor1 + 1.2f);
                Vector3 clothDir = SpellSimulator.EditorTools.Map.TowerBuilder.Polar(1f, SpellSimulator.EditorTools.Map.MapTutorial.ClothAngle - 90f, 0f);
                bool ClothBlocks() => Physics.Raycast(clothFrom, clothDir, out RaycastHit ch, 1.5f, ~0, QueryTriggerInteraction.Ignore) && ch.collider.GetComponentInParent<SpellSimulator.World.TutorialCloth>() != null;
                bool hung = cloth != null && !cloth.IsOpen && ClothBlocks();
                bool windBook = false;
                foreach (var lb in Object.FindObjectsByType<SpellSimulator.Learning.LibraryBook>(FindObjectsSortMode.None)) if (lb.BookId == "wind-primer") windBook = true;
                if (cloth != null) cloth.ForceOpen();
                Physics.SyncTransforms();
                bool freed = cloth != null && cloth.IsOpen && !ClothBlocks();
                spawn.Begin(false);
                Physics.SyncTransforms();
                bool rehung = cloth != null && !cloth.IsOpen && ClothBlocks();
                Check(hung && windBook && freed && rehung, $"tutorial piso 1: la tela tapa la bajada, esta el tomo I y al soltarla se libera y se rearma (colgada {hung}, libro {windBook}, libre {freed}, rearmada {rehung})");
                // Planta baja (T4): la puerta arranca trabada (E no la abre); cada traba (pestillo, contrapeso, piedra) la destraba; saltear la abre.
                var towerDoor = Find("TowerDoor") != null ? Find("TowerDoor").GetComponent<TowerDoor>() : null;
                bool locked = towerDoor != null && towerDoor.Locked && !towerDoor.IsOpen;
                if (towerDoor != null) towerDoor.Press();
                bool pressIgnored = towerDoor != null && !towerDoor.IsOpen;
                bool noneYet = tutorial != null && !tutorial.DoorLatchReleased();
                var latchRoot = pileRoot != null ? pileRoot.Find(SpellSimulator.EditorTools.Map.MapTutorial.DoorLatchesName) : null;
                var latchBodies = latchRoot != null ? latchRoot.GetComponentsInChildren<SpellSimulator.World.TutorialCloth>(true) : new SpellSimulator.World.TutorialCloth[0];
                int latchesOk = 0;
                foreach (var l in latchBodies)
                {
                    l.ForceOpen();
                    if (tutorial.DoorLatchReleased()) latchesOk++;
                    spawn.Begin(false);
                }
                var stoneGo = tutorial != null ? tutorial.transform.parent != null ? Find(SpellSimulator.EditorTools.Map.MapTutorial.DoorStoneName) : null : null;
                bool stoneBreaks = false;
                if (stoneGo != null)
                {
                    Vector3 inward = tutorial.transform.position - stoneGo.transform.position;
                    inward.y = 0f;
                    SpellSimulator.World.MeshBreaker.Break(stoneGo.transform.position + inward.normalized * 0.4f + Vector3.up * 0.5f, 0.55f, false);
                    stoneBreaks = tutorial.DoorLatchReleased();
                    spawn.Begin(false);
                }
                bool relocked = towerDoor != null && towerDoor.Locked && !tutorial.DoorLatchReleased();
                tutorial.Skip();
                bool skipOpens = towerDoor != null && !towerDoor.Locked && towerDoor.IsOpen;
                Check(locked && pressIgnored && noneYet && latchBodies.Length == 2 && latchesOk == 2 && stoneBreaks && relocked && skipOpens,
                    $"tutorial planta baja: la puerta arranca trabada y la destraban pestillo, contrapeso y piedra (trabada {locked}, E ignorada {pressIgnored}, sin soltar {noneYet}, trabas {latchBodies.Length}/{latchesOk}, piedra {stoneBreaks}, rearme {relocked}, salteo abre {skipOpens})");
                spawn.Begin(false);
                if (lever != null) lever.Press(); else spawn.Begin(true);
                bool barrierDown = tutorial != null && !tutorial.IsActive && !tutorial.BarrierUp;
                barrierDown &= !Physics.CheckSphere(outside, 0.2f, ~0, QueryTriggerInteraction.Ignore);
                Check(barrierUp && caged && barrierDown, $"tutorial: la barrera encierra la torre y la palanca saltea (puesta {barrierUp}, encierra {caged}, apagada {barrierDown})");
                Vector3 atNexus = spawn.transform.position;
                spawnBody.position = new Vector3(0f, SpellSimulator.World.Map.SpawnRules.FallHeight - 5f, 60f);
                spawn.transform.position = spawnBody.position;
                Physics.SyncTransforms();
                for (float end = Time.realtimeSinceStartup + 3f; spawnBody.position.y < 0f && Time.realtimeSinceStartup < end;) yield return null;
                Vector3 respawned = spawnBody.position;
                Vector2 nexus = SpellSimulator.World.Map.ValleyShape.Spawn;
                spawnOk = terraceFloor && onTerrace.y > 12f
                    && Vector2.Distance(new Vector2(atNexus.x, atNexus.z), nexus) < 0.5f
                    && Vector2.Distance(new Vector2(respawned.x, respawned.z), nexus) < 1.5f;
                if (!spawnOk) Debug.LogWarning($"[Smoke] spawn: terraza {onTerrace} piso={terraceFloor}, nexo {atNexus}, reaparicion {respawned}");
                spawnBody.linearVelocity = Vector3.zero;
                spawnBody.position = spawnBack;
                spawn.transform.SetPositionAndRotation(spawnBack, spawnBackRot);
                Physics.SyncTransforms();
                // La camara sigue al jugador con suavizado: esperar a que se quede quieta antes de los chequeos que la miden.
                var settleCam = Camera.main;
                Vector3 settleLast = settleCam != null ? settleCam.transform.position : Vector3.zero;
                int settleStill = 0;
                for (float end = Time.realtimeSinceStartup + 4f; settleCam != null && settleStill < 10 && Time.realtimeSinceStartup < end;)
                {
                    yield return null;
                    settleStill = (settleCam.transform.position - settleLast).sqrMagnitude < 1e-8f ? settleStill + 1 : 0;
                    settleLast = settleCam.transform.position;
                }
            }
            Check(spawnOk, "nexo: el tutorial empieza en la terraza, el salteo deja en el nexo y una caida reaparece en el nexo");
            var towerMap = Object.FindFirstObjectByType<SpellSimulator.World.Map.TowerMap>();
            Check(towerMap != null && Vector2.Distance(new Vector2(towerMap.transform.position.x, towerMap.transform.position.z),
                SpellSimulator.World.Map.ValleyShape.Spawn) < 8f, "nexo: el tablero del mapa esta junto al nexo");
            Check(towerMap != null && towerMap.ZoneCount > 0 && towerMap.MarkerCount == towerMap.ZoneCount,
                $"mapa de la torre: {(towerMap != null ? towerMap.MarkerCount : 0)} marcadores = zonas");
            // Mapa: los rombos de portal son informacion (nadie se teletransporta) y Tab abre la vista completa del cuadro.
            var mapCam = Camera.main;
            Vector3 beforeMap = mapCam != null ? mapCam.transform.position : Vector3.zero;
            if (towerMap != null)
                for (int i = 0; i < towerMap.PortalMarkerCount; i++) towerMap.GetPortalMarker(i).Press();
            yield return null;
            bool noTeleport = mapCam != null && (mapCam.transform.position - beforeMap).sqrMagnitude < 1e-6f
                && towerMap != null && towerMap.PortalMarkerCount >= 4;
            Check(noTeleport, "mapa de la torre: presionar los rombos de portal no teletransporta");
            bool tabOpens = false, tabClosed = false;
            if (towerMap != null && mapCam != null && !SpellSimulator.Player.PlatformDetector.IsVR
                && towerMap.TryGetFocusFrame(out Vector3 mapCenter, out Vector3 mapNormal, out Vector2 mapSize))
            {
                Vector3 camBefore = mapCam.transform.position;
                // Se mide en el mismo frame: despues el jugador del smoke mueve la camara antes del LateUpdate de SurfaceFocus.
                SpellSimulator.Player.SurfaceFocus.ShowPanel(mapCam.transform, mapCenter, mapNormal, mapSize.x, mapSize.y);
                tabOpens = SpellSimulator.Player.SurfaceFocus.IsActive && SpellSimulator.Player.SurfaceFocus.IsPanel
                    && Vector3.Distance(mapCam.transform.position, mapCenter) > 0.5f && Vector3.Dot(mapCam.transform.forward, mapNormal) < -0.99f;
                if (!tabOpens)
                    Debug.Log($"[Smoke] mapa Tab: activo {SpellSimulator.Player.SurfaceFocus.IsActive}, panel {SpellSimulator.Player.SurfaceFocus.IsPanel}, " +
                              $"distancia {Vector3.Distance(mapCam.transform.position, mapCenter):F2}, dot {Vector3.Dot(mapCam.transform.forward, mapNormal):F3}, tam {mapSize}");
                SpellSimulator.Player.SurfaceFocus.Close();
                yield return null;
                tabClosed = !SpellSimulator.Player.SurfaceFocus.IsActive && (mapCam.transform.position - camBefore).sqrMagnitude < 1e-4f;
            }
            Check(tabOpens && tabClosed, $"mapa de la torre: Tab abre la vista completa de frente (abrio {tabOpens}, cerro {tabClosed})");
            var trainingTarget = GameObject.Find("PaperCanvas");
            var labBench = GameObject.Find("LabBench");
            Check(trainingTarget != null && labBench != null && Vector3.Distance(trainingTarget.transform.position, labBench.transform.position) > 12f,
                "entrenamiento base reubicado a mas de 12 m del banco del laboratorio");

            // Controles: todos enlazados y uno (velocidad del viento) cambia su variable por su delegado set (rueda = un paso).
            // Incluye las paginas ocultas de la consola v5 (se enlazan en el primer frame, con todo activo).
            var bindings = Object.FindObjectsByType<LabControlBinding>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int bound = 0;
            LabControlBinding speedBinding = null;
            foreach (var binding in bindings)
            {
                if (binding.Bound) bound++;
                if (binding.Source == LabControlSource.Environment && binding.Variable == (int)LabVariableId.WindSpeed) speedBinding = binding;
            }
            Check(bindings.Length >= 24 && bound == bindings.Length, $"laboratorio v2: {bound}/{bindings.Length} controles enlazados");
            bool controlMoved = false;
            if (speedBinding != null && speedBinding.Bound)
            {
                float speedBefore = parameters.Get(LabVariableId.WindSpeed);
                LabControl control = speedBinding.Control;
                control.BeginManipulation(default);
                control.UpdateManipulation(new SpellSimulator.Interfaces.ManipulationInput(Vector2.zero, Vector3.zero, 0f, Vector3.zero,
                    Vector3.forward, Vector3.right, 1f, false));
                control.EndManipulation();
                controlMoved = parameters.Get(LabVariableId.WindSpeed) > speedBefore + 0.5f;
            }
            Check(controlMoved, "laboratorio v2: un LabControl (velocidad del viento) cambia su variable");

            // Viento visible: tras materializar, LabWindVisual sigue la zona (enfriamiento de 1 s por boton, tiempo de juego).
            var windVisual = Object.FindFirstObjectByType<LabWindVisual>();
            float windAt = Time.time;
            for (float end = Time.realtimeSinceStartup + 5f; Time.time - windAt < 1.2f && Time.realtimeSinceStartup < end;) yield return null;
            var visibleWind = spawner.Materialize(LabSpawnKind.Wind);
            int windFrame = Time.frameCount;
            while (Time.frameCount < windFrame + 3) yield return null;
            Check(windVisual != null && visibleWind != null && windVisual.HasZone, "laboratorio v2: LabWindVisual sigue la zona de viento materializada");
            spawner.ClearAll();

            // --- Registro: se cierra (escribe la perilla pendiente y libera el archivo) y se cuentan las filas.
            for (float end = Time.realtimeSinceStartup + LabSessionLog.KnobGroupSeconds + 0.3f; Time.realtimeSinceStartup < end;) yield return null;
            logGo.SetActive(false);
            string[] lines = File.Exists(LabLogPath) ? File.ReadAllLines(LabLogPath) : new string[0];
            int rows = Mathf.Max(0, lines.Length - 1);
            string all = string.Join("\n", lines);
            Check(rows >= 4 && all.Contains(LabSessionLog.EventKnob) && all.Contains(LabSessionLog.EventMaterialize) && all.Contains(LabSessionLog.EventReset),
                $"laboratorio: el registro CSV en Logs/ tiene {rows} filas (perilla, materializar y reinicio)");
            // Columna «evento» exacta (el encabezado tiene «spec_sello»).
            string sealCell = LabCsv.Separator + LabSessionLog.EventSeal + LabCsv.Separator;
            Check(all.Contains(sealCell), "laboratorio v2: el registro CSV tiene el evento «sello»");

            Object.Destroy(logGo);
            spawner.ClearAll();
            parameters.ResetAll();
            if (bench != null) bench.ResetSpec();
        }

        /// <summary>Elemento que deberia dar un sigilo (su entrada del diccionario); None si no se puede saber.</summary>
        private static SpellSimulator.Compiler.ElementType ExpectedElement(SpellSimulator.Glyphs.GlyphDictionary glyphs, string sigilId)
        {
            if (glyphs == null || string.IsNullOrEmpty(sigilId)) return SpellSimulator.Compiler.ElementType.None;
            var entry = glyphs.Get(sigilId);
            return entry != null ? SpellSimulator.Compiler.SpellIR.ParseElement(entry.Element) : SpellSimulator.Compiler.ElementType.None;
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
