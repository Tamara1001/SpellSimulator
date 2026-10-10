---
tags: [doc, datos]
---
# Glifos

## Modelo
`Assets/_Project/Data/glyph-recognizer.onnx` (+ `.onnx.data`, + `glyph-class-to-idx.json`) — **GlyphStudent**: CNN de
439K parámetros destilada de un ResNet18 (wha-spell-simulator, oct‑2026). 0.9 MB (antes 22 MB).

- Entrada `image` `[N,1,96,96]` float32: fondo −1, tinta +1, con antialias (valores intermedios).
- Salidas: `class_logits` `[N,26]` (**logits**: se aplica softmax), `angle`, `scale`, `center` (pose, sin uso todavía).
- Rasterizado (`TensorRasterizer`) = `renderCandidateTensor` de la referencia: canvas 2×, línea 2 px, margen 20%,
  escala uniforme anclada arriba‑izquierda, promedio 2×2.
- TTA: cada candidato se infiere en 8 rotaciones (0°, 45°…315°) y se promedia el softmax.
- Validación: el test `ModelTemplateTests` reconoce **20/20** plantillas del diccionario.
- Las advertencias `MaxPool ceil_mode/storage_order` al importar son inofensivas (el grafo usa 0).

## Diccionario como datos
Los JSON de `wha-spell-simulator/src/lib/dictionary/{sigils,signs}` se copian a `Assets/_Project/Data/Dictionary/` y se
agrupan en `GlyphDictionary.asset`. Para actualizar: **SpellSimulator > Dictionary > Sync From Reference**
(copia JSON; el modelo se copia también desde el menú) o **Rebuild Asset** si editaste los JSON a mano.

Cada entrada aporta: `kind` (por carpeta), `element`, `referenceSizeNorm`, `allowedLayers`,
`semantic {force, focus, spread, range, lifetimeBias, manifestation, directionMode}` y `strokeTemplate`.

## Tabla (valores actuales del diccionario)

Deltas = `force / focus / spread / range / lifetimeBias`. Sigilos permitidos en centro/medio/exterior; signos en medio/exterior.

| Idx | id | Rol | Elemento | Manifestación · dirección | Deltas | Tamaño ref. |
|---|---|---|---|---|---|---|
| 0 | aeroform | sigilo | wind | — | -0.04 / -0.05 / 0.08 / 0 / 0.03 | |
| 1 | billowing | signo | | billowing · position | -0.18 / -0.20 / 0.28 / 0 / 0.20 | 0.237 |
| 2 | collection | signo | | collection · inward | 0.08 / 0.20 / -0.22 / -0.06 / 0 | 0.172 |
| 3 | column | signo | | column · inward | 0.30 / 0.35 / -0.24 / 0.18 / 0 | 0.154 |
| 4 | convergence | signo | | convergence · inward | 0.08 / 0.36 / -0.32 / -0.04 / 0.08 | 0.165 |
| 5 | cool | **sin entrada** | | | | |
| 6 | crush | signo | | crush · position | 0.26 / 0.08 / 0.06 / 0 / 0 | 0.235 |
| 7 | crystal | sigilo | earth | — | -0.02 / 0.06 / -0.04 / 0 / 0.05 | |
| 8 | dispersion | signo | | dispersion · position | -0.04 / -0.30 / 0.34 / 0.10 / 0 | 0.183 |
| 9 | earth | sigilo | earth | — | 0.10 / 0.10 / -0.06 / -0.02 / 0.12 | |
| 10 | empower | **sin entrada** | | | | |
| 11 | entwine | **sin entrada** | | | | |
| 12 | fire | sigilo | fire | — | 0.12 / 0.04 / 0.08 / 0.02 / -0.03 | 0.21 |
| 13 | float | signo | | levitation · position | -0.06 / -0.04 / 0.06 / 0 / 0.26 | 0.22 |
| 14 | focus | **sin entrada** | | | | |
| 15 | gather | **sin entrada** | | | | |
| 16 | levitation | signo | | levitation · orientation | 0.12 / 0.02 / 0.08 / 0.18 / 0.28 | 0.176 |
| 17 | light | sigilo | light | — | 0.02 / 0.16 / 0.04 / 0.16 / 0.02 | |
| 18 | orb | **sin entrada** | | | | |
| 19 | pull | signo | | pull · inward | 0.18 / 0.16 / -0.22 / 0.16 / 0 | 0.205 |
| 20 | region | signo | | directed · orientation | 0.06 / 0.16 / -0.14 / 0.24 / 0 | 0.154 |
| 21 | repetition | signo | | repetition · position | 0 / 0 / 0 / 0 / 0.34 | 0.326 |
| 22 | water | sigilo | water | — | 0.02 / -0.04 / 0.12 / 0.04 / 0.08 | |
| 23 | weave | signo | | weave · position | -0.04 / 0.10 / 0.10 / 0 / 0.06 | 0.209 |
| 24 | wind-directs-air | sigilo | wind | — | 0.08 / 0.08 / 0.02 / 0.14 / -0.02 | |
| 25 | wind-underfoot | sigilo | wind | — | -0.02 / -0.03 / 0.05 / 0.04 / 0 | |

Los 6 glifos **sin entrada** existen en el Sample Maker de la referencia y el modelo los reconoce, pero no tienen
semántica: se reportan como desconocidos (`unknown_class`) y no rompen el hechizo. Para darles efecto, agregar un JSON.

**Direction modes**: `position` = hacia donde está el signo · `inward` = hacia el centro (opuesto a su posición) ·
`orientation` = sentido en que se dibujó.

**VFX por elemento**: fire → `Prefabs/VFX/Fire.prefab` (VFX Graph); water/wind/earth/light → `Prefabs/VFX/{Water,Air,Earth,Light}.prefab`.

## Efectos de los signos (octava tanda)
Los signos del anillo ya no solo suman potencia: cada uno tiene efecto propio (crush, collection, billowing, repetition, weave, región, además de columna, dispersión, levitación, pull y convergencia). Tabla completa en [GAME_DESIGN](GAME_DESIGN.md#signos-y-sus-efectos); implementación en `PlanResolver` (signo -> `SpellPlan`) y `MagicField`/`EarthShapes` (efecto). El significado sigue siendo data del diccionario.
