---
tags: [doc, referencia]
---
# Proyecto de referencia: wha-spell-simulator

- Ruta local: `D:\Programas\wha-spell-simulator` · Web: https://wha-spell-simulator.vercel.app/
- Stack: SvelteKit + TypeScript, `onnxruntime-web`, Postgres (muestras), PyTorch (entrenamiento).
- Fan project de *Witch Hat Atelier*; SpellSimulator porta su pipeline a Unity/VR.

## Mapa referencia → Unity

| Referencia (TS) | Unity (C#) | Estado del port |
|---|---|---|
| `src/lib/parser/` (clean, ring, grouping) | `Core/StrokeCleaner`, `RingDetector`, `StrokeGrouper` | Simplificado (heurísticas propias, sin template matcher `$P`/chamfer ni tree-cut) |
| `src/lib/parser/ml/rendering.ts` (`renderCandidateTensor`) | `Inference/TensorRasterizer` | Equivalente (96px, -1/+1, margen 10%/lado) |
| `src/lib/parser/ml/predictions.ts` (softmax, votos) | `SentisInferenceEngine.RunInference` | **Falta softmax** (KI-001); no usa cabezas de pose |
| `src/lib/compiler/spellBuilder.ts` | `Compiler/SpellCompiler` | Constantes de tuning portadas; deltas difieren (KI-003) |
| `src/lib/compiler/semanticRules.ts` | `Compiler/SemanticRules` | Parcial (Column, Levitation/Float) |
| `src/lib/compiler/spellDirection.ts`, `spellQuality.ts` | — | No portado (dirección neutra `forward`, quality = confianza) |
| `src/lib/field/buildSpellField.ts` | — | No portado (ver ROADMAP F4) |
| `src/lib/dictionary/{sigils,signs}/*.json` | `SpellCompiler.SigilIds/SignIds/SignSemantics` | Hardcodeado en C# |
| `static/models/glyph-recognizer.onnx*`, `glyph-class-to-idx.json` | `Assets/_Project/Data/` | Idénticos (md5) |
| `models/training/` | — | Entrenamiento fuera de Unity |

## Docs útiles de la referencia
- `docs/recognition.md` — pipeline híbrido, entrada/salida del modelo, rotación.
- `docs/spell-ir.md` — contrato de `SpellIR`.
- `docs/glyph-ast.md` — estructura del AST.
- `docs/dictionary-authoring.md` — formato de glifos.
- `docs/play-rules.md` — reglas de juego (anillo abierto/cerrado, etc.).
- `docs/effect-rendering.md`, `Plan for converting spells to animations.md` — VFX.

Al portar algo, citar el archivo de origen en un comentario y actualizar esta tabla.
