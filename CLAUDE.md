# CLAUDE.md — Reglas para agentes (y humanos)

SpellSimulator: simulador sandbox de hechicería en **Unity 6 (6000.3.14f1) + URP**. El jugador
dibuja glifos sobre un lienzo físico; un modelo ONNX (Unity Inference Engine / Sentis) los
reconoce, un compilador los traduce a un `SpellIR` y el módulo de salida instancia VFX.
Target: **PC (mouse/teclado) y Meta Quest (OpenXR) en paralelo**.

Idioma del proyecto: **español** (docs, commits, logs, comentarios). Identificadores de código en inglés.

## Antes de tocar nada

1. Leé [Docs/Home.md](Docs/Home.md) (índice). Para entender **qué** se construye: [Docs/GAME_DESIGN.md](Docs/GAME_DESIGN.md),
   [Docs/MAGIC_SYSTEM.md](Docs/MAGIC_SYSTEM.md) y [Docs/SYSTEMS_DESIGN.md](Docs/SYSTEMS_DESIGN.md). Para una tarea concreta, como mínimo:
   - [Docs/ARCHITECTURE.md](Docs/ARCHITECTURE.md) — pipeline y contratos entre módulos.
   - [Docs/CODESTYLE.md](Docs/CODESTYLE.md) — convenciones C#/Unity obligatorias.
   - [Docs/KNOWN_ISSUES.md](Docs/KNOWN_ISSUES.md) — bugs y deuda técnica ya detectados.
2. Si la tarea toca reconocimiento/compilación, consultá el proyecto de referencia
   `D:\Programas\wha-spell-simulator` (ver [Docs/REFERENCE.md](Docs/REFERENCE.md)). Es la fuente de verdad
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

1. Entender → planear (si es > 1 archivo, contar el plan en 3-5 líneas antes).
2. Implementar en `Assets/_Project/`.
3. **Verificar** con el harness ([Docs/HARNESS.md](Docs/HARNESS.md)): compilación limpia en consola de Unity,
   tests EditMode verdes y, si aplica, Play Mode con logs `[Modulo]` esperados.
4. **Actualizar la documentación afectada en el mismo cambio** (ver tabla abajo) y agregar entrada a
   [Docs/CHANGELOG.md](Docs/CHANGELOG.md).
5. Commit atómico en `main` al cerrar la tarea, mensaje en español, estilo de los commits existentes
   (`Título corto` + cuerpo opcional). **Nunca push** sin pedido explícito. No commitear cambios de
   `ProjectSettings/`/escenas que no hizo el agente sin avisar.

### Qué doc actualizar

| Si cambiaste… | Actualizá |
|---|---|
| Un módulo, contrato, flujo de datos | `Docs/ARCHITECTURE.md` |
| Carpetas / archivos nuevos o movidos | `Docs/STRUCTURE.md` |
| Glifos, diccionario, deltas semánticos | `Docs/GLYPHS.md` |
| Arreglaste o descubriste un bug/deuda | `Docs/KNOWN_ISSUES.md` |
| Decisión de diseño no obvia | `Docs/DECISIONS.md` (ADR corto) |
| Avance de hito / tarea | `Docs/ROADMAP.md` |
| Mecánica, objeto o regla de juego | `Docs/GAME_DESIGN.md` (y `MAGIC_SYSTEM.md` si toca el canon) |
| Sistema nuevo o cambio de su diseño | `Docs/SYSTEMS_DESIGN.md` |
| Siempre | `Docs/CHANGELOG.md` y re-generar grafo (`graphify update .`) si cambió código |

## Comandos útiles

```bash
graphify update .                          # re-extrae el grafo del código (sin LLM)
graphify export obsidian --dir Docs/Graph  # regenera la vista de Obsidian
graphify query "¿quién llama a ProcessVectorSession?"
```
