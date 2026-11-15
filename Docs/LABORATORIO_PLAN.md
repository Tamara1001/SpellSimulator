---
tags: [doc, plan]
---
# Plan: el Laboratorio (lentes de tinta, mesa de variables y registro CSV)

> Plan de implementación del diseño ya aprobado. Cumple la consigna de *Simuladores*: parámetros modificables, ajuste en tiempo real,
> registro de cada cambio y su resultado, y algo que mostrar en la mesa de examen. Estado: **implementado (fases 0, 1, 2a y 2b)**;
> falta la fase 3 (fuente y multiplicador de fuerza). Sin probar a mano ni en VR (KI-129). Ver «Decisiones de la implementación» al final.

## 1. Qué hay hoy y qué se reutiliza (investigado)

| Tema | Lo que existe | Uso en el Laboratorio |
|---|---|---|
| Panel F3 | `Debug/DiagnosticUIController.cs:12-97`: UI en pantalla que solo muestra **strings** de `DiagnosticBus` (`Debug/DiagnosticBus.cs:16-28`); toggle en `LateUpdate` con `PlayerInputReader.IsTogglingDebug` (`Player/PlayerInputReader.cs:68,326`). | **No sirve de base**: es texto armado en pantalla (no se ve en VR, KI-095) y sin datos estructurados. Las etiquetas leen los datos de los componentes. |
| Texto 3D sin GC | `Combat/DamageNumbers.cs:10-86` (pool fijo de `TextMeshPro`, mira a la cámara en `LateUpdate`); `Output/SealAnnotations.cs:15-60` (pool por lienzo, escucha `SpellCaster.SealAnalyzed`). | Modelo del pool del overlay. Actualizar con `TMP_Text.SetText("{0:0.0}", v)` (no aloca). |
| Datos del hechizo | `SpellCaster.SpellCompiled(SpellIR, DrawingCanvas)` (`Casting/SpellCaster.cs:60,392,521,538`). `SpellIR`: `element, force, range, duration, quality, inkPower, sealRadius` (`Compiler/SpellIR.cs:52-95`). `MagicField.Active` (`Output/MagicField.cs:36`), `WindSpeed`, `ConeLength`, `MainDirection`, `Frame` (`:44-75`). | Etiqueta sobre cada sello lanzado y flechas del viento de hechizo. |
| Botones apuntando | `IPressable` con `PressWithClick` (`Interfaces/InteractionInterfaces.cs:77-83`); `GrimoireButton` (`Learning/GrimoireButton.cs:14-93`). El interactor detecta con un rayo (cámara en PC, mando derecho en VR, `Player/PlayerInteractor.Hints.cs:139-168`) y aprieta con clic/gatillo (`Player/PlayerInteractor.cs:242-249`) o con E/grip (`:199`). | Perillas = placas `−`/`+` y botón «materializar» como `IPressable` con clic. **Funciona igual en PC y VR sin input nuevo.** |
| Objetos movibles | `World/Movable.cs:12-128`: `maxMass = 600`, resorte que se ablanda con la masa (`:69-70`); **no hay registro** de instancias. | Caja materializada = `Movable` + `Rigidbody.mass`. Agregar registro estático. |
| Fuego | `Reactions/GrassFire.cs`: grilla 70×70 de 2 m (`:28-33`), paso lógico 0,25 s (`:44`). Ya **serializados**: `spreadChance 0.045`, `burnSeconds 4`, `heatPerSecond 5`, `generationDecay 0.78`, `maxGenerations 6` (`:69-95`), `ApplyDefaults()` (`:350-358`), `RebuildDecayTable()` privado (`:648`), `Ignite(p, r)` (`:341`), contadores `BurningCells/BurntCells` (`:212-214`), `AddWindBias` (`:303`), `IsFireproof` privado (`:639-645`), reset por `MagicWorld.WorldReset` (`:257,361`). | Ajustar decay/maxGen/spread con un setter nuevo; materializar con `Ignite`; área = celdas × 4 m². |
| Viento | `Output/Bodies/WindMath.cs` (puro y testeado: `Drag`, `ProjectedArea`, `SpreadBias`); `WindCone` necesita un `SpellIR` + `MagicField` (`Output/Bodies/WindCone.cs:32-56`). `MagicField` ignora cuerpos > 60 kg y > 250 kg en algunos efectos (`Output/MagicField.cs:240,255`). | `LabWindZone` propio usando `WindMath.Drag` + `GrassFire.AddWindBias`. **No** se fabrica un `SpellIR` falso (regla: todo cast pasa por `SpellCaster.Cast`). |
| Agua | `WaterSpell.ConfigureDrops(ps, inkPower, flowMultiplier, ignore)` público (`Output/Bodies/WaterSpell.cs:52`); fuente estática del Duelo (`Net/World/DuelArena.cs:93`). | Fase 3: fuente = `ParticleSystem` configurado con `ConfigureDrops`. |
| Fuerza del cast | `SpellCaster` completa `spell.inkPower` (`Casting/SpellCaster.cs:495,561`). | Fase 3: `PowerScale` de un solo uso multiplicado ahí. |
| Generador | `MapBuilder.Build()` (`Editor/Map/MapBuilder.cs:30-77`): mesa de estampas en `FieldCenter + (2, −10.5)` (`:58`), runas a −5 m en x (`:60`); `TowerBuilder.Table(...)` (`Editor/Map/TowerBuilder.cs:568`); `MapDetails.Stamps` configura componentes con `SerializedObject` (`Editor/Map/MapDetails.cs:317-380`); zona libre `StampArea` r 5 m (`Editor/Map/MapStructures.cs:57,184`); `MapLayout` conserva lo movido a mano **por ruta jerárquica** (`Editor/Map/MapLayout.cs:17-60`). | `Editor/Map/MapLab.cs` nuevo, llamado desde `Build()`. Nombres estables. |
| Reaparición/tope | `SupplyPoint` clona plantillas (`World/SupplyPoint.cs:127-134`); `WorldObjectBudget` (40 total, 12 por tipo, `World/WorldObjectBudget.cs:14-15`). | Tope propio del laboratorio (más chico) además del global. |
| Guardado | `SpellLibraryStore` (tmp + reemplazo, `.bak` si está corrupto, `Learning/SpellLibraryStore.cs:30-47`); `SealRecord` (`Casting/SealRecord.cs:75-84`); `Discovery` (`Learning/Discovery.cs:34,154`). No hay nada de WebGL para archivos. | Un log es *append*: `StreamWriter` + `Flush` por fila (el tmp-reemplazo es para reescribir archivos enteros). |
| Harness | `Editor/HarnessPlaySmoke.cs:78-297` (`Check(bool, nombre)`; ya prueba fuego del pasto y reinicio en `:154-161`). Tests en `Tests/EditMode/` (473). | Bloque «Laboratorio» en el smoke + 4 archivos de test. |

