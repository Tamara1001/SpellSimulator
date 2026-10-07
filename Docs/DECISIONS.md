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
