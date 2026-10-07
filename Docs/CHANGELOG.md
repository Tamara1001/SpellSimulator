---
tags: [doc, changelog]
---
# Changelog

Entradas más nuevas arriba. Una por tarea/commit relevante: fecha, qué, por qué, docs tocadas.

## 2026-10-07 — M2: correcciones de la prueba manual, tinta, trapo, feedback y build
- **Prueba manual**: el sigilo de fuego grande se partía en sigilo + 2 signos `region`. Se resolvió con agrupado por
  **afinidad** (de la referencia) + `SealReader` (lectura guiada por reconocimiento, en un batch). 5 sigilos grandes pasan end-to-end.
- **Dibujo lento**: estabilizador por plataforma y sensibilidad al dibujar 0.6.
- KI-032 resuelto (círculos de glifos no son anillos). **KI-033**: bug del buffer de tensores, lo encontró un test.
- `GlyphClassifier` puro (testeable con el modelo) detrás de `SentisGlyphClassifier`.
- **Tinta**: `InkReservoir`, lápiz con depósito e indicador de color, `Inkwell`, `InkSupply`, `R` para recargar, mojar en VR.
- **Trapo** (`RagTool`), **feedback del sello** (`SealFeedback`), **hápticos** (`Haptics`), contacto en VR (toggle).
- Escena: trapo, tintero, `SealFeedback` en el papel, `InkSupply` en el jugador.
- 64 tests. **Build de Windows** en `Build/SpellSimulator.exe` (verificada: arranca en PC sin excepciones).

## 2026-10-07 — M2: auto-activación al cerrar el anillo
- `SealActivation` (lógica pura) + `SpellCaster.OnStrokeFinished` / `OnSealEdited` / `GetPhase` / `SealPhaseChanged`.
- `PhysicalTool`: al soltar un trazo de tinta se evalúa el sello; el secundario es deshacer en ambas herramientas.
- 6 tests nuevos (50 en total). Smoke test en Play Mode: sigilo → anillo abierto (preparado) → cerrar (lanza) →
  trazo extra (no relanza) → deshacer (rearma) → cerrar (lanza).
- Decisiones de diseño: sellos de piedra (cooldown, 5 usos), números del Duelo, audio y tutorial ⇒ [[GAME_DESIGN]].
- Nuevo issue KI-032 (curvas de glifos leídas como anillo).

## 2026-10-07 — Diseño del juego completo
- Investigación del sistema mágico de Witch Hat Atelier (wiki, artículos, `ground-truth.md` de la referencia) ⇒ [[MAGIC_SYSTEM]].
- [[GAME_DESIGN]]: pilares, modos Libre y Duelo, mapa, catálogo de objetos, flujo de un hechizo, controles y preguntas abiertas.
- [[SYSTEMS_DESIGN]]: superficies inscribibles, tinta, estampas, contraptions, manifestaciones 3D, MagicWorld,
  materiales y reacciones, fractura, agua, daño, progresión, IA y presupuestos para Quest.
- [[ROADMAP]] reescrito en hitos jugables M2–M7 (Libre) y D1–D3 (Duelo).
- ADR-012 a ADR-018. PROJECT y Home actualizados.

## 2026-10-07 — Reestructura del pipeline + port fiel de la referencia
- **Pull de wha-spell-simulator** (54 commits). Adoptado: modelo GlyphStudent (0.9 MB), diccionario actualizado,
  invariantes del compilador. Resumen de novedades en [[REFERENCE]].
- **Nuevos módulos**: `Parsing`, `Recognition`, `Glyphs`, `Casting`; `Compiler` reescrito como lógica pura.
  `SpellCaster` es el único punto de entrada para castear.
- **Port fiel** de `spellBuilder`, `semanticRules`, `spellQuality`, `spellDirection`; diccionario como datos (JSON + asset + sync).
- **Reconocimiento**: rasterizado idéntico al de entrenamiento, batch, TTA ×8, softmax.
- **Anillo**: ajuste de círculo + cobertura → cerrado (activo) vs abierto (preparado); anillos de varios trazos.
- **Bugs**: 17 issues previos resueltos + nuevos encontrados y resueltos (KI-018 aspecto, **KI-019 goma/deshacer borraban
  todo**, KI-027 rasterizado, KI-028…031). Ver [[KNOWN_ISSUES]].
- **Eliminado**: `MouseInputHandler`, botones del HUD, `Assets/Editor` (reemplazado por `HarnessProjectChecks`), carpeta `_Project/VFX`.
- **Escena**: `SpellCaster` cableado, lápiz/goma con referencia al caster, `VRPointer.mat`.
- **Tests**: 44 EditMode (todos pasan) + smoke test de cast en Play Mode por MCP.
- Newtonsoft agregado explícitamente al manifest.

## 2026-10-07 — Entorno de trabajo y harness
- Documentación inicial en `Docs/` (vault Obsidian): PROJECT, ARCHITECTURE, STRUCTURE, GLYPHS, ROADMAP,
  CODESTYLE, HARNESS, KNOWN_ISSUES, DECISIONS, REFERENCE.
- `CLAUDE.md` + `AGENTS.md` con reglas para agentes.
- asmdefs: `SpellSimulator.Runtime`, `SpellSimulator.Editor`, `SpellSimulator.Tests.EditMode`.
- Harness: `HarnessTestRunner` (→ `Logs/harness-editmode.txt`) y `HarnessSceneValidator`. Menú `SpellSimulator/Harness`.
- 15 tests EditMode de `RingDetector`, `StrokeGrouper`, `StrokeCleaner`, `GeometryUtils`, `TensorRasterizer` (todos pasan).
- Graphify: `.graphifyignore`, grafo del código y export a Obsidian.
- Análisis: 17 issues registrados en [[KNOWN_ISSUES]] (crítico: KI-001 logits sin softmax).

## Historial previo (de git)
- `a57a0a1` Agarrar items + dibujar en canvas (VR)
- `eda9f70` VR básico (head movement + detección de manos)
- `b49aff3` Physical Tools & Canvas
- `a6a9aab` 3D Controller
- `368fe20` Tools (Pen & Eraser)
- `37a0dc5` VFX Graph Fire · `daeaa95` RingDetector · `d54fafc` StrokeCleaner · `1190100` Debug UI
- `4e92e91` Diccionario completo · `098a05e` Spell Recognition (Sentis) · `5a7579a` Prototipo inicial