## 2. Arquitectura y contratos

Módulo nuevo `Scripts/Lab/` (namespace `SpellSimulator.Lab`). Depende de `World`, `Reactions`, `Output`, `Casting` (como `Casting` usa todo); **nadie depende de `Lab`**. Los módulos existentes solo ganan API genérica (setters, consultas, registro), nunca referencias al laboratorio.

**Lógica pura (estática, con test EditMode):**
- `LabVariableId` (enum: `Mass, WindYaw, WindSpeed, FireSeedRadius, FireDecay, FireMaxGenerations, WaterFlow, PowerScale`) y `LabVariableDef` (struct: id, rótulo, unidad, min, max, paso, default, formato). `LabCatalog.Defs` = la tabla de abajo.
- `LabParameterMath`: `Step(value, def, dir)`, `Snap`, `Clamp`, `IsDefault`, `Normalized(value, def)` (para el ángulo de la palanca).
- `LabCsv`: `Header`, `FormatRow(in LabLogRow)` con `CultureInfo.InvariantCulture`, `Escape` (comillas, `;`, saltos).
- `LabMetricsMath`: área quemada (celdas × 4 m²), desplazamiento horizontal, detector de reposo (`SettleStep(ref state, speed, dt)`).
- `LabOverlayMath`: elegir los N más cercanos dentro de un radio sobre arrays preasignados.

