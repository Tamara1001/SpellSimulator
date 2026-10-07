---
tags: [doc, adr]
---
# Decisiones (ADR)

Formato corto: contexto → decisión → consecuencias. Numeración incremental, nunca reutilizar.

## ADR-001 — Reconocimiento con el modelo de wha-spell-simulator
**2026-08 (retroactivo).** Se reutiliza el ResNet18 multi-head 96×96 entrenado en la referencia (8k+ muestras),
corriendo local con Unity Inference Engine (`GPUCompute`). No se entrena en Unity. El rasterizado de Unity debe
imitar `renderCandidateTensor` de la referencia (fondo -1, tinta +1, bbox con margen). Ver [[REFERENCE]], [[GLYPHS]].

## ADR-002 — PC y VR en paralelo, una sola escena
**2026-10-07.** Ambas plataformas se desarrollan a la vez. Un único `Player` prefab con `PCRIG` y `VRRIG`;
`PlatformDetector` activa uno al inicio y expone `IsVR`. Es el único punto con lógica de plataforma.
Consecuencia: toda feature se diseña con input abstracto (`PlayerInputReader`, `IUsable`) y se prueba en ambos.

## ADR-003 — Input por código, sin `.inputactions`
**2026-08 (retroactivo).** `PlayerInputReader` crea las `InputAction` en `Awake` con bindings PC/Gamepad/XR.
Facilita agregar bindings XR sin tocar assets. `InputSystem_Actions.inputactions` queda sin uso.

## ADR-004 — Cada lienzo es dueño de su `DrawingSession`
**2026-09 (retroactivo).** `PhysicalTool` usa `hitCanvas.Session`; permite varios lienzos independientes.
`MouseInputHandler` (legacy) mantiene una sesión propia; ver KI-014.

## ADR-005 — Harness contra el Editor vivo vía Unity MCP
**2026-10-07.** El Editor suele estar abierto, así que la verificación no usa batchmode. Se agregan asmdefs
(`SpellSimulator.Runtime/Editor/Tests.EditMode`), un runner que escribe `Logs/harness-editmode.txt` y un validador
de escena, todos invocables por menú o `Unity_RunCommand`. Ver [[HARNESS]].

## ADR-006 — Documentación como vault de Obsidian en `Docs/`
**2026-10-07.** Docs en Markdown con wikilinks dentro de `Docs/` (fuera de `Assets/`, Unity no la importa).
Graphify genera `Docs/Graph/` (ignorado en git, regenerable). `CLAUDE.md` en la raíz es el punto de entrada para agentes.

## ADR-007 — Port fiel del compilador y diccionario como datos
**2026-10-07.** El compilador de Unity era una versión propia con valores distintos. Se porta fielmente
`spellBuilder.ts`, `semanticRules.ts`, `spellQuality.ts` y `spellDirection.ts`, y el significado de cada glifo pasa a ser
**datos**: los JSON de la referencia se copian a `Data/Dictionary/` y se cargan vía `GlyphDictionaryAsset`. Consecuencia:
cambiar o agregar un glifo no toca código, y un `git pull` de la referencia + "Sync From Reference" trae sus ajustes.
La capa Plan queda para F4.

## ADR-008 — Modelo GlyphStudent con TTA y softmax
**2026-10-07.** Se reemplaza el ResNet18 (22 MB) por el GlyphStudent destilado (0.9 MB, misma I/O). Se infieren todos
los candidatos × 8 rotaciones en un solo batch y se promedia el softmax (como la referencia). Se elimina la rotación manual
de signos. Si en Quest es caro, `useRotationTta = false` reduce el costo 8×.

## ADR-009 — Reglas de la referencia: anillo obligatorio y activación por cierre
**2026-10-07.** Sin anillo el hechizo es inválido (antes se casteaba el sigilo suelto). Anillo cerrado = activo,
abierto = preparado (válido, no se lanza). Habilita la mecánica de "sellar" un hechizo preparado.

## ADR-010 — Pipeline en módulos puros detrás de `SpellCaster`
**2026-10-07.** `Parsing` → `Recognition` → `Glyphs` → `Compiler` son lógica pura (salvo el clasificador, detrás de
`IGlyphClassifier`). `SpellCaster` es el único punto de entrada y emite `SpellCompiled` para que sandbox, hápticos y
sonido se enganchen sin acoplarse. `MouseInputHandler` y el HUD de botones se eliminan: PC y VR usan las herramientas físicas.

## ADR-011 — Espacio isotrópico para el reconocimiento
**2026-10-07.** Los trazos se guardan en UV del lienzo, pero el pipeline trabaja en un espacio donde el lado mayor mide 1
(`DrawingCanvas.UvToIsotropic`), para que lienzos no cuadrados no deformen anillos ni glifos.
