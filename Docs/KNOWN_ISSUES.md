---
tags: [doc, bugs]
---
# Known Issues y Deuda Técnica

Formato: `KI-NNN` · severidad (🔴 alta, 🟠 media, 🟢 baja) · estado (`abierto` / `resuelto <fecha>`).
Al resolver uno, no lo borres: marcalo resuelto y explicá cómo.

## Abiertos

### KI-020
🟠 `parcial` — **Agrupado de trazos.** Ahora: afinidad portada de la referencia (tocarse, encerrar, compartir centro) +
lectura guiada por reconocimiento barata (`SealReader`: hipótesis "capa media fusionada al sigilo" en el mismo batch y
modo radial si nada fue válido). Cubre sigilos grandes con partes sueltas (fuego, agua, tierra, luz, viento: test
end-to-end). Si nada es válido, prueba también **el sigilo sin sus partes sueltas** (`SealParser.PeelLooseFromSigil`):
lo que no toca el núcleo pasa a ser signo (fixture `fire_columnas_mal`). Falta la búsqueda de particiones completa de
la referencia (`partition.ts`).

### KI-042
🟠 `abierto` — **Columna dibujada con la barra curva o separada del palito se lee como `region`** (fixture
`fire_columnas_bien`: 0.62 region / 0.38 column). Es una confusión del modelo entre dos glifos parecidos. Opciones: tutorial
que muestre la columna canónica (barra recta tocando el palito), o desempate por geometría (la región es un arco sin
palito). Pendiente de más fixtures.

### KI-022
🟢 `abierto` — El gate de ambigüedad del sigilo (`PrimarySigilAmbiguityGap = 0.05`) es inalcanzable con softmax puro:
para pasar `≥ 0.62` la alternativa no puede superar 0.38. En la referencia la confianza es un score híbrido
(template matcher + ML). Queda como red de seguridad.

### KI-023
🟠 `parcial` — **Capa Plan portada en parte.** Están: lectura con orientación, columnas (chorro, abanico, vórtice),
levitación (orbe), pull (succión), convergencia (lente), dispersión como fuga y spin-up (R‑21). Faltan: **region**
(cercos y válvulas, `region.ts` + `chevrons.ts`), **weave**, **orb/vessel** y snapping de simetría. Hoy esos signos aportan
solo potencia y dejan una nota `unmodeled-*`.

### KI-038
🟢 `abierto` — **Partículas sin presupuesto medido en Quest**: cada hechizo puede llegar a ~1000 partículas (chorro 420/s).
El límite de 6 hechizos activos es provisorio. Medir en el dispositivo (M3/M7).

### KI-039
🟢 `abierto` — `SpellManifestation.FastForward` adelanta partículas y tiempos pero no la física. Sirve para capturas y
previews, no para simular resultados.

### KI-024
🟢 `abierto` — `Assets/_Recovery/0.unity` (escena de recuperación tras un crash) y `_Project/DebugCanvasCapture.png`
siguen en el repo. No se borraron porque pueden tener trabajo tuyo: revisar y decidir.

### KI-007
🟢 `abierto` — El cast aloca (listas, `float[]` por predicción, `StringBuilder` de diagnóstico, `Texture2D` de debug).
Es aceptable porque ocurre sólo al castear, nunca por frame. El buffer de entrada del clasificador sí se reutiliza.

### KI-034
🟢 `abierto` — **Dibujo por contacto en VR sin probar** (`PhysicalTool.vrContactDrawing`, apagado por defecto). Activarlo
en el Inspector del lápiz y probar en el Quest; el `RaycastTool` de la punta debe apuntar hacia el papel.

### KI-026
🟢 `abierto` — Sin PlayMode tests. El smoke test de cast en Play Mode se hizo a mano por MCP (ver [[HARNESS]]);
convendría automatizarlo.

### KI-043
🟢 `abierto` — **Fractura simple por grilla**: los fragmentos son cubos (se ve bien en cajas, no en objetos redondos).
Pre-fractura Voronoi pendiente (M5, mesas y paredes).

### KI-044
🟢 `abierto` — Las llamas de un objeto que gira quedan inclinadas (se orientan al crearse). Menor.

### KI-048
🟢 `abierto` — **Agua sin simulación de altura**: las ondas son analíticas (16 a la vez) y el nivel no baja al evaporar
ni al succionar. El vapor usa el look del viento. Suficiente para Quest; revisar si el lago grande de M5 lo pide.

### KI-049
🟠 `abierto` — **Post-proceso en Quest**: bloom + tonemapping + viñeta cuestan en GPU móvil y en VR la viñeta molesta.
Decidir con la medición de M7 (probablemente: solo tonemapping y color en Quest). El pasto es estático (sin viento).