**Variables (pasos discretos; los defaults = valores actuales del código, así el balance no cambia):**

| Variable | Rango · paso | Default | Efecto |
|---|---|---|---|
| Masa de la caja | 1, 2, 5, 10, 25, 50, 100, 200, 400, 600 kg (escala logarítmica) | 10 | `Rigidbody.mass` de la caja nueva |
| Viento: dirección / velocidad | 0-315° · 45° / 0-20 m/s · 1 | 0° / 6 | `LabWindZone` |
| Fuego: radio de semilla | 0.5-4 m · 0.5 | 1 | `GrassFire.Ignite(p, r)` |
| Fuego: debilitamiento | 0.50-0.95 · 0.05 | 0.78 | `generationDecay` (global de la grilla) |
| Fuego: generaciones máx. | 0-12 · 1 | 6 | `maxGenerations` |
| Agua: caudal (fase 3) | 0.5-3 × · 0.25 | 1 | `ConfigureDrops(flowMultiplier)` |
| Multiplicador de fuerza (fase 3) | 0.5-2 × · 0.25 | 1 | `SpellCaster.PowerScale` del próximo cast |

**Componentes y eventos:**
- `LabParameters` (en la mesa, `static Current`): `float[] values`; `event Action<LabVariableId, float, float> ValueChanged` (id, anterior, nuevo); `Set`, `Step`, `ResetAll()` (botón «valores por defecto», emite un cambio por variable). Aplica en vivo las variables globales (fuego) vía `GrassFire.SetTuning`.
- `LabDial` (perilla: palanca + lectura TMP) y `LabStepPlate : IPressable` (`PressWithClick = true`, dir ±1). La palanca interpola su ángulo solo mientras anima (sin `Update` en reposo).
- `LabMaterializeButton : IPressable` → `LabSpawner.Materialize(LabSpawnKind)`.
- `LabSpawner`: crea caja / zona de viento / parche de fuego (/ fuente) en anclas fijas; topes y enfriamiento; limpia todo en `MagicWorld.WorldReset`; `event Action<LabSpawnKind, GameObject> Materialized`.
- `LabWindZone`: caja de 4×3×4 m; `FixedUpdate` con `Physics.OverlapBoxNonAlloc` (array cacheado) + `WindMath.Drag`; `GrassFire.Current.AddWindBias` cada 0,1 s; partículas de hojas/rayas; expira a los 30 s; registro estático `All` para el overlay.
- `LabMeasurements` (10 Hz, sin GC): segmentos de movimiento de cada caja (empieza con v > 0,3 m/s, termina al reposar 1 s o a los 15 s → distancia), episodios de fuego (desde el `Ignite` o un cast de fuego hasta `BurningCells == 0` o 40 s → área quemada), ventana de cast (5 s: desplazamiento máx. de cajas a < 8 m, `ConeLength`). `event Action<LabMeasurement> Measured`.
- `LabSessionLog` + `LabCsvWriter`: escucha `ValueChanged` (agrupado: una fila 0,6 s después del último paso, con el valor anterior al primer paso), `Materialized`, `SpellCaster.SpellCompiled`, `Measured`, `WorldReset`. Columnas: `fecha_hora;t_sesion_s;evento;variable;anterior;nuevo;resultado;valor_medido;unidad;id_experimento;notas`. Cada resultado se cuelga del `id_experimento` de la fila que lo originó.
- `LabOverlay` + `InkLenses : IPressable`: E/grip sobre las lentes las «pone» (desaparecen del soporte; `LabOverlay.Visible = true`), otra vez sobre el soporte las saca. Pool de 32 `TextMeshPro` + 8 flechas (`LineRenderer`); refresco 5 Hz; solo dentro de 12 m. Fuentes: hechizos vivos (`SpellCompiled` + `MagicField.Active`), `Movable.All` (masa, velocidad), muestras de `GrassFire` (generación, prob. efectiva = spread·decay^g), `LabWindZone.All` y conos de viento (flecha + m/s). Contadores fijos sobre la mesa: área quemada, alcance y duración del último experimento.

