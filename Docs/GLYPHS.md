---
tags: [doc, datos]
---
# Glifos

Modelo: `Assets/_Project/Data/glyph-recognizer.onnx` (+ `.onnx.data`) — **idéntico** (md5) al de
`wha-spell-simulator/static/models/`. Mapa: `glyph-class-to-idx.json` (26 clases, idéntico).

- Entrada: `image` `[N,1,96,96]` float32, fondo **-1**, tinta **+1**, recortado al bounding box con margen 10%/lado,
  ancho de trazo ≈ 2 px (en la referencia `strokeWidth: 2`). En Unity: `TensorRasterizer` con
  `debugStrokeRadius = 1` (valor serializado en `SystemManagers.prefab`; el default del código es 3).
- Salidas: `class_logits` `[N,26]` (**logits, sin softmax**), `angle` (sin, cos), `scale`, `center`.
  Unity sólo lee la primera salida vía `PeekOutput()`. Ver [[KNOWN_ISSUES#KI-001]].
- Rotación: el modelo se entrenó con *augmentation* de rotación; todos los glifos del diccionario son
  `recognitionRotationInvariant: true`.

## Tabla

Deltas = `force / focus / spread / range / lifetimeBias`.
"Ref." = valores del diccionario de la referencia (`src/lib/dictionary/*.json`, campo `semantic`).
"Unity" = valores en `SpellCompiler.SignSemantics`.

| Idx | id | Rol | Elemento (Unity) | Deltas Ref. | Deltas Unity |
|---|---|---|---|---|---|
| 0 | aeroform | sigilo | Wind | -0.04 / -0.05 / 0.08 / 0 / 0.03 | — (sigilos sin deltas) |
| 1 | billowing | signo | — | -0.18 / -0.20 / 0.28 / 0 / 0.20 | 0.06 / -0.08 / 0.20 / 0.10 / -0.05 |
| 2 | collection | signo | — | 0.08 / 0.20 / -0.22 / -0.06 / 0 | 0.04 / 0.08 / -0.06 / -0.05 / 0 |
| 3 | column | signo | — | 0.30 / 0.35 / -0.24 / 0.18 / 0 | 0.12 / 0.18 / -0.08 / 0.18 / 0.10 |
| 4 | convergence | signo | — | 0.08 / 0.36 / -0.32 / -0.04 / 0.08 | 0 / 0.12 / -0.18 / 0 / 0 |
| 5 | cool | **sin rol** | — | *(no está en el diccionario)* | — |
| 6 | crush | signo | — | 0.26 / 0.08 / 0.06 / 0 / 0 | 0.18 / 0.14 / -0.12 / -0.08 / 0 |
| 7 | crystal | sigilo | Earth (variante) | -0.02 / 0.06 / -0.04 / 0 / 0.05 | — |
| 8 | dispersion | signo | — | -0.04 / -0.30 / 0.34 / 0.10 / 0 | 0.05 / -0.10 / 0.18 / 0.12 / -0.05 |
| 9 | earth | sigilo | Earth | 0.10 / 0.10 / -0.06 / -0.02 / 0.12 | — |
| 10 | empower | **sin rol** | — | *(no está en el diccionario)* | — |
| 11 | entwine | **sin rol** | — | *(no está en el diccionario)* | — |
| 12 | fire | sigilo | Fire | 0.12 / 0.04 / 0.08 / 0.02 / -0.03 | — |
| 13 | float | signo | — | -0.06 / -0.04 / 0.06 / 0 / 0.26 | -0.08 / 0.04 / 0.06 / -0.06 / 0.05 |
| 14 | focus | **sin rol** | — | *(no está en el diccionario)* | — |
| 15 | gather | **sin rol** | — | *(no está en el diccionario)* | — |
| 16 | levitation | signo | — | 0.12 / 0.02 / 0.08 / 0.18 / 0.28 | -0.06 / 0.06 / 0.04 / -0.04 / 0.05 |
| 17 | light | sigilo | Light | 0.02 / 0.16 / 0.04 / 0.16 / 0.02 | — |
| 18 | orb | **sin rol** | — | *(no está en el diccionario)* | — |
| 19 | pull | signo | — | 0.18 / 0.16 / -0.22 / 0.16 / 0 | 0.08 / 0.10 / -0.05 / -0.08 / 0 |
| 20 | region | signo | — | 0.06 / 0.16 / -0.14 / 0.24 / 0 | -0.04 / -0.04 / 0.10 / -0.10 / 0 |
| 21 | repetition | signo | — | 0 / 0 / 0 / 0 / 0.34 | 0 / 0 / 0 / 0 / 0.12 |
| 22 | water | sigilo | Water | 0.02 / -0.04 / 0.12 / 0.04 / 0.08 | — |
| 23 | weave | signo | — | -0.04 / 0.10 / 0.10 / 0 / 0.06 | 0.04 / -0.06 / 0.12 / 0.08 / 0.02 |
| 24 | wind-directs-air | sigilo | WindDirectsAir | 0.08 / 0.08 / 0.02 / 0.14 / -0.02 | — |
| 25 | wind-underfoot | sigilo | WindUnderfoot | -0.02 / -0.03 / 0.05 / 0.04 / 0 | — |

**Notas**
- Los 6 glifos "sin rol" (`cool`, `empower`, `entwine`, `focus`, `gather`, `orb`) existen en el Sample Maker de la
  referencia y el modelo los reconoce, pero no tienen semántica. Hoy el compilador devuelve `unclassified_glyph`.
- Los deltas de Unity **no coinciden** con la referencia aunque el comentario del código diga que son copiados.
  Ver [[KNOWN_ISSUES#KI-003]]. Además la referencia tiene `manifestation` y `directionMode` por signo, que Unity no porta.
- VFX por elemento: Fire → `VFX/Fire.prefab` (VFX Graph); Water/Air/Earth/Light → `Prefabs/VFX/*.prefab`.