### KI-051
🟢 `abierto` — **Audio sin escuchar por el agente**: los sintetizados se validaron por forma y duración, no de oído.
La música es un pad de 64 s en loop (puede cansar). Ajustar volúmenes y elegir/reemplazar sonidos tras la prueba de
Tamara; con muchas cosas ardiendo suenan muchos loops de fuego a la vez (poner un tope si molesta).

### KI-054
🟢 `abierto` — En VR el texto de ayuda y los avisos van a la HUD de pantalla y no se ven. Pasar a un texto en el mundo
(junto al rayo o en la muñeca) en M7.

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
| KI-021 🟠 | No había forma de limpiar el lienzo | Trapo (`RagTool`): frotarlo limpia todo y rearma el sello. |
| KI-032 🟠 | Curvas de un glifo leídas como anillo | `RingDetector.IsGlyphCircle` (colas fuera de la banda, trazos que cruzan), de `glyphCircle.ts`. Tests. |
| KI-033 🔴 | **Nuevo:** `Tensor<float>(shape, array)` copia el array entero, así que un cast con menos candidatos que el anterior lanzaba excepción | El buffer mide exactamente el batch. Lo encontró un test end-to-end. |
| KI-035 🟠 | **Nuevo (prueba manual):** sigilo grande partido en sigilo + signos | Ver KI-020. Test `UserCaseTests`. |
| KI-036 🟢 | **Nuevo (prueba manual):** dibujar se sentía lento | Estabilizador por plataforma (PC 40, VR 14) y sensibilidad al dibujar 0.2 → 0.6. |
| KI-025 🟢 | No se usaban las cabezas de pose del modelo | `GlyphClassifier` lee `angle`, calibra con plantillas y da la rotación de cada glifo (test: error < 25° en 7 poses). |
| KI-040 🔴 | **Nuevo (física):** el campo aplicaba aceleración sin límite, y un cuerpo liviano salía a cientos de m/s | Modelo de arrastre hacia la velocidad del flujo, ráfaga acotada, `Respawnable`. |
| KI-041 🟠 | **Nuevo (plan):** el ruido de pose creaba chorros o giros fantasma en sellos simétricos | Zona muerta hacia las clases canónicas + umbral relativo de primitivas (20%). |
| KI-037 🟢 | La succión (pull) atraía cualquier Rigidbody | Solo atrae `IElementMatter` de su elemento (piedra = tierra, lo que arde = fuego); el pull de agua tira un chorro desde un `WaterVolume`. |
| KI-047 🟢 | **Nuevo (M4):** el estanque se armó a y=0 y el piso real está a 0.5 (cubo) | Estanque subido. Ojo al ubicar props: la cara de arriba del `Floor` es y=0.5. |
| KI-056 🟠 | **Prueba manual:** no se podía entrar a la torre sin saltar (el zócalo subía 60 cm en la puerta) | Zócalo cortado en la puerta y bajado a 25 cm; umbral de 5 cm. |
| KI-057 🟠 | **Prueba manual:** el jugador se trababa con muebles y bordes (cápsula de 1 m de ancho, esfera extra, fricción) | Cápsula de radio 0.3, sin la esfera, material sin fricción, subida automática de escalones (≤ 35 cm) y gravedad contra el piso cuando está quieto (no resbala en rampas). Silla y atril fuera del paso puerta → rampa. |
| KI-058 🟢 | **Prueba manual:** el dorso de los lienzos no se veía | `Canvas.mat` de doble cara. |
| KI-055 🟢 | **Nuevo (M6):** los textos de ayuda ("Agarrar …") se armaban en cada frame al mirarlos (GC por frame) | Se arman una vez y se guardan. |
| KI-052 🟠 | **Prueba manual:** no se podía subir al segundo piso: la cabeza chocaba con el piso de arriba (2.0 m libres, el jugador mide ~2.1) | Hueco del piso mayor: 3 m libres en toda la rampa (medido con rayos: mínimo 2.98 m). |
| KI-053 🟢 | **Prueba manual:** las hojas soltadas quedaban suspendidas y de pie (gravedad apagada desde el laboratorio) | Al soltar, gravedad + frenado del aire + torque que las acuesta. Papeles fijos (pared, escritorio) ya no se agarran. |
| KI-050 🟢 | **Nuevo (M5):** la cascada quedaba tapada: en laderas empinadas la malla del terreno se aparta varios metros de la altura teórica | La cinta sigue la malla real (raycast contra `Terrain`). |
| KI-045 🟠 | **Nuevo (M4):** el calor de lo que ardía crecía sin tope y el agua se evaporaba antes de apagarlo | Tope de calor (2× ignición) + test `Water_ExtinguishesALongFire_StillUnderFire`. |
| KI-046 🟢 | **Nuevo (M4):** el reinicio movía cuerpos cinemáticos (herramienta en la mano) | `RespawnAll` ignora cinemáticos. |
| KI-031 🟢 | **Nuevo:** suelo detectaba triggers (lienzo sostenido) | `QueryTriggerInteraction.Ignore`. |