**Cambios en módulos existentes (API mínima, sin cambiar defaults):**
- `World/Movable.cs`: `static IReadOnlyList<Movable> All` (alta/baja en `OnEnable/OnDisable`), `Mass`, `Body`.
- `Reactions/GrassFire.cs`: getters `SpreadChance/GenerationDecay/MaxGenerations`; `SetTuning(spread, decay, maxGen)` (clamp + `RebuildDecayTable`); `int SampleBurning(Vector3 near, float radius, Vector3[] pos, byte[] gen)` sin GC; `static bool IsFireproofAt(Vector3)`.
- `Player/PlayerInteractor.cs`: no disparar `OnUseStart` de la herramienta si el clic fue a un `IPressable` con clic (hoy el lápiz en la mano también «usa» al apretar una placa, `:242-249` y `:315-325`).
- `Casting/SpellCaster.cs` (fase 3): `float PowerScale` (1 por defecto) multiplicado en `:495` y `:561`; el laboratorio lo pone y lo vuelve a 1 al recibir el siguiente `SpellCompiled`.
- `Editor/Map/MapBuilder.cs`: `MapLab.Build(map, lit, stampTable + offset)` después de `MapContraptions` (`:60`). `Editor/Map/MapStructures.cs`: ampliar la zona libre si la mesa cae fuera de `StampArea`. `Editor/FlammableSetup.cs`: excluir `Lab*` (que la mesa no se queme).

## 3. Archivos

**Nuevos** — `Scripts/Lab/`: `LabVariableDef.cs` (enum + struct + `LabCatalog`), `LabParameterMath.cs`, `LabCsv.cs`, `LabMetricsMath.cs`, `LabOverlayMath.cs`, `LabParameters.cs`, `LabDial.cs`, `LabStepPlate.cs`, `LabMaterializeButton.cs`, `LabSpawner.cs`, `LabWindZone.cs`, `LabMeasurements.cs`, `LabSessionLog.cs`, `LabCsvWriter.cs`, `LabOverlay.cs`, `InkLenses.cs`, (fase 3) `LabFountain.cs`. `Editor/Map/MapLab.cs`. `Tests/EditMode/`: `LabParameterMathTests.cs`, `LabCsvTests.cs`, `LabMetricsMathTests.cs`, `LabOverlayMathTests.cs`.
**Modificados** — `World/Movable.cs`, `Reactions/GrassFire.cs`, `Player/PlayerInteractor.cs`, `Casting/SpellCaster.cs` (fase 3), `Editor/Map/MapBuilder.cs`, `Editor/Map/MapStructures.cs`, `Editor/FlammableSetup.cs`, `Editor/HarnessPlaySmoke.cs`. Docs: `CHANGELOG`, `GAME_DESIGN` (lentes, mesa), `ARCHITECTURE` (módulo `Lab`), `STRUCTURE`, `DECISIONS` (ADR: viento propio y no `SpellIR` falso; log *append*), `KNOWN_ISSUES` si queda deuda. Controles: no cambian (no se tocan `GrimoireTexts`/`PlayerInputReader`).

## 4. Fases (en orden)

