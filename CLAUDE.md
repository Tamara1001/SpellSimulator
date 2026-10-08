# CLAUDE.md — Reglas para agentes (y humanos)

SpellSimulator: simulador sandbox de hechicería en **Unity 6 (6000.3.14f1) + URP**. El jugador
dibuja glifos sobre un lienzo físico; un modelo ONNX (Unity Inference Engine / Sentis) los
reconoce, un compilador los traduce a un `SpellIR` y el módulo de salida instancia VFX.
Target: **PC (mouse/teclado) y Meta Quest (OpenXR) en paralelo**.

Si sos una persona o un agente nuevo: empezá por [Docs/ONBOARDING.md](Docs/ONBOARDING.md).

Idioma del proyecto: **español** (docs, commits, logs, comentarios). Identificadores de código en inglés.

## Antes de tocar nada

1. Leé [Docs/Home.md](Docs/Home.md) (índice). Para entender **qué** se construye: [Docs/GAME_DESIGN.md](Docs/GAME_DESIGN.md),
   [Docs/MAGIC_SYSTEM.md](Docs/MAGIC_SYSTEM.md) y [Docs/SYSTEMS_DESIGN.md](Docs/SYSTEMS_DESIGN.md). Para una tarea concreta, como mínimo:
   - [Docs/ARCHITECTURE.md](Docs/ARCHITECTURE.md) — pipeline y contratos entre módulos.
   - [Docs/CODESTYLE.md](Docs/CODESTYLE.md) — convenciones C#/Unity obligatorias.
   - [Docs/KNOWN_ISSUES.md](Docs/KNOWN_ISSUES.md) — bugs y deuda técnica ya detectados.
2. Si la tarea toca reconocimiento/compilación, consultá el proyecto de referencia
   el repo `wha-spell-simulator` clonado en una carpeta hermana (ver [Docs/REFERENCE.md](Docs/REFERENCE.md)). Es la fuente de verdad
   del modelo ONNX, del diccionario de glifos y de las constantes de tuning.
