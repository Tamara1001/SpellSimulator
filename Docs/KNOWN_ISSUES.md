---
tags: [doc, bugs]
---
# Known Issues y Deuda Técnica

Formato: `KI-NNN` · severidad (🔴 alta, 🟠 media, 🟢 baja) · estado (`abierto` / `resuelto <fecha>`).
Al resolver uno, no lo borres: marcalo resuelto y explicá cómo.

## Abiertos

### KI-020
🟠 `abierto` — **Agrupado de trazos simplificado.** `StrokeGrouper` usa umbral radial (0.40 r) + proximidad. La
referencia hace búsqueda de particiones guiada por reconocimiento (`parser/grouping/partition.ts`): separa un signo
pegado al sigilo y mantiene juntos los puntos sueltos de un glifo. Síntoma esperado: un signo dibujado en la capa media
muy cerca del centro se suma al sigilo. Próximo paso: probar con dibujos reales y, si falla, portar `hypotheses.ts`.

### KI-021
🟠 `abierto` — **No hay forma de limpiar el lienzo completo** desde el juego (los botones del HUD viejo estaban
inactivos y se eliminaron). Sólo goma (trazo a trazo) y deshacer. Propuesta: mantener secundario de la goma = limpiar.

### KI-022
🟢 `abierto` — El gate de ambigüedad del sigilo (`PrimarySigilAmbiguityGap = 0.05`) es inalcanzable con softmax puro:
para pasar `≥ 0.62` la alternativa no puede superar 0.38. En la referencia la confianza es un score híbrido
(template matcher + ML). Queda como red de seguridad.

### KI-023
🟢 `abierto` — **Capa Plan no portada** (`compiler/plan/`: columnas, regiones, vórtices, R‑xx). `SpellIR` tiene
manifestaciones y escalares, pero no el "plan" que la referencia anima. Ver [[ROADMAP]] F4.

### KI-024
🟢 `abierto` — `Assets/_Recovery/0.unity` (escena de recuperación tras un crash) y `_Project/DebugCanvasCapture.png`
siguen en el repo. No se borraron porque pueden tener trabajo tuyo: revisar y decidir.

### KI-025
🟢 `abierto` — No se aprovechan las cabezas de pose del modelo (`angle`, `scale`, `center`). La orientación de cada
signo sale de la geometría (`DirectedOrientationDeg`), no de la red. La referencia calibra el ángulo por glifo.

### KI-007
🟢 `abierto` — El cast aloca (listas, `float[]` por predicción, `StringBuilder` de diagnóstico, `Texture2D` de debug).
Es aceptable porque ocurre sólo al castear, nunca por frame. El buffer de entrada del clasificador sí se reutiliza.

### KI-026
🟢 `abierto` — Sin PlayMode tests. El smoke test de cast en Play Mode se hizo a mano por MCP (ver [[HARNESS]]);
convendría automatizarlo.

## Resueltos (2026-10-07, reestructura del pipeline)

| KI | Problema | Resolución |
|---|---|---|
| KI-001 🔴 | Logits usados como probabilidades | `ClassificationMath.Softmax` + promedio TTA. Test de regresión. |
| KI-002 🟠 | Hechizo spawneaba en `SystemManagers` | `SpellOutputController` recibe el lienzo: posición + normal + dirección papel→mundo. |
| KI-003 🟠 | Deltas no coincidían con la referencia; sigilos sin semántica | Diccionario como datos (JSON de la referencia) + port fiel de `semanticRules.ts`/`spellBuilder.ts`. |
| KI-004 🟠 | Anillo sin sigilo → inválido silencioso | `GlyphWarnings` con texto, publicado en la UI de diagnóstico. |
| KI-005 🟠 | `active` siempre true | `RingDetector` mide cobertura: cerrado = activo, abierto = preparado. |
| KI-006 🟢 | Rotación manual de signos al norte | Reemplazada por TTA de 8 rotaciones, como la referencia. |
| KI-008 🟢 | Parser JSON casero, campos muertos | `GlyphDictionary` con Newtonsoft; compilador reescrito. |
| KI-009 🟠 | `FindFirstObjectByType` en cada cast, constantes duplicadas | `SpellCaster` referenciado por `PhysicalTool`; parámetros en un solo lugar. |
| KI-010 🟢 | Cursor bloqueado en `PlayerLook` | Sólo `PlatformDetector`; Escape libera el cursor. |
| KI-011 🟢 | Rotación de 90° hardcodeada; `Shader.Find` en runtime | `vrRayPitchOffset` serializado; material `VRPointer.mat` asignado. |
| KI-012 🟢 | Un solo `IUsable` | Usable por mano; la derecha tiene prioridad. |
| KI-013 🟢 | Radio de goma en UV, `GetComponent` por llamada | Radio en metros; bounds del mesh cacheados. |
| KI-014 🟢 | `MouseInputHandler` legacy | Eliminado junto con los botones del HUD. PC y VR usan las herramientas físicas. |
| KI-015 🟢 | Carpetas VFX duplicadas, editor scripts sin menú | Todo en `Prefabs/VFX/`; `HarnessProjectChecks` reemplaza `CheckURP`/`VFXDiagnostics`. |
| KI-016 🟢 | Sin asmdefs ni tests | 3 asmdefs, 44 tests EditMode. |
| KI-017 🟢 | README desactualizado | Actualizado y apuntando a `Docs/`. |
| KI-018 🟠 | **Nuevo:** trazos en UV ⇒ en lienzos no cuadrados un círculo era una elipse para parser y red | `DrawingCanvas.UvToIsotropic`; el caster trabaja en espacio isotrópico. |
| KI-019 🔴 | **Nuevo:** `RedrawSession` llamaba `ClearCanvas`, que vaciaba la sesión ⇒ **borrar o deshacer un trazo borraba todo** | `Redraw()` (sólo textura) separado de `Clear()`. |
| KI-027 🟠 | **Nuevo:** rasterizado distinto al de entrenamiento (centrado, binario, grosor 2 r) | `TensorRasterizer` replica `renderCandidateTensor`: anclado arriba‑izq., 2× + promedio, línea 2 px. 20/20 plantillas reconocidas. |
| KI-028 🟢 | **Nuevo:** Douglas‑Peucker dividía por cero en trazos que se cruzan; trazos cortos compartidos por referencia | Distancia a punto en segmentos degenerados; copia de trazos. Tests. |
| KI-029 🟢 | **Nuevo:** clustering de signos no transitivo | Union‑find. Test. |
| KI-030 🟢 | **Nuevo:** `renderer.material` instanciado nunca se destruía | `DrawingCanvas.OnDestroy` lo destruye. |
| KI-031 🟢 | **Nuevo:** suelo detectaba triggers (lienzo sostenido) | `QueryTriggerInteraction.Ignore`. |