| Fase | Contenido | Terminado cuando | Tamaño |
|---|---|---|---|
| **0. API** | Cambios en `Movable`, `GrassFire`, `PlayerInteractor` + lógica pura + tests. | Compila; tests EditMode verdes (473 + ~25); defaults del fuego idénticos (test: `SetTuning` con defaults = estado de `ApplyDefaults`). | S (~250 líneas) |
| **1. Lentes** | `LabOverlay`, `InkLenses`; soporte con lentes en la mesa de estampas (por `MapLab`). | Con lentes: etiqueta de masa sobre cada caja a < 12 m, etiqueta sobre el sello lanzado, generación sobre el pasto en llamas, flecha y m/s del viento de un hechizo. Profiler: 0 B GC/frame con overlay activo. Smoke: «lentes: el overlay etiqueta una caja». | M (~450) |
| **2a. Mesa** | `MapLab` (mesa, 6 perillas, 3 botones de materializar, «por defecto», anclas), `LabParameters/LabDial/LabStepPlate/LabMaterializeButton/LabSpawner/LabWindZone`. | Masa, viento y fuego se ajustan apuntando + clic en PC y gatillo en VR; materializan caja/viento/fuego; el reinicio borra lo materializado y conserva los valores; «por defecto» restaura. Smoke: perilla sube un paso, caja de 600 kg con `rb.mass == 600`, reinicio la borra, `SetTuning` aplicado y restaurado. | L (~700 + 250 generador) |
| **2b. Registro** | `LabMeasurements`, `LabSessionLog`, `LabCsvWriter`, contadores en la mesa. | `registro-sesion.csv` con filas de perilla (agrupadas), materializado, hechizo y resultado medido (distancia de la caja, área quemada); se abre en Excel con acentos bien. Smoke: ≥ 4 filas con la ruta redirigida a `Logs/`. **Con esto se cumple la consigna.** | M (~400) |
| **3. Extras** | `LabFountain` (caudal) + `PowerScale`. | La fuente apaga fuego y llena charcos; el próximo cast sale ×N y el siguiente vuelve a ×1 (fila en el CSV). | S/M (~250) |

## 5. Riesgos y mitigación

