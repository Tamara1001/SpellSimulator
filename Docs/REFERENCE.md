---
tags: [doc, referencia]
---
# Proyecto de referencia: wha-spell-simulator

- Ruta local (convención): carpeta hermana `../wha-spell-simulator` (la ruta se guarda en EditorPrefs al sincronizar) · repo: https://github.com/cosykid/wha-spell-simulator
- Stack: SvelteKit + TypeScript, `onnxruntime-web`, Postgres (muestras), PyTorch (entrenamiento).
- Fan project de *Witch Hat Atelier*; SpellSimulator porta su pipeline a Unity/VR.
- Último pull: **2026-10-07** (`9a125ec` → `73606bf`, 54 commits). Para actualizar: `git pull --ff-only` en esa carpeta
  y luego **SpellSimulator > Dictionary > Sync From Reference** en Unity.

## Mapa referencia → Unity

| Referencia (TS) | Unity (C#) | Estado |
|---|---|---|
| `src/lib/parser/rings/` | `Parsing/RingDetector` | Simplificado: ajuste Kasa + cobertura angular (96 bins), sin topología |
| `src/lib/parser/layerMapper.ts`, `coordinateNormalizer.ts` | `Parsing/RingLayers`, `SymbolCandidate` | Fiel (umbrales y métricas iguales) |
| `src/lib/parser/grouping/` | `Parsing/StrokeGrouper` | Simplificado (KI-020): radial + union-find, sin búsqueda de particiones |
| `src/lib/parser/ml/rendering.ts` | `Recognition/TensorRasterizer` | Fiel |
| `src/lib/parser/ml/predictions.ts` (softmax, TTA) | `Recognition/ClassificationMath`, `SentisGlyphClassifier` | Fiel en clase; pose no usada (KI-025) |
| template matcher (`$P`, chamfer) | — | No portado: sólo ML |
| `src/lib/parser/classifier/assembly.ts`, `metrics.ts` | `Compiler/GlyphAstBuilder` | Fiel en lo esencial |
| `src/lib/compiler/spellBuilder.ts` | `Compiler/SpellCompiler` | **Fiel** |
| `src/lib/compiler/semanticRules.ts` | `Compiler/SemanticRules` | **Fiel** |
| `src/lib/compiler/spellQuality.ts`, `spellDirection.ts` | `Compiler/SpellQuality`, `SpellDirection` | **Fiel** |
| `src/lib/compiler/reading/`, `plan/` | — | No portado (KI-023, ROADMAP F4) |
| `src/lib/dictionary/{sigils,signs}/*.json` | `Data/Dictionary/` + `GlyphDictionary.asset` | Copia exacta (sync) |
| `static/models/*` | `Data/glyph-recognizer.onnx*` | Copia exacta (GlyphStudent) |
| `src/lib/cast/` (renderer) | `Output/` + VFX Graph | Reinterpretado en 3D |

## Novedades del pull 2026-10-07 y qué nos sirve

| Novedad | ¿Sirve? | Estado |
|---|---|---|
| **GlyphStudent**: modelo destilado 0.9 MB (antes ResNet18 22 MB), misma I/O | Sí, clave para Quest | ✅ Adoptado |
| `src/lib/compiler/CLAUDE.md`, `parser/CLAUDE.md`, `dictionary/CLAUDE.md` | Sí: documentan invariantes (activación = cierre del anillo, etc.) | ✅ Aplicados en el port |
| Diccionario: ajustes en `column` y `weave` | Sí | ✅ Vía sync |
| **Capa Plan** (`compiler/plan/`, `reading/`): reglas canon R‑01…R‑23 (columnas que se inclinan, pulls que forman vórtices, regiones, weave con corrientes) | Sí, es la "física" de cada signo; ideal para el sandbox 3D | ⬜ F4 |
| `docs/animation-spec.md`, `animation-redesign.md`, `ground-truth.md` | Sí: especificación de qué hace cada hechizo en espacio y tiempo | 📖 Leer antes de F4 |
| Agrupado por **búsqueda de particiones** (`grouping/partition.ts`, `hypotheses.ts`) | Probablemente (KI-020) | ⬜ Si el agrupado simple falla |
| Cast con sonido generado por física (`cast/sound/`) | Ideas para audio diegético | ⬜ Opcional |
| Entrenamiento: `vector_augment.py`, `run_glyph_eval.py` | Sólo si reentrenamos | — |
| Tutorial del primer hechizo, librería, presets | Ideas de UX (grimorio) | ⬜ F6 |

## Docs útiles de la referencia
- `docs/recognition.md` — pipeline híbrido, entrada/salida del modelo, rotación.
- `docs/spell-ir.md`, `docs/glyph-ast.md` — contratos.
- `docs/play-rules.md` — anillo abierto/cerrado, capas.
- `docs/animation-spec.md` — reglas R‑xx del comportamiento de cada hechizo.
- `src/lib/compiler/CLAUDE.md` — invariantes del compilador.

Al portar algo, citar el archivo de origen en un comentario y actualizar esta tabla.
