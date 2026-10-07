---
tags: [doc, changelog]
---
# Changelog

Entradas más nuevas arriba. Una por tarea/commit relevante: fecha, qué, por qué, docs tocadas.

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