1. **Rendimiento en Quest (overlay).** 32 TMP con outline + reordenar cada frame es caro en GPU tile-based. → Pool fijo, refresco 5 Hz (posición en `LateUpdate`, texto solo si cambió, con `SetText` sin GC), radio 12 m, sin outline en VR, overlay apagado por defecto; medir con el Profiler del MCP antes de cerrar la fase 1.
2. **Allocations.** Strings del CSV, `GetComponent` en el escaneo, lambdas. → Strings solo en eventos (no por frame); el escaneo usa registros estáticos (`Movable.All`, `LabWindZone.All`) y arrays cacheados (`OverlapBoxNonAlloc`); `GrassFire.SampleBurning` escribe en buffers del que llama; nada de LINQ. Chequear `Unity_Profiler_GetFrameGcAllocati…` con overlay + viento + fuego activos.
3. **Física inestable con 600 kg.** Relación de masas > 100:1 con cajas de 1 kg o papeles: jitter, atraviesa, sale despedido; arrastrarla con el resorte de `Movable` es lentísimo (`:69-70`). → Caja de tamaño fijo (0,6 m) para aislar la variable, `solverIterations = 12`, `maxDepenetrationVelocity = 2` (como `RockSpell.cs:65-67`), `ContinuousDynamic` solo si masa ≥ 100; aparece apoyada en un ancla libre (raycast al piso), nunca encima de otra caja; masa máx. 600 = `Movable.maxMass` (sigue siendo levantable, como hoy).
4. **Spawn sin límite.** → Topes del laboratorio: 4 cajas, 2 zonas de viento, 1 parche de fuego activo, 1 fuente (el más viejo se borra con el achique de `ExpiringBody`); enfriamiento de 1 s por botón; cajas registradas también en `WorldObjectBudget` (`supplyId "lab_box"`).
5. **Parámetros que rompen balance o tests.** El fuego es global: un decay de 0,95 con 12 generaciones puede quemar toda la pradera y también cambia los incendios de hechizos. → Rangos acotados (arriba), la grilla está acotada (4900 celdas, 36 llamas), los defaults del catálogo = constantes actuales (`GrassFire.cs:69-73`), «por defecto» llama a `ApplyDefaults`; el overlay muestra «fuego modificado» cuando difiere; los tests existentes no tocan `GrassFire` (no hay test de él), así que se agrega uno de la tabla de decay.
6. **Colisión con otros inputs.** Clic sobre una placa con el lápiz en la mano también «usa» la herramienta; la rueda ya es grosor/distancia; E sobre las lentes vs agarrar. → Placas con clic (como el grimorio) y guard en `HandleUsage`; no se usa la rueda ni teclas nuevas; las placas son triggers chicos separados ≥ 6 cm (rayo de VR con temblor) y con resaltado al apuntar.
7. **Reinicio/persistencia.** `MapBuilder.Build()` regenera la escena; el reinicio no conoce los objetos del laboratorio. → Todo lo materializado vive bajo `LabSpawned` (raíz de runtime) y se destruye en `WorldReset`; la mesa se genera con nombres estables (`Map/Lab/LabDial_Mass`…) para que `MapLayout` conserve ajustes; valores en memoria (no se guardan entre sesiones), fila «reinicio» en el CSV.
8. **Precisión de las métricas.** El fuego es aleatorio (RNG interno, `GrassFire.cs:126`) y depende de los pasos por frame; experimentos superpuestos se contaminan; un cuerpo que rueda nunca «reposa». → Cada medición tiene `id_experimento`, tiempo máximo y una marca `superpuesto` si hubo otro experimento del mismo tipo abierto; área por conteo exacto de celdas (resolución 4 m², se dice en la unidad); se recomienda repetir 3 veces por valor (texto en la mesa). Desplazamiento horizontal desde la posición al empezar el movimiento.
9. **WebGL.** `persistentDataPath` es IndexedDB (MEMFS + sincronización); el archivo no es accesible para la usuaria y puede no persistir. → `LabCsvWriter` con `#if UNITY_WEBGL && !UNITY_EDITOR` guarda en memoria y deja «exportar» como extra (descarga por `.jslib`); en PC escribe archivo; en Quest queda en `Android/data/<paquete>/files` (accesible por USB): decirlo en la mesa.
10. **Escritura del archivo.** Excel bloquea el archivo abierto; crash a mitad de línea. → `FileShare.ReadWrite`, *append* + `Flush` por fila; si falla, se sigue en memoria y se avisa con `Hud.Notify` una sola vez; UTF-8 con BOM para los acentos.
11. **Red.** En una sesión de red nada de esto se replica (KI-121) y `GrassFire` del cliente no simula. → El laboratorio se desactiva si `NetSession.Exists` (cartel «solo en modo Libre»).
12. **El fuego quema la mesa** (tablas con `ElementReactive` vía `FlammableSetup`). → Parche de fuego en un ancla a ≥ 8 m, mesa excluida de `FlammableSetup`, chequeo con `GrassFire.IsFireproofAt` (si el ancla cae en un camino no prende).
13. **El viento propio no es un `MagicField`**: no apaga velas (`WindCone.SnuffExposureAt`) ni lo ve el `Flutter`. → Aceptado y anotado en `KNOWN_ISSUES`; si molesta, interfaz `IWindSource` común (fuera de este plan).
14. **VR sin probar** (M7). → Todo es world-space y por rayo; queda en el resumen qué probar en el Quest.

## 6. Tandas para agentes (especificación cerrada, sin solaparse)