3. Para navegar el código rápido existe el grafo de Graphify (ver [Docs/HARNESS.md](Docs/HARNESS.md#graphify)).

## Reglas duras

- **Sólo se edita código propio en `Assets/_Project/`.** No tocar `Assets/Samples/`, `Assets/TextMesh Pro/`,
  `Assets/XR*/`, `Assets/TutorialInfo/`, `Packages/` ni `Library/` salvo pedido explícito.
- **Nunca editar a mano `.unity`, `.prefab`, `.asset` ni `.meta`.** Cambios de escena/prefab/settings se hacen
  vía Unity MCP (`Unity_ManageGameObject`, `Unity_RunCommand`, etc.) o se le piden a Tamara.
  Al crear/mover/borrar un `.cs` fuera del Editor, mover también su `.meta` (el GUID mantiene las referencias).
  **Antes de reestructurar, pausar el auto-refresh** (`AssetDatabase.DisallowAutoRefresh()` por MCP): si no, Unity borra
  las `.meta` huérfanas a mitad de camino. Ver [Docs/HARNESS.md](Docs/HARNESS.md#mover--renombrar-scripts-importante).
- **El significado de los glifos es data** (`Assets/_Project/Data/Dictionary/*.json`), no código. Tuning del compilador:
  constantes nombradas portadas de la referencia, citando el archivo de origen.
- **Todo cast pasa por `SpellCaster.Cast`.** Para reaccionar a hechizos, suscribirse a `SpellCaster.SpellCompiled`.
- **Cero allocations en `Update/FixedUpdate/LateUpdate`** (ver CODESTYLE). Pre-alocar y reutilizar.
- **Lógica de plataforma (PC vs VR) sólo vía `PlatformDetector.IsVR`.** Toda feature nueva debe funcionar en ambos.
- **No `Texture2D.SetPixel`/CPU por píxel en el lienzo**: el trazado va por GPU (`RenderTexture` + `Graphics.DrawTexture`).
- **Lógica pura → clase estática sin `MonoBehaviour` + test EditMode** en `Assets/_Project/Tests/EditMode/`.
- No reintroducir dependencias directas entre módulos: comunicarse por interfaces, eventos o `DiagnosticBus`.

## Flujo de trabajo

0. Leé [Docs/STATUS.md](Docs/STATUS.md) (una página con el estado, el mapa mental y las reglas baratas) y [Docs/HARNESS.md](Docs/HARNESS.md#trucos-del-mcp-de-unity-lo-que-cuesta-redescubrir).
1. Entender → planear (si es > 1 archivo, contar el plan en 3-5 líneas antes; los rediseños, plan de media página y esperar aprobación).
2. Implementar en `Assets/_Project/`.
3. **Verificar** con el harness: compilación limpia, tests EditMode verdes y `HarnessPlaySmoke` (agregar un chequeo por sistema nuevo).
4. **Documentar lo mínimo útil:** siempre `Docs/CHANGELOG.md` y, si aplica, `Docs/KNOWN_ISSUES.md`; el resto de la tabla de abajo **solo si cambió lo que
   describe**. `graphify update .` solo si se pide (o antes de una refactorización grande).
5. Commit atómico en `main` al cerrar la tarea, mensaje en español, estilo de los commits existentes
   (`Título corto` + cuerpo opcional). **Push solo si lo piden.** No commitear cambios de
   `ProjectSettings/`/escenas que no hizo el agente sin avisar.

### Qué doc actualizar (solo si cambió)

| Si cambiaste… | Actualizá |
|---|---|
| Un módulo, contrato, flujo de datos | `Docs/ARCHITECTURE.md` |
| Carpetas / archivos nuevos o movidos | `Docs/STRUCTURE.md` |
| Glifos, diccionario, deltas semánticos | `Docs/GLYPHS.md` |
| Arreglaste o descubriste un bug/deuda | `Docs/KNOWN_ISSUES.md` |
| Decisión de diseño no obvia | `Docs/DECISIONS.md` (ADR corto) |
| Mecánica, objeto, regla o **control** de juego | `Docs/GAME_DESIGN.md` (y `MAGIC_SYSTEM.md` si toca el canon) |
| Fin de hito | `Docs/ROADMAP.md` y `Docs/STATUS.md` |
| Siempre | `Docs/CHANGELOG.md` |

## Traspaso: lo que sabía el Claude anterior (leelo, no tenés su memoria)

El proyecto lo diseñó y probó **Tamara D'Angelo** (@Tamara1001); ahora lo continúa otra persona con su propio Claude. Este bloque reemplaza las
notas de memoria del agente anterior. Si la persona que te habla es otra, preguntale cómo prefiere trabajar (idioma, commits, push) y adaptate.

**Contexto.** Evaluación final de la asignatura *Simuladores*. Dos modos: **Libre** (torre + pradera + lago, mundo reactivo, glifos que se aprenden leyendo
libros; es lo que debe estar sólido) y **Duelo** (arena, rival IA, online; después). PC y Quest en paralelo; sin fecha fija. Estilo visual: *Witch Hat Atelier*, low-poly cartoon.
El ONNX y el diccionario ya están en el repo; la referencia `wha-spell-simulator` solo hace falta para re-sincronizarlos.

**Primeros pasos:** [Docs/ONBOARDING.md](Docs/ONBOARDING.md) → [Docs/STATUS.md](Docs/STATUS.md) → «Siguiente» al final de [Docs/ROADMAP.md](Docs/ROADMAP.md).
Lo reciente (HUD, inventario, grimorio, torre, VFX, sellos, menú, calidad, WebGL) **nunca se probó a mano con teclado/mouse reales ni en VR**: lo primero es probar y corregir.

**Cómo se trabajó (y por qué):**
- **Todo en español** (docs, commits, logs, comentarios); identificadores en inglés. Commits atómicos en `main` con trailer `Co-Authored-By`. **Push solo si te lo piden.**
- **No commitear ruido del Editor** (`Assets/Settings/*_RPAsset.asset`, `UniversalRenderPipelineGlobalSettings`, `OpenXRPackageSettings`, `SpellSimulator.slnx`, defines de `ProjectSettings.asset`): `git checkout -- <archivo>`.
- **Costo/límites:** un pedido de ~25 puntos mezclados gastó >75 % de un límite de 5 h. Separá **bugs** de **rediseños**; en rediseños proponé un plan de media página y esperá el sí antes de codear.
  Delegá a subagentes **solo con especificación cerrada y archivos propios** (Haiku: datos/textos; Sonnet: lógica de varios archivos; Opus: UI/estética 3D y planificación profunda, ~250k tokens cada uno).
  Los subagentes **no compilan**: compilás y verificás vos una vez por tanda. A veces el sistema rechaza lanzar un subagente; no insistas con la misma tarea, hacela directo o avisá.
- **Verificá con el harness**, no a ojo: compilación limpia + tests EditMode + `HarnessPlaySmoke` (+1 chequeo por sistema nuevo). Capturas con `HarnessCapture` o `ScreenCapture`. Probá en el navegador los builds WebGL (servidor local con `Content-Encoding: gzip`).
- **No resolver por hipótesis lo que se puede medir**: p. ej. el desempate agua/viento se simuló, empeoró y se descartó (KI-105); ver «Siguiente» para cómo juntar datos reales (`Tests/Fixtures`).
- Si dudás, **preguntá** a la persona antes de cambiar diseño o controles. Los controles tienen una sola fuente de verdad: `Docs/GAME_DESIGN.md` (se repiten en `Learning/GrimoireTexts.cs` y `PlayerInputReader`: si cambian, tocar los tres).
- Reglas del trabajo con Unity MCP (parámetros `Code`/`Title`, esperar la compilación con loops de `sleep`, `delayCall` para builds, cartel de «guardar escena», CRLF, candado `Logs/unity.lock` si hay varios agentes): [Docs/HARNESS.md](Docs/HARNESS.md).
- `MapBuilder.Build()` regenera la escena; **`MapLayout` conserva los ajustes manuales** (`Data/MapOverrides.json`). No borres ese archivo sin avisar.

## Comandos útiles

```bash
graphify update .                          # re-extrae el grafo del código (sin LLM)
graphify export obsidian --dir Docs/Graph  # regenera la vista de Obsidian
graphify query "¿quién llama a ProcessVectorSession?"
```