| Tanda | Quién | Archivos propios | Depende de |
|---|---|---|---|
| T1 lógica pura + tests | Haiku/Sonnet | `Lab/LabVariableDef.cs`, `LabParameterMath.cs`, `LabCsv.cs`, `LabMetricsMath.cs`, `LabOverlayMath.cs`, los 4 tests | — |
| T2 API en módulos | Sonnet | `Movable.cs`, `GrassFire.cs`, `PlayerInteractor.cs` | — |
| T3 lentes y overlay | Opus (estética) | `Lab/LabOverlay.cs`, `Lab/InkLenses.cs` | firmas de T1/T2 |
| T4 mesa y materializar | Sonnet | `Lab/LabParameters.cs`, `LabDial.cs`, `LabStepPlate.cs`, `LabMaterializeButton.cs`, `LabSpawner.cs`, `LabWindZone.cs` | T1, T2 |
| T5 registro y métricas | Sonnet | `Lab/LabMeasurements.cs`, `LabSessionLog.cs`, `LabCsvWriter.cs` | T1, eventos de T4 (firmas fijadas en §2) |
| T6 generador, harness, docs | agente principal (Unity MCP) | `MapLab.cs`, `MapBuilder.cs`, `MapStructures.cs`, `FlammableSetup.cs`, `HarnessPlaySmoke.cs`, docs | todo; compila, corre `Build()`, tests y smoke |
| T7 fase 3 | Sonnet | `Lab/LabFountain.cs`, `SpellCaster.cs` | T4 |

T1 y T2 en paralelo; T3, T4 y T5 en paralelo con las firmas de §2 congeladas; T6 compila y verifica una vez por tanda (los subagentes no compilan).

## 7. Preguntas abiertas

1. Lentes: ¿alcanza con E/grip en el soporte para ponerlas y sacarlas, o se quiere también un atajo (p. ej. reutilizar F3 para mostrar el overlay)?
2. ¿Los valores de la mesa sobreviven al Retroceso (propuesta: sí; solo el botón «por defecto» los restaura)?
3. CSV: ¿un archivo por sesión (`registro-sesion-AAAAMMDD-HHMM.csv`) o uno solo acumulado? ¿Separador `;` con punto decimal (propuesta) o lo que abra directo el Excel de la mesa de examen?
4. «Semilla» del fuego: ¿radio de encendido (propuesta) o cantidad de focos?
5. WebGL: ¿hace falta el registro en el build web para el examen, o basta PC/Quest?
6. ¿Dónde exacto va la mesa (propuesta: 5,5 m al este de la mesa de estampas, verificado con captura) y dónde el parche de fuego y la zona de viento?

## 8. Decisiones de la implementación (2026-11-15)

- **CSV con coma decimal** y `;` como separador (responde la pregunta 3): Excel en español lo abre directo. Un archivo por sesión, `registro-sesion-AAAAMMDD-HHMM.csv` en `persistentDataPath` (ADR-038).
- **Fuego sin «borrar el más viejo»**: el pasto quemado no se puede borrar; un parche nuevo espera a que el anterior se apague (aviso en la HUD). El tope de 1 parche activo se mantiene.
- **«Por defecto» no usa `ApplyDefaults`**: vuelve las variables al catálogo y pasa decay y generaciones con `SetTuning`, conservando el `spreadChance` que tenga la grilla.
- **Ubicación** (pregunta 6): mesa en mesa de estampas + 5,5 m al este, mundo ~(35.5, −16.5), girada para que quien la usa mire al este; caja y viento delante (~(40.5, −15.6) y ~(40.5, −14.5)), fuego a ~10 m sobre pasto (~(45, −20)); verificado contra la geometría del mapa (caminos, lago, estructuras y pinos de la escena generada), falta verlo en una captura.
- Lentes: solo E/grip en el soporte (pregunta 1); los valores sobreviven al reinicio (pregunta 2); semilla = radio de encendido (pregunta 4); WebGL guarda el registro solo en memoria (pregunta 5, KI-127).
