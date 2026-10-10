---
tags: [doc, plan, multijugador]
---
# Plan de multijugador (bloque 7 de la octava tanda)

> Planificación y auditoría, **sin código**. Escrito el 2026-10-09 sobre `main` (`e7fa77c`).
> Contexto: [[PLAN_OCTAVA_TANDA]] §7, [[ARCHITECTURE]], [[SYSTEMS_DESIGN]] §8, [[GAME_DESIGN]] (Duelo), [[ROADMAP]] (D1–D3), [[DECISIONS]].

**Decisiones tomadas por Ale (no se discuten acá):**
- Netcode for GameObjects (NGO) + Relay.
- Servidor autoritativo para hechizos y física.
- Los clientes predicen el movimiento y los trazos.
- Primero **cooperativo sandbox** (modo Libre compartido) para validar la red; después PvP/Duelo (D1–D3).

## 0. Resumen ejecutivo

- Con Relay el «servidor» es un **host** (un jugador que además simula el mundo). Con dedicado headless, el «servidor
  autoritativo» se cumple igual; el plan sirve para los dos, pero arranca con host.
- **Lo que viaja por red son trazos y eventos, nunca texturas ni partículas.** Cada cliente repinta los lienzos desde los
  trazos (`DrawingCanvas.Redraw`) y arma los VFX de un hechizo desde un resumen compacto del `SpellIR`.
- **Reconocimiento híbrido.** El cliente que dibuja reconoce (rápido, es su GPU) y propone la lectura. El servidor ya
  tiene los trazos (los recibió para replicarlos) y:
  - en coop, valida sólo invariantes baratas (tinta, geometría del anillo, ritmo de trazos);
  - en PvP, re-lee con el modelo durante el retardo de lanzamiento (0,5–2 s), que tapa la latencia.
- **Física:** el servidor simula y replica con `NetworkTransform` + `NetworkRigidbody` (no son dueños del cuerpo: los
  clientes lo ven cinemático). El jugador mueve su propio cuerpo (dueño) y el servidor le manda los empujones de los hechizos.
  El objeto agarrado se pide al servidor y se predice localmente (ver §2.6).
- **La economía ya tiene la forma correcta.** `SupplyPoint`, `WorldObjectBudget`, `RecyclingStation`, `Rune` y
  `InkCartridgeStock` son reglas acotadas y en parte puras. Falta:
  - `ownerId` real en todo lo que se reparte;
  - que `BudgetMath` cuente por jugador (KI-113);
  - que las protecciones dejen de mirar `Camera.main`.
- **Lo más caro de adaptar** no es la red en sí. Es que varias reglas de juego consultan **al único jugador local**:
  - `Camera.main` como «el que mira»;
  - `FindFirstObjectByType<ObjectMover|PlayerInteractor|PlayerMovement>`;
  - «agarrado = hijo de la cámara»;
  - `Portal.travelerReadyAt`.

  F0 las limpia sin red y con tests.
- Estimación total: **~70–95 días de trabajo** (F0–F6). El coop jugable (F0–F4) lleva ~45–60.

## 1. Auditoría del código para red

Recorrido de `Assets/_Project/Scripts` (180 archivos, un solo asmdef `SpellSimulator.Runtime`). Netcode **no está
instalado**: `Packages/manifest.json` sólo trae `com.unity.multiplayer.center` (asistente, sin runtime).

### 1.1 Cómo leer los estáticos

En NGO hay **un jugador local por proceso**. Por eso un estático de «estado del jugador local» no rompe la red por sí
mismo; sólo hay que asegurarse de que la lógica de servidor **no lo consulte** para jugadores remotos.

| Patrón | Ejemplos | Qué hacer |
|---|---|---|
| **Inocuo: estado local del proceso** | `PlatformDetector.IsVR`, `FocusMode`, `SurfaceFocus` (instancia privada), `PlayerSettings`, `GrimoireOptions`, `GraphicsQuality`, `Hud` (`HudModel`), `BookReaderUI.Instance`, `MainMenu.Instance`, `AudioDirector.Instance`, `DiagnosticBus` | Ya sirve. Sólo vigilar que nadie del servidor los use. |
| **Rompe: el mundo supone un solo jugador** | `Portal.travelerReadyAt` (el propio código lo anota), `WorldObjectBudget` (protege por `Camera.main` y busca el único `ObjectMover`), `Crystallized.IsHeld` (agarrado = hijo de `Camera.main`), `ExpiringBody.ReleaseIfHeld` (`FindFirstObjectByType<ObjectMover>`), `MagicWorld` (busca un `ObjectMover`), `CrystalSpell` (busca un `PlayerInteractor` y `Camera.main`), `Movable` (busca un `PlayerMovement`), `Respawnable.RespawnAll` (protege lo del único `PlayerInteractor` y la cámara) | Hay que adaptar: pasar a «cualquier jugador» o a «este jugador» (`ownerId`). |
| **Rompe: la jugabilidad depende de la cámara local** | `SpellOutputController.SpawnSpellEffect` usa `Camera.main` como `viewer` para `DrawingCanvas.EmissionNormal(viewer)` (la **dirección del hechizo**) | En el servidor el «viewer» tiene que ser **el lanzador**: se guarda con `NoteInkViewer` y se replica. |
| **Registros estáticos de simulación** | `MagicField.Active`, `WindColumn.Active`, `ElementReactive.All`, `Health.All`, `WaterVolume.All`, `WaterContainer.All`, `TrainingDummy.All`, `Portal.All`, `RuneJoin.all`, `Crystallized.active`, `IceSheet.all`, `FloodZone.zones`, `RockSpell.rocks`, `Inscribable.decals`, `GrassFire.Current`, estáticos de `WaterPuddleManager`, `Respawnable.resettables` | Sirven: son por proceso. Hay que asegurarse de que **sólo el servidor** los recorra para simular. En el cliente quedan para la presentación o vacíos. |
| **Eventos estáticos** | `SpellCaster` (eventos de **instancia**: `SpellCompiled`, `SealAnalyzed`, `SealPhaseChanged`, `SealCharging`…), `Portal.Traveled/StateChanged/RouteOpened`, `SupplyPoint.Supplied`, `Rune.WearChanged`, `SealBreaker.SealBroken`, `Stamp.Stamped/Loaded`, `ElementReactive.AnyReacted`, `Health.AnyDied`, `MagicWorld.WorldReset`, `GroundMarks.Marked`, `WaterVolume.Splashed`, `PhysicalTool.UseChanged/Refilled/PickedUp`, `Discovery.Discovered`, `LibraryBook.BookRead`, `Grimoire.*` | Muy buena base: son **el punto de enganche de la capa de red**. El servidor escucha y emite RPC; el cliente re-emite el mismo evento local para audio y VFX. Hay que **agregar quién** (`ownerId`) a los que hoy no lo traen. |

### 1.2 Tabla por módulo

Esfuerzo: S ≤ 1 día · M 2–4 días · L ≥ 5 días.

#### Player — rehacer en parte · L

Problemas:
- `PlayerMovement` (Rigidbody + `AddForce`) usa `Camera.main` para la dirección.
- `PlayerInteractor` (712 líneas) mezcla entrada, inventario de manos/barra y reglas de agarre. Además emparenta lo agarrado a `MainHold`.
- `ObjectMover` (resorte delante de `Camera.main`).
- `FocusMode` y `SurfaceFocus` son estáticos (locales; sirven).
- `InkSupply` / `InkCartridgeStock` (puro y serializable).

Qué hacer:
- Separar `PlayerInventory` (estado, servidor) de `PlayerInteractor` (entrada y presentación).
- El agarre pasa a ser un **pedido**.
- `ObjectMover` manda un objetivo y no mueve el cuerpo directamente.
- `InkCartridgeStock` ya sirve como estado del servidor.

#### Canvas — adaptar · M

- `DrawingCanvas` es dueño de `Session` + `RenderTexture` y `Redraw()` repinta desde los trazos: **ideal**.
- `NoteInkViewer` guarda quién mira; debe guardar al **lanzador**.
- `Stroke` no tiene **id**. `EraserTool` borra trazos enteros (bien para red), pero hay que identificarlos.
- Ids de trazo `(ownerId, seq)`.
- Separar `DrawingCanvasState` (sesión y cara con tinta, servidor) de la RT (presentación).

#### Casting — adaptar · M

- `SpellCaster` es instancia (bien) y es el único punto de lanzamiento (bien).
- Usa `Time.time` en 6 lugares (fases, `SealCharge`, `activatedAt`).
- `ReadSeal`, `Trigger`, `Emit` y `Compile` están bien partidos.
- `SealReading` (`Ast` + `Spell` + `Predictions`) es la «propuesta» natural.
- `SealRecord` graba fixtures (útil para la validación).
- `SpellCaster` corre en el servidor como autoridad; el cliente lo usa en **modo predicción**: lee, muestra la carga y no emite física.
- Inyectar el reloj (`ServerTime`).
- `CanvasJoinSystem`, `NestedSealReader` y `SpellFusion` corren en el servidor.

#### Compiler — ya sirve · S

- Puro: `SpellCompiler.Compile(ast, time)` recibe el tiempo.
- `SpellIR` es un struct grande (strings, arrays): **no se manda entero**. Se manda un `SpellCastMsg` mínimo (§2.4) y cada cliente re-deriva la presentación.

#### Parsing — ya sirve · S

- Puro y determinista: `SealParser`, `RingDetector`, `StrokeGrouper`.
- El servidor lo puede correr siempre (es barato) para validar la geometría.

#### Recognition — adaptar · M

- `GlyphClassifier`: `Worker.Schedule` y después `DownloadToArray` **síncrono**, que bloquea el hilo.
- `SentisGlyphClassifier` usa `GPUCompute`, o `CPU` en WebGL.
- Para validar en el servidor sin tirones: lectura asíncrona (`ReadbackAndCloneAsync`, a verificar en Inference Engine 2.6) o repartir los lotes en varios cuadros.
- En headless sólo CPU.

#### Output — adaptar · M

- Presentación casi pura: `SpellManifestation`, `ElementVfx`, `SealGlowDirector`, `SealFeedback`, `SealAnnotations`.
- Excepciones:
  - `SpellOutputController` usa la cámara para la dirección (ver 1.1);
  - `MagicField` aplica fuerzas: **sólo en el servidor**;
  - `Output/Bodies/*` (`RockSpell`, `CrystalSpell`, `WaterSpell`, `WindSpell`) **crean objetos de juego**: rocas, esferas de cristal, columnas de viento. `RockForming` y `RockSpell` usan `Random`.
- Dividir cada hechizo en «efecto de servidor» (cuerpos, campos) y «efecto visual» (partículas).
- Las rocas pasan a ser `NetworkObject` creados desde un prefab, con semilla replicada.

#### Reactions — adaptar · L

- `MagicWorld` (tick de 15 Hz) y `ReactionModel` (puro): servidor.
- `GrassFire` es una grilla: se replica como celdas que cambian (bits).
- `WaterPuddleManager`, `WaterDropSink` (**colisiones de partículas que crean charcos**) y `FloodZone`: el resultado de juego sale de partículas, que no son deterministas. Se rehace en el servidor con rayos o muestreo, y los charcos se replican como eventos.
- `IceSheet` y `Shatter` crean y rompen objetos. `GroundMarks` es decorativo (`Random`, local).
- `WorldResetter` y `MagicWorld.WorldReset`: sólo el host, o por votación.

#### World — adaptar o rehacer · L

- **Objetos agarrables**: `PhysicalCanvas` (398 líneas; emparenta a la mano y **a la cámara** al fijar), `PhysicalTool`, `Inkwell` (emparenta), `Movable`, `PinnedBody`, `ExpiringBody`, `Crystallized`, `Rune`, `SealTrigger`, `RuneJoin`, `Stamp`.
- **Economía**: `SupplyPoint` (ya por `ownerId`), `WorldObjectBudget` (estático; ver 1.1), `RecyclingStation`, `Respawnable`.
- **Portales**: `Portal`, `PortalGate`, `PortalPlate`, `PortalVisuals`.
- **Lienzos que se unen**: `CanvasJoinSystem` **crea un lienzo nuevo en tiempo de ejecución** (`CreateJoinedCanvas`).

Qué hacer:
- `NetworkObject` por agarrable.
- Lo que emparenta pasa a «seguir un ancla» (NGO sólo emparenta bajo otro `NetworkObject`, y nunca a una cámara).
- `SupplyPoint` y `RecyclingStation` clonan **plantillas desactivadas de la escena**, que NGO no puede crear: hacen falta prefabs registrados en `NetworkPrefabsList`, o un `INetworkPrefabInstanceHandler`.

#### Combat — adaptar · S

- `Health` (estático `All`, `AnyDied`), `DamageModel` (puro), `DamageTable` (SO), `ImpactDamage`.
- `DamageNumbers` es presentación (`Camera.main`, `Random`).
- `Health.current` pasa a `NetworkVariable`. El daño se calcula sólo en el servidor.

#### Learning — ya sirve (local) · M

- `Discovery` guarda `descubrimientos.json` **local**, `DiscoveryState` es puro y `Grimoire.Instance` (1168 líneas) también es local.
- `LibraryBook` y `Stamp` usan `Grimoire.Instance` (`CurrentGlyph`).
- Por jugador y local, no se replica.
- El grimorio en el mundo (ADR-026) es **un objeto por jugador**: los demás ven un libro simple.
- Al cargar una estampa se manda al servidor sólo el id del glifo.

#### UI — ya sirve · S/M

- Presentación pura: `HudController`, `BookReaderUI`, `MainMenu`, `ItemIcons`.
- `HudController` busca `PlayerInteractor`/`Belt` con `Find`: tiene que mirar **al jugador local** (`NetworkManager.LocalClient.PlayerObject`).
- `MainMenu` suma Hospedar/Unirse.

#### Core — ya sirve · S

- Puro: `Stroke`, `DrawingSession`, `StrokeCleaner`, `InkReservoir`, `InkMath`, `InkKind`, `InkCartridgeStock`.
- Agregar `StrokeId` y un codificador compacto (`StrokeCodec`, puro y con tests).

#### Audio — ya sirve · S

- Escucha eventos: basta con que el cliente re-emita los eventos locales.
- `AudioListener` con `FindAnyObjectByType`: uno sólo, el del jugador local.

#### Rendering / Debug — ya sirve · —

- Presentación.
- `OutlineHighlight` usa `FindObjectsByType` al iniciar (local).

### 1.3 `Time.time` y `Random`

`Time.time` aparece en 31 archivos. Para red hay que separar dos usos:

- **Tiempo de juego** (pasa a `NetworkManager.ServerTime`, inyectado): `SpellCaster` (6 usos: carga, fases), `Portal` (6),
  `SealTrigger` (6, enfriamientos de runa), `PinnedBody` (3, 60 s en el aire), `RuneJoin` (3), `CanvasJoinSystem` (3, los
  0,4 s), `RecyclingStation` (2), `FloodZone` (7), `IceSheet` (5), `WaterPuddleManager` (6), `Health` (2), `WorldObjectBudget`
  (`birthTime`) y `SpellIR.activatedAt`.
- **Tiempo de presentación** (queda local): `SealFeedback`, `SealGlowDirector`, `SpellManifestation`, `PortalVisuals`,
  `DamageNumbers`, Audio.

`Random` aparece en 11 archivos.
- **De juego:** `RockForming` y `RockSpell` (forma y variante de la roca), `PinnedBody` (vaivén), `PhysicalCanvas` (2).
  Se resuelven con **semilla elegida por el servidor** y replicada.
- **Decorativo:** `GroundMarks`, `WaterEffects`, `LowPolyMeshes`, `AudioDirector`, `DamageNumbers`, `MainMenu`.

### 1.4 Contradicciones con los supuestos y con la documentación

- **«`ownerId` en todo» todavía no es cierto.** Sólo existe en `SupplyPoint` y en `WorldObjectBudget.Register`, y
  `BudgetMath.Entry` **no** lo tiene: el tope no cuenta por jugador (KI-113). `RecyclingStation`, `Rune`, `RockSpell`,
  `Stamp`, `Stroke` y los lienzos no tienen dueño.
- **El `IsHeld` de `WorldObjectBudget` y de `Crystallized`** supone que agarrar = emparentar a la cámara o al único `ObjectMover`.
  En red, «agarrado por» tiene que ser un dato (`heldBy`), no una jerarquía.
- **La dirección del hechizo depende de la cámara local** (`SpellOutputController` → `EmissionNormal(Camera.main)`). No lo
  dice [[ARCHITECTURE]].
- **[[ARCHITECTURE]] nombra `SplitSealHalf`, que ya no existe** (lo reemplazaron `CanvasJoinSystem` y `CanvasJoinMath`). Es
  deuda de la documentación.
- **[[ROADMAP]] y [[SYSTEMS_DESIGN]] nombran `AICaster`, `SealTemplate`, `GameMode` y `DamageReceiver`**, que aún no existen
  (son D1/D2 por hacer). El rol de `DamageReceiver` hoy lo cumple `Health`.
- **El charco de agua nace de colisiones de partículas** (`WaterDropSink`): en un servidor autoritativo hay que reemplazarlo.

## 2. Modelo de autoridad y replicación

### 2.1 Quién manda en qué

| Servidor (autoridad) | Cliente (local o predicho) |
|---|---|
| Física de props, rocas, cristal, hielo, inundación; `MagicField` y `MagicWorld` | Cámara, `PlayerLook`, `FocusMode`, `SurfaceFocus` (vista completa del lienzo), HUD |
| Sesiones de trazos de cada lienzo (orden, tinta, ancho) | Trazo en curso (predicción: se pinta ya) |
| Fases del sello, carga, lanzamiento, `SpellCaster.Emit`; lectura final en PvP | Lectura propuesta (`SealReading`), anotaciones (`SealAnnotations`), brillo de carga predicho |
| Tinta: `InkReservoir` de cada herramienta, `InkCartridgeStock` de cada jugador, tinteros | Grimoire y `Discovery` (por jugador, persistentes en su disco) |
| Portales (estado, rutas, viajeros con enfriamiento por jugador) | VFX, partículas, audio, marcas del suelo, números de daño |
| `SupplyPoint`, `RecyclingStation`, `WorldObjectBudget`, desgaste de `Rune` | Movimiento propio (dueño del cuerpo; ver 2.7) |
| `Health`, daño, rondas y tiempo del Duelo | Opciones (`PlayerSettings`, `GrimoireOptions`, `GraphicsQuality`) |

### 2.2 Objetos de red

| Objeto | Cómo se replica |
|---|---|
| Jugador | `NetworkObject` (prefab del jugador): rig PC/VR local + **avatar** remoto (cabeza, 2 manos, cuerpo). |
| Cada agarrable | `NetworkObject` + `NetworkTransform` + `NetworkRigidbody`: `PhysicalCanvas`, `PhysicalTool`, `Inkwell`, `Movable`, `Rune`, `Stamp`, `LibraryBook`, `WaterContainer`, cajas. |
| Lienzo (`DrawingCanvas`) | Un `NetworkBehaviour` `CanvasNet` en el mismo objeto: trazos por RPC + instantánea para quien entra tarde. |
| Fijos | Lienzos de pared, placas de portal, tinteros fijos: `NetworkObject` de escena (in-scene placed). |
| Rocas, esferas de cristal, lienzo unido de `CanvasJoinSystem` | Prefabs registrados, creados por el servidor. |
| Charcos, hielo, celdas de fuego | **No** son `NetworkObject`: van como eventos o deltas en un `WorldStateNet` único (bitset de `GrassFire`, lista de charcos con id). |

Estado chico como `NetworkVariable`:
- `Rune.uses` (byte);
- `Portal.State` + id de destino;
- `PinnedBody` (bool + tiempo de caída);
- `heldBy` (ulong).

### 2.3 Trazos de tinta

**Por qué trazos y no texturas.** Un lienzo de 1024² RGBA son 4 MB; un sello completo son ~5 KB de puntos. Además el
servidor **necesita los puntos** para leer y validar: con una textura no podría hacerlo. Lo dice ADR-034 para guardar
hechizos y vale igual para la red.

**Formato (`StrokeCodec`, puro, en `Core`).**
- Cabecera: `canvasId` (NetworkObjectId, 8 B) · `strokeId = (ownerId:8 bits, seq:16 bits)` · `ink` (2 bits, `InkKind`) ·
  `width` (cuantizado a 8 bits sobre el rango del lienzo) · `flags` (fin de trazo, cara con tinta).
- Puntos en UV cuantizados a 12 bits por eje: un error de ~0,25 mm en un lienzo de 1 m, muy por debajo del ruido del pulso.
  El primero va absoluto (3 B) y los siguientes en **delta** con signo de 8 bits por eje (2 B). Si un salto no entra, se
  manda otro punto absoluto.
- **Simplificar antes de mandar:** el cliente ya pasa los puntos por el estabilizador exponencial de `PenTool`. Se mandan
  los puntos crudos con un filtro de distancia mínima (≈ 1/256 del lienzo); `StrokeCleaner` (Douglas-Peucker + Catmull-Rom)
  corre del lado del lector, igual que hoy.

**Flujo.**
1. El cliente pinta en su RT al instante (predicción).
2. Cada 50 ms manda `AppendStrokeRpc` (no confiable y secuenciado mientras dibuja) y, al soltar, `EndStrokeRpc`
   (**confiable**, con el trazo completo comprimido, para cerrar huecos).
3. El servidor valida:
   - el lienzo está en la mano, o a ≤ 2 m del jugador;
   - descuenta tinta (`InkMath`);
   - velocidad máxima de la pluma.

   Agrega el trazo a la sesión y lo reenvía al resto.
4. Si lo rechaza (sin tinta, lienzo ajeno), manda `StrokeRejectedRpc`: el cliente quita el trazo y llama a `Redraw()`.
5. Goma, trapo y deshacer son operaciones con id: `EraseStrokesRpc(ids[])`, `ClearCanvasRpc`, `UndoRpc`. El orden lo fija el
   servidor: cada lienzo tiene un `revision` que sube con cada cambio, y el cliente que se adelantó re-sincroniza.
6. Quien entra tarde recibe una instantánea por lienzo con un mensaje con nombre (`CustomMessagingManager`), no un
   `NetworkList` (es caro y tiene límites de tamaño).

**Vista del lienzo ajeno.** El trazo en curso de otro jugador llega cada 50 ms con interpolación: se ve dibujarse con
~100 ms de atraso, y alcanza. La «vista completa» (Tab) es local y funciona sobre cualquier lienzo replicado.

### 2.4 Reconocimiento: ¿lo hace el cliente o el servidor?

**Costo medido o estimado.**
- Objetivo de [[SYSTEMS_DESIGN]]: menos de 30 ms por lanzamiento con TTA×8.
- `SealReader` prueba hasta 3–4 agrupaciones, cada una un lote por el modelo. En el peor caso son **~4 inferencias de
  ~10–25 ms en GPU**, y unas 3–5× más en CPU. Hay que medirlo con el profiler: **a verificar**.
- Si el host lee todo con 4 jugadores (≈ 1 lanzamiento cada 3 s por jugador, ≈ 1,3/s) gasta **40–130 ms/s** de hilo
  principal en GPU. Peor que el promedio: **tirones de 30–100 ms en el cuadro del host**, porque `DownloadToArray` es síncrono.
  En una Quest de host sería inaceptable.

**Esquema propuesto: «el cliente propone, el servidor dispone».**
1. El cliente que cierra el anillo corre `SpellCaster.Compile` localmente (predice brillo, anotaciones y carga) y manda
   `ProposeCastRpc { canvasId, revision, sigilId, signIds[] + ángulos cuantizados, element, inkKind, confianza }` (~40–80 B).
2. El servidor **siempre** corre lo barato y puro sobre sus trazos (los tiene por 2.3):
   - `SealParser` / `RingDetector`: anillo cerrado, capas, cantidad de candidatos igual a lo propuesto;
   - tinta gastada coherente con el largo;
   - enfriamientos.

   Cuesta < 1 ms.
3. Re-lectura con el modelo:
   - **En coop:** el servidor acepta la propuesta tras las invariantes y compila el `SpellIR` con `SpellCompiler`, que es
     puro, desde un `GlyphAST` armado con las predicciones del cliente.
   - **En PvP, o si el hechizo es costoso** (tinta poderosa, anidado, `strength` alto), el servidor re-lee con el modelo
     **durante la carga** (0,5–2 s, `SealCharge`), repartido en varios cuadros. Si su lectura difiere, gana la del servidor
     y el cliente corrige la anotación.
   - **Muestreo aleatorio** en coop (1 de cada N) para detectar clientes que mienten sin pagar el costo completo.
4. Clientes WebGL (CPU lenta) o Quest flojas: pueden pedir `ServerReadRpc` y que el servidor lea por ellos.

**Por qué no sólo el servidor:** el costo cae en el host, que además juega, y la respuesta (brillo y anotaciones) llegaría
tarde. **Por qué no sólo el cliente:** en PvP un cliente modificado podría declarar «fuego de potencia máxima» con un garabato.

### 2.5 Evento de lanzamiento y `SpellIR`

El servidor manda `SpellCastClientRpc`, unos 48–64 B:
- `castId`, `canvasId`, `revision`, `serverTime`;
- `element`, `sigilId` (índice de diccionario, byte), `signs[]` (índice + ángulo de 8 bits);
- `sealRadius` (half), `ringCenterUV` (2×u16), `inkKind`, `strength`, `effectScale` (half);
- **pose del lienzo al lanzar** (posición + rotación comprimida) y **normal de emisión** (ya resuelta por el servidor con el lanzador como viewer);
- semilla.

Cada cliente re-deriva la presentación localmente: `SpellManifestation`, `ElementVfx`, `SealGlowDirector`, audio. Para no
re-correr el compilador, el servidor puede mandar también `direction` y `plan` cuantizados, sumando ~24 B.

Los cuerpos que crea el hechizo (roca, esfera de cristal) son `NetworkObject` del servidor. Las partículas de agua y viento
son locales y las fuerzas sólo las aplica `MagicField` en el servidor.

### 2.6 Física de objetos agarrados

- **Agarre = pedido.** `GrabRequestRpc(objId, mano)`. El servidor valida distancia (≤ alcance + margen), que esté libre y
  que no esté `Crystallized`. Fija `heldBy` y avisa.
- **Mover (`ObjectMover`).** El cliente manda a 30 Hz la pose objetivo (cámara/mano + distancia + rotación del objeto)
  con `HoldTargetRpc` no confiable. El servidor corre el resorte actual de `ObjectMover`, que pasa a ser un componente de
  servidor por objeto agarrado.
  - Para que no se sienta el lag, el cliente dueño dibuja el objeto con un **fantasma predicho** que sigue su objetivo
    local y se reconcilia con suavidad con la posición del servidor.
  - Alternativa más simple: **pasarle la propiedad del `NetworkObject` al que agarra mientras lo tiene**
    (`ChangeOwnership` + `NetworkTransform` autoridad del dueño). Responde perfecto, pero rompe «física autoritativa»
    mientras se sostiene. Ver la pregunta 2.
- **En la mano** (lienzo, herramienta, tintero): hoy `SetParent(MainHold)` o a la cámara (`PhysicalCanvas`, línea 368).
  En red, el objeto queda cinemático y **sigue un ancla del avatar** (`FollowAnchor` por id de jugador + mano), sin
  emparentar. Para el dueño, el ancla es su rig local.
- **`PinnedBody`:** «dejar en el aire» es un pedido. El servidor guarda la hora de caída (60 s) y el vaivén se calcula en
  el cliente con una semilla (no se replica).
- **`ExpiringBody`** (rocas a los 2 min, cosas en el aire): temporizador en el servidor. El encogerse es visual,
  disparado por un `NetworkVariable<bool> expiring`.
- **Rocas:** prefab de red, `RockForming` en el servidor con semilla y `NetworkRigidbody` en los clientes. Tope de
  `RockMath.MaxRocks` global y por jugador.
- **Arrojar (Q):** pedido con velocidad. El servidor aplica el impulso y limita la velocidad máxima.

### 2.7 Jugadores (`PlayerMovement` sobre Rigidbody)

NGO **no trae** predicción y reconciliación para Rigidbody (Netcode for Entities sí). Hay dos caminos:

**A. Dueño autoritativo del propio cuerpo (recomendado para F1–F5).**
- `NetworkTransform` en modo dueño (`AuthorityMode.Owner`).
- El jugador simula su `Rigidbody` como hoy, sin lag.
- Los empujones de `MagicField` (viento, agua, explosión) los calcula el servidor y le llegan al dueño como
  `ApplyImpulseClientRpc` (acumulado por tick).
- El servidor valida velocidad y teletransportes (portales) y corrige si se pasan.
- Costo: S/M. Es «el cliente predice el movimiento» en su forma más barata.

**B. Servidor autoritativo con predicción y reconciliación propias.**
- Entradas con número de tick y re-simulación.
- Con Rigidbody en PhysX hace falta `Physics.Simulate` manual por escena: L/XL.
- Recién justificado si el PvP lo pide (quejas de velocidad o teletransporte).

### 2.8 Inventario, barra, cinturón y cartuchos

- **Estado del servidor por jugador** en `PlayerInventoryNet`:
  - 3 slots (5 con `Belt`) de ids de `NetworkObject`;
  - mano secundaria;
  - `InkCartridgeStock` serializado (≤ 3 clases × 4 cartuchos × usos: < 16 B);
  - tinta activa.
- `HotbarMath` y `InkCartridgeStock` ya son puros: se reusan tal cual en el servidor y en el cliente (predicción de R/V).
- El HUD (`HudModel`/`Hud`) lee la copia local.

### 2.9 Grimorio y descubrimiento

- **Por jugador y persistentes en su disco** (`descubrimientos.json`, `hechizos.json` de ADR-034). No se replican.
- Al entrar a una sesión, el cliente puede anunciar su `DiscoveryState` resumido, sólo para mostrar el libro de los demás.
- Los libros (`LibraryBook`) se leen en local: dos jugadores pueden leer el mismo libro a la vez.
- Las identidades de portal aprendidas también son locales: sabe el que leyó.

## 3. Anti-agotamiento y economía en red

| Regla | Hoy | En red |
|---|---|---|
| Reposición (`SupplyPoint`) | `maxLive` por `ownerId` (siempre 0) y enfriamiento global del punto | `ownerId` = `OwnerClientId` del que **toma** el objeto (no del que lo ve). Enfriamiento **por jugador y punto** (`SupplyMath` ya es puro: sólo falta indexarlo). |
| Reciclaje (`RecyclingStation`) | Economía cerrada: 1 agotada → 1 nueva; 2 útiles → 1; 1 papel → 1 limpio | En el servidor. El reemplazo pertenece a quien lo depositó, que es el último que lo agarró (`lastHeldBy`). |
| Tope global (`WorldObjectBudget`, 40 total / 12 por tipo) | Protege lo que está en la mano o a 15 m de la cámara | Proteger a ≤ 15 m de **cualquier** jugador o con `heldBy` puesto. Agregar `ownerId` a `BudgetMath.Entry` y `MaxPerOwner` (p. ej. 40/N + 4). Se saca primero lo más viejo **del que más tiene**. |
| Runas gastadas (`Rune`) | Desgaste de por vida; no se reinicia al borrar | `NetworkVariable<byte>` de usos en el servidor; las grietas son visuales. |
| Rocas (`RockSpell`) | `RockMath.MaxRocks` global | Global + por jugador. Se van primero las propias más viejas. |
| Tinta | Por herramienta y por jugador (cartuchos) | En el servidor. El cliente predice y el servidor corrige la reserva cada 0,5 s. |

**Para que nadie acapare:**
- límite de objetos vivos por jugador y por punto;
- un objeto «de otro» que lleva 60 s tirado lejos de su dueño vuelve a la bolsa común (`ownerId = 0`);
- el reciclaje es la única fuente de runas de oro;
- en Duelo no hay `SupplyPoint`: sólo el mazo fijo.

**Cómo se mide.** Un `EconomyStats` (sólo del servidor) cuenta por jugador y por minuto:
- objetos tomados, reciclados y desalojados;
- tiempo con cada punto vacío;
- tinta gastada.

Se vuelca por `DiagnosticBus` y con un chequeo del harness (`HarnessNetEconomy`): «dos jugadores vacían el mundo en
< 5 min» tiene que dar **falso**.

## 4. PvP / Duelo

- **Reglas** ([[GAME_DESIGN]] §Duelo):
  - vida 100, rondas de 3 min, al mejor de 3;
  - tinta 100 por herramienta con regeneración de 2/s y tinteros de arena que cargan en 1,5 s;
  - mazo de 3 sellos con 1 carga cada uno;
  - daño de 8 a 30 por impacto, 4/s de quemadura y hasta 25 de aplastamiento.

  Todo eso va a un SO `DuelRules`, ya previsto.
- **Encaje:**
  - `DuelGameMode` es un `NetworkBehaviour` del servidor (timer y rondas en `NetworkVariable`; ROADMAP lo llama `GameMode`);
  - `Health` en red;
  - el mazo es un `InkCartridgeStock`-análogo: `SealDeck` puro, con las estampas del grimorio como fuente;
  - la arena es una escena aparte, generada como el mapa.
- **IA (D2):**
  - `AICaster` (a crear) corre **en el servidor** como un jugador falso (`ownerId` reservado, p. ej. 255);
  - dibuja `SpellStrokeBuilder` / `StampPattern` con ruido sobre su lienzo y pasa por el mismo `SpellCaster`;
  - como está en el servidor, no hay nada que validar;
  - el mismo código sirve para el Duelo offline (host sin clientes).
- **Daño:** `MagicWorld` aplica la exposición en su tick de 15 Hz en el servidor; `ImpactDamage` corre en el servidor
  (las colisiones de cuerpos replicados).
- **Compensación de lag.** Los hechizos tienen **0,5–2 s de carga** y son flujos de área, no balas instantáneas. Por eso
  **no hace falta rebobinar** el mundo:
  - el que lanza ve su carga predicha al instante;
  - el servidor arranca la carga al recibirla (RTT/2 tarde) y descuenta ese atraso de la duración, con un tope de 150 ms;
  - el que recibe ve el anillo brillar ~1 s antes del golpe y puede esquivar o cubrirse; esa ventana ya tapa la latencia;
  - las rocas arrojadas son cuerpos del servidor (el que arroja las ve con ~RTT de atraso: aceptable).
- **Trampas (modelo de amenaza simple; atacante = cliente modificado, sin acceso al host):**

| Amenaza | Defensa |
|---|---|
| Declarar un hechizo que no dibujó | Re-lectura en el servidor en PvP (2.4) |
| Tinta infinita | La tinta es del servidor |
| Saltear la carga o el enfriamiento | `SealCharge` y `SealTrigger` en el servidor, con `ServerTime` |
| Dibujar «perfecto» con un bot | Límite de velocidad de pluma y de puntos por segundo; queda como riesgo aceptado (es como un aimbot) |
| Agarrar a distancia o robar en la mano ajena | Pedido validado (2.6) |
| Velocidad o teletransporte | Validación del dueño (2.7 A) |
| Llenar el mundo con objetos (spam) | Límites por jugador (§3) |
| Inundar de RPC | Límite de tasa por cliente; desconectar si se pasa |
| Host tramposo | **Fuera de alcance** con Relay (el host manda). Sólo lo resuelve un servidor dedicado. |

## 5. Presupuesto

### 5.1 Jugadores y ticks

| Modo | Jugadores | Tick de red (NGO) | Simulación |
|---|---|---|---|
| Coop sandbox | 2–4 (objetivo) | 30 Hz | `FixedUpdate` 50 Hz, `MagicWorld` 15 Hz |
| Sandbox ampliado | hasta 8 | 20–30 Hz | Igual; reactivar la relevancia (5.3) |
| Duelo | 2 (+ IA) | 30 Hz | Igual |

### 5.2 Ancho de banda (estimado, a verificar con el Network Profiler / Runtime Network Stats Monitor)

| Flujo | Cálculo | Por jugador emisor |
|---|---|---|
| Trazo dibujándose | ~60 puntos/s filtrados a ~30, ×2 B + cabecera de 20 B ×20 msg/s | **≈ 0,5–1 KB/s** (sólo mientras dibuja) |
| Trazo completo (confiable) | 100 puntos × 2 B + 20 | 0,2 KB por trazo |
| Instantánea de un lienzo | sello típico de 20–40 trazos | 3–8 KB por lienzo (una vez) |
| Movimiento propio + avatar (cabeza, 2 manos) | 3 transformaciones × ~14 B × 30 Hz | **≈ 1,3 KB/s** |
| Objeto agarrado (objetivo) | 20 B × 30 Hz | 0,6 KB/s |
| Hechizo | 64–90 B por lanzamiento | despreciable |
| Props en movimiento (servidor → cada cliente) | `NetworkTransform` con half floats y cuaternión comprimido ≈ 12–18 B. Peor caso: 30 cuerpos moviéndose × 30 Hz | **≈ 11–16 KB/s** en ráfagas; en reposo, ~0 (umbral) |
| Mundo reactivo | Bitset de celdas de `GrassFire` cambiadas + eventos de charcos/hielo/estado de `ElementReactive` | 0,2–1 KB/s |

- **Por cliente (bajada):** típico **5–20 KB/s**, con picos de ~30 KB/s en una explosión con muchas cajas.
- **Host (subida):** × (N−1). Con 4 jugadores, ≈ 60–90 KB/s ≈ **0,5–0,7 Mbps**: razonable en hogares.
- Relay agrega latencia (un salto) y tiene cuotas de uso del servicio: **a verificar** límites y precios de UGS para el
  tamaño de la asignatura.

### 5.3 `NetworkTransform` contra algo propio

- **F2 a F4:** `NetworkTransform` (`UseHalfFloatPrecision`, `UseQuaternionCompression`, umbrales de 1 mm / 0,5°,
  interpolación) + `NetworkRigidbody`. Con ~60–100 cuerpos alcanza y no hay que mantenerlo.
- **Algo propio** (`WorldBodiesSync`: un mensaje por tick con los cuerpos despiertos, cuantizados a 6 B de posición
  relativa a una celda + 4 B de cuaternión, y prioridad por distancia) **sólo si** el profiler muestra > 30 KB/s
  sostenidos o se apunta a 8 jugadores. Esfuerzo L.
- **Fragmentos (`Shatter`, `CrystalShards`):** locales, con semilla. No se replican uno por uno.

**Relevancia (interés).** El valle mide ~140 m y con 4 jugadores casi todo es relevante: **no hace falta en coop**. Para 8
jugadores, `NetworkObject.CheckObjectVisibility` + `NetworkShow/Hide` por distancia (≥ 60 m), con histéresis y revisando
cada 0,5 s. Los objetos agarrados y los cuerpos de hechizos siempre se ven.

### 5.4 Quest

- **Cliente sí; host sólo para 2 jugadores.**
- El host simula física, `MagicWorld` y lecturas de validación; en Quest compite con el render a 72–90 Hz. Hay que perfilar.
- Las lecturas del cliente en la Quest corren en su GPU (`GPUCompute`). Medir si tironea: **a verificar** (KI-095 y M7 sin
  perfilar todavía).

### 5.5 WebGL (a verificar)

- NGO corre en WebGL (un solo hilo).
- **Unity Transport 2.x** soporta **WebSockets** (`UnityTransport.UseWebSockets`) y Relay con tipo de conexión `"wss"`.
- **Un navegador no puede ser host** (no puede escuchar conexiones): WebGL sólo como cliente de un host de PC.
- **Todos tienen que usar el mismo transporte.** Si se aceptan clientes WebGL, el host y los demás también van por
  WebSocket/`wss`. Se puede, pero suma un poco de latencia.
- El SDK de sesiones (`com.unity.services.multiplayer`) tiene soporte para WebGL: **a verificar**, versión y API.
- KI-104 (tinta y lienzos invisibles en WebGL) quedó resuelto el 2026-10-09; ya no bloquea.
- Sentis en WebGL es sólo CPU: conviene la lectura delegada (2.4, punto 4).

## 6. Plan por fases

Cada fase cierra con:
- compilación limpia;
- tests EditMode verdes;
- `HarnessPlaySmoke`;
- un chequeo nuevo en el harness;
- un commit atómico.

Herramientas de prueba:
- **Multiplayer Play Mode** (`com.unity.multiplayer.playmode`): el editor + hasta 3 jugadores virtuales.
- **Tests de integración de NGO** (`NetcodeIntegrationTest`, varios `NetworkManager` en un proceso; disponibilidad pública
  **a verificar**).
- **Servidor headless** (`-batchmode -nographics`, Inference Engine en CPU) para pruebas largas.

### F0 — Preparación, sin red (6–9 días) — HECHO 2026-10-09 (resto en KI-118)

**Depende de:** nada. **Se puede hacer ya, sin instalar NGO.**

Qué se hace:
- `IPlayerContext` (posición de la cabeza, manos, `ownerId`, `IsLocal`) y un `PlayerRegistry` (lista de jugadores). En
  local tiene uno solo. Reemplaza `Camera.main` y `FindFirstObjectByType` en la **lógica de juego**:
  `WorldObjectBudget`, `Crystallized`, `ExpiringBody`, `MagicWorld`, `CrystalSpell`, `Movable`, `Respawnable`,
  `SpellOutputController` (viewer = lanzador), `Portal` (`travelerReadyAt` por jugador).
- `heldBy` como dato (`Holdable`), en lugar de mirar la jerarquía.
- `ownerId` en: `Stroke` (+ `StrokeId`), `BudgetMath.Entry` (+ `MaxPerOwner`), `RecyclingStation`, `Rune`, `RockSpell`, `Stamp`.
- `IGameClock` inyectado en `SpellCaster`, `SealTrigger`, `Portal`, `PinnedBody`, `RuneJoin`, `CanvasJoinSystem`.
- `StrokeCodec` puro.
- Partir `PlayerInteractor` en `PlayerInventory` (estado) + interactor (entrada).

**Acepta si:**
- el juego se comporta igual (los tests y la matriz de lienzos dan los mismos resultados);
- un grep de `Camera.main` en `World/`, `Reactions/` y `Casting/` da 0;
- hay tests nuevos: `StrokeCodecTests` (ida y vuelta, error < 1/4096), `BudgetMathTests` por dueño, `PortalMath` con 2 viajeros.

**A mano:** una pasada del modo Libre.

### F1 — Sesión y conexión (5–7 días)

**Depende de:** F0.

Qué se hace:
- Instalar NGO 2.x + Unity Transport + Multiplayer Services (versiones a verificar para 6000.3).
- `NetworkManager` en la escena; `NetSession` (crear y unirse con código de Relay).
- Menú Hospedar/Unirse en `MainMenu`.
- Prefab de jugador con rig local y avatar remoto (cápsula, cabeza, manos y nombre).
- `PlatformDetector` elige el rig sólo para el dueño.
- Movimiento A (2.7) con `NetworkTransform` dueño.

**Acepta si:**
- 2 instancias (Multiplayer Play Mode) se ven moverse;
- PC + Quest por Relay;
- desconectar y reconectar no rompe el juego;
- entrar tarde aparece en el spawn.

**Harness:** `HarnessNetSmoke` (host + 1 cliente virtual: conectan, spawnean y el cliente camina 2 m y el host lo ve).

**A mano:** PC↔PC por Internet, PC↔Quest.

### F2 — Mundo compartido: objetos físicos y agarre (8–12 días) — HECHO 2026-10-09

> Cambio respecto de lo planeado: **no se usa `NetworkObject` por cuerpo** (los objetos son de la escena, no prefabs). Se replica con mensajes con nombre y un registro de ids por orden de la jerarquía (`NetWorld`, `NetBody`; ver CHANGELOG). Agarre = el cliente empieza al instante y el servidor valida (decisión de Ale: en coop manda el que agarra). Pendiente de F2: clonados en juego (reposición/reciclaje/hechizos: F3-F4), empuje por caminar de un cliente, prueba a mano con 100-200 ms.

**Depende de:** F1.

Qué se hace:
- `NetworkObject` en los agarrables: `MapBuilder` los agrega al generar; respetar `MapLayout`.
- Prefabs de red para lo que se clona (`SupplyPoint`, `RecyclingStation`, rocas, lienzo unido).
- Agarre como pedido, `FollowAnchor`, `ObjectMover` de servidor con fantasma predicho.
- `PinnedBody` y `ExpiringBody` de red; arrojar.

**Acepta si:**
- dos jugadores se pasan un lienzo y una caja;
- agarrar lo que tiene otro se rechaza;
- 30 cajas empujadas se ven iguales en ambos (error < 5 cm tras asentarse);
- no se ve tirón al sostener con 150 ms de RTT simulado (el simulador de red de UTP permite agregar latencia y pérdida).

**Harness:** `HarnessNetGrab`.

**A mano:** sensación de agarre con 100–200 ms.

### F3 — Dibujo compartido y hechizos replicados (10–14 días) — HECHO 2026-10-10

> Hecho con mensajes propios (como F2): `NetCanvasSync` (trazos por comparacion de sesiones), `NetCast` + `SpellIRCodec` (el cliente propone el SpellIR compilado; el anfitrion valida y ejecuta; los demas lo muestran) y `NetSpawn` (la roca se replica). Pendiente: union de mitades y de runas (crean lienzos en juego), tinta validada por el servidor (PvP), brillo de carga de los hechizos ajenos, matriz de lienzos desde un cliente.

**Depende de:** F2.

Qué se hace:
- `CanvasNet`: trazos predichos, rechazos, goma, trapo, deshacer, revisión e instantánea.
- Tinta del servidor.
- `ProposeCastRpc` + validación barata.
- `SpellCastClientRpc` + VFX locales.
- `MagicField` sólo en el servidor; empujones al dueño (2.7 A).
- Estampas (`Stamp`) con id de glifo; runas armadas (`SealTrigger`) y su desgaste.
- `CanvasJoinSystem` y `RuneJoin` en el servidor.

**Acepta si:**
- la matriz de lienzos (`HarnessCanvasMatrix`), corrida **desde el cliente**, da los mismos elementos y direcciones que
  en local en ≥ 95 % de los casos;
- un hechizo de viento del cliente empuja una caja y al host;
- un cliente sin tinta no puede dibujar;
- entrar tarde muestra los sellos ya dibujados.

**Harness:** `HarnessNetCanvasMatrix`: reusa los sellos de `HarnessSeals` y los fixtures de `Tests/Fixtures`.

**A mano:** dibujar a la vez en el mismo lienzo; ver dibujar a otro en vista completa.

### F4 — Mundo reactivo, portales y suministros en red (10–15 días) — HECHA (2026-10-10, verificada con bots)

> Hecho: suministros/reciclaje solo en el anfitrion con clones replicados, portales (anfitrion decide, cruce validado con enfriamiento por jugador, verificado con bots), y escrito sin probar `NetReactiveSync` (pasto + objetos reactivos). Ver CHANGELOG.

**Depende de:** F3.

Qué se hace:
- `MagicWorld` sólo en el servidor + `WorldStateNet`: bitset de `GrassFire`, charcos (reemplazar `WaterDropSink` por
  muestreo del servidor), `IceSheet`, `FloodZone`, estados de `ElementReactive` (quemando, mojado, congelado), `Crystallized`.
- Portales: estado y destino en `NetworkVariable`, viaje validado por el servidor con teletransporte al dueño y enfriamiento por jugador.
- `SupplyPoint` / `RecyclingStation` / `WorldObjectBudget` por dueño + `EconomyStats`.
- `WorldResetter` sólo el host.

**Acepta si:**
- un fuego encendido por A lo apaga el agua de B y ambos lo ven igual;
- A abre un portal, B lo cruza y a B no le afecta el enfriamiento de A;
- 2 jugadores no agotan los suministros en 5 min (`HarnessNetEconomy`);
- reiniciar el mundo desde el host deja todo igual en los clientes.

**A mano:** 30 min de coop con 3–4 personas (sesión de juego para la asignatura).

### F5 — PvP/Duelo y balance (12–18 días) — HECHA EN DOS LOTES (2026-10-10: reglas, red, HUD, arena, tinta del servidor y re-lectura; falta IA, coberturas destructibles y balance a mano)

**Depende de:** F4 (y D1/D2 del ROADMAP, que se pueden hacer en paralelo en local desde F0).

Qué se hace:
- Arena (escena).
- `DuelGameMode` (rondas, vida, victoria).
- `DuelRules` (SO), `SealDeck`, `Health` en red, daño del servidor.
- Re-lectura del servidor en PvP.
- `AICaster` en el servidor.
- Compensación de carga (tope de 150 ms).

**Acepta si:**
- una partida al mejor de 3 contra la IA y otra entre 2 personas terminan sin desincronizarse;
- un cliente que declara otro elemento queda corregido por el servidor (test con propuesta falsa);
- el daño coincide con `DamageModel` (tests EditMode).

**Harness:** `HarnessNetDuel` (host + IA + cliente virtual, 1 ronda acelerada).

**A mano:** balance (tinta, cargas, daño) con 4–6 partidas.

### F6 — Pulido, anti-trampas, WebGL/Quest (10–15 días)

**Depende de:** F5.

Qué se hace:
- Límites de tasa de RPC, validación de velocidad, muestreo de lecturas en coop.
- Relevancia por distancia si se apunta a 8.
- Perfilado de ancho de banda y del host.
- WebGL con `wss` (si KI-104 está resuelto).
- Quest de cliente y de host para 2.
- Reconexión y abandono del host (fin de sesión limpio; migrar el host **no** entra).

**Acepta si:**
- ≤ 30 KB/s por cliente con 4 jugadores en la escena de prueba de estrés;
- el host en PC mantiene 60 fps con 4 jugadores y 10 lanzamientos por minuto;
- Quest de cliente a ≥ 72 fps;
- WebGL se une a un host de PC.

**A mano:** sesión pública de prueba.

## 7. Riesgos y preguntas abiertas (por impacto)

1. **Física de Rigidbody del jugador con hechizos que empujan** (2.7). Si A (dueño) no alcanza en PvP, B es caro (L/XL).
   Mitigación: medirlo en F3 con viento y agua contra jugadores.
2. **Costo del reconocimiento en el host** (2.4). El `DownloadToArray` síncrono provoca tirones. Mitigación: lectura
   asíncrona o repartida y validación selectiva. Medir en F3.
3. **Objetos clonados de plantillas de escena** (`SupplyPoint`, `RecyclingStation`, `CanvasJoinSystem`). NGO necesita
   prefabs: implica cambios en `MapBuilder` y en las escenas, siempre por MCP.
4. **`MapBuilder.Build()` regenera la escena**, y con ella los `GlobalObjectIdHash` de los `NetworkObject` de escena.
   Host y cliente deben tener **el mismo build**: hay que versionar la escena y negociar la versión al conectar.
5. **Agua por partículas** (`WaterDropSink` → charcos): reescribir en el servidor puede cambiar la sensación del agua.
6. **Tamaño de `PlayerInteractor` (712 líneas) y `Grimoire` (1168)**: partirlos es riesgoso sin pruebas a mano; nada de lo
   reciente se probó con teclado y mouse reales (ver `CLAUDE.md` y [[STATUS]]).
7. **WebGL** (5.5): transporte, SDK de sesiones y KI-104, todo a verificar.
8. **Cuotas y costo de Relay/UGS** para la entrega de la asignatura: a verificar.
9. **Host tramposo o que se va:** con Relay no hay solución. Se acepta para coop y para un duelo entre amigos.

## 8. Decisiones que necesito de Ale

1. **Movimiento del jugador:** ¿empezamos con el jugador dueño de su cuerpo y el servidor mandando los empujones de los
   hechizos (2.7 A), y dejamos predicción y reconciliación completas (B) para cuando el PvP lo pida? *Recomiendo: sí, A.*
2. **Objeto agarrado:** ¿el servidor simula el resorte y el cliente ve un fantasma predicho (física 100 % autoritativa), o
   le pasamos la propiedad al que agarra mientras lo tiene (responde mejor, menos código)?
   *Recomiendo: propiedad al que agarra en coop; resorte del servidor en Duelo.*
3. **Reconocimiento:** ¿«el cliente propone y el servidor valida barato siempre, y re-lee con el modelo sólo en PvP y en
   hechizos costosos» (2.4)? *Recomiendo: sí.*
4. **Topología:** ¿sólo host con Relay (un jugador hospeda) para coop y Duelo, sin servidor dedicado por ahora?
   *Recomiendo: sí; dedicado sólo si se publica un PvP abierto.*
5. **Jugadores objetivo:** ¿fijamos 4 en coop y 2 (+ IA) en Duelo, y dejamos 8 como «posible» sin relevancia por
   distancia hasta F6? *Recomiendo: sí.*
6. **WebGL en red:** ¿entra en el alcance (obliga a usar WebSocket/`wss` para todos) o queda fuera hasta resolver KI-104?
   *Recomiendo: fuera hasta F6.*
7. **¿Hacemos F0 ya, antes de instalar NGO** (limpieza de estáticos, `ownerId`, reloj inyectado, `StrokeCodec`), aunque
   no se vea nada nuevo en pantalla? *Recomiendo: sí, primero F0 y después la prueba a mano del modo Libre.*
8. **Orden Duelo contra red:** ¿hacemos D1/D2 (arena, reglas, IA) **en local** en paralelo a F1–F4, para que F5 sea sólo
   «poner en red» el Duelo? *Recomiendo: sí, D1/D2 en local después de F2.*

## Apéndice — Inventario de clases a tocar

Todas las rutas son relativas a `Assets/_Project/Scripts/`. Entre paréntesis, la fase.

**Player**
- `Player/PlayerInteractor.cs` (F0 partir; F2 pedidos de agarre)
- `Player/ObjectMover.cs` (F2: resorte de servidor + objetivo del cliente)
- `Player/PlayerMovement.cs` (F0 dirección sin `Camera.main`; F1 red; F3 impulsos)
- `Player/InkSupply.cs` (F3)
- `Player/HotbarMath.cs` (reusar)
- `Player/PlatformDetector.cs` (F1: sólo el dueño)
- `Player/PlayerInputReader.cs` (F1: sólo el dueño)
- `Player/PlayerLook.cs` (F1)

**Canvas, Core y herramientas**
- `Canvas/DrawingCanvas.cs` (F0 `StrokeId`, viewer = lanzador; F3 `CanvasNet`)
- `Core/DrawingData.cs` (F0 `StrokeId`, `ownerId`)
- `Core/InkReservoir.cs`, `Core/InkCartridgeStock.cs`, `Core/InkMath.cs` (F3 servidor)
- `Input/Tools/PenTool.cs`, `Input/Tools/EraserTool.cs`, `Input/Tools/RagTool.cs` (F3: operaciones con id)

**Casting y Recognition**
- `Casting/SpellCaster.cs` (F0 reloj; F3 propuesta y validación)
- `Casting/SealReader.cs`, `Casting/NestedSealReader.cs` (F3 re-lectura del servidor)
- `Casting/SealCharge.cs` (F5 compensación)
- `Casting/PortalRecipe.cs` (`PortalMath`, F0 por viajero)
- `Recognition/GlyphClassifier.cs` (F3 lectura asíncrona)
- `Recognition/SentisGlyphClassifier.cs` (F3 CPU en headless)

**Output**
- `Output/SpellOutputController.cs` (F0 viewer; F3 VFX desde el mensaje)
- `Output/MagicField.cs` (F3 sólo servidor)
- `Output/Bodies/RockSpell.cs`, `Output/Bodies/RockForming.cs` (F2/F3 prefab y semilla)
- `Output/Bodies/CrystalSpell.cs` (F0 sin `Find`/`Camera.main`)
- `Output/Bodies/WaterSpell.cs`, `Output/Bodies/WindSpell.cs`, `Output/Bodies/WindColumn.cs` (F3)

**Reactions**
- `Reactions/MagicWorld.cs` (F0 sin `Find`; F4 servidor)
- `Reactions/GrassFire.cs` (F4 bitset)
- `Reactions/WaterDropSink.cs`, `Reactions/WaterPuddleManager.cs`, `Reactions/FloodZone.cs`, `Reactions/IceSheet.cs` (F4)
- `Reactions/ElementReactive.cs` (F4 estado replicado)
- `Reactions/WorldResetter.cs` (F4 sólo el host)

**World**
- `World/WorldObjectBudget.cs` + `World/BudgetMath.cs` (F0 por dueño, KI-113)
- `World/SupplyPoint.cs` + `World/SupplyMath.cs` (F0/F4)
- `World/RecyclingStation.cs` + `World/RecycleMath.cs` (F0/F4)
- `World/Rune.cs`, `World/SealTrigger.cs`, `World/RuneJoin.cs` (F0 reloj; F3 red)
- `World/Portal.cs`, `World/PortalGate.cs`, `World/PortalPlate.cs` (F0 viajeros por jugador; F4 red)
- `World/PhysicalCanvas.cs` (F2: sin emparentar a la cámara ni a la mano)
- `World/PhysicalTool.cs`, `World/Inkwell.cs`, `World/Movable.cs`, `World/PinnedBody.cs`, `World/ExpiringBody.cs`, `World/Crystallized.cs` (F0 `heldBy`; F2 red)
- `World/CanvasJoinSystem.cs` (F2 prefab; F3 servidor)
- `World/Stamp.cs` (F3 id de glifo)
- `World/Respawnable.cs` (F0/F4)
- `World/Belt.cs` (F2 inventario)

**Combat**
- `Combat/Health.cs` (F5 `NetworkVariable`)
- `Combat/ImpactDamage.cs` (F5 servidor)
- `Combat/DamageModel.cs` (reusar)

**Learning y UI**
- `Learning/Grimoire.cs`, `Learning/Discovery.cs`, `Learning/LibraryBook.cs` (locales; F2 grimorio por jugador)
- `UI/MainMenu.cs` (F1 Hospedar/Unirse)
- `UI/HudController.cs` (F1 jugador local)

**Editor** (`Assets/_Project/Editor/`)
- `HarnessPlaySmoke.cs`, `HarnessCanvasMatrix.cs`, `HarnessSeals.cs` (extender)
- `Map/` (`MapBuilder`: agregar `NetworkObject` y prefabs)

**Clases nuevas propuestas** (no existen todavía):
- `IPlayerContext`, `PlayerRegistry`, `IGameClock`, `StrokeId`, `StrokeCodec`, `Holdable`, `PlayerInventory`;
- `NetSession`, `CanvasNet`, `PlayerInventoryNet`, `WorldStateNet`, `FollowAnchor`, `EconomyStats`;
- `DuelGameMode`, `DuelRules`, `SealDeck`, `AICaster`;
- `HarnessNetSmoke`, `HarnessNetGrab`, `HarnessNetCanvasMatrix`, `HarnessNetEconomy`, `HarnessNetDuel`.

## 9. Decisiones tomadas por Ale (2026-10-09)

1. **Movimiento:** cada jugador dueno de su cuerpo; el servidor manda los empujones de los hechizos (2.7 A). Prediccion completa solo si el PvP la pide.
2. **Objeto agarrado:** propiedad al que agarra en coop; resorte del servidor en Duelo.
3. **Reconocimiento:** el cliente propone y el servidor valida lo barato siempre; re-lee con el modelo solo en PvP y en hechizos costosos.
4. **Topologia:** un jugador hace de host por Relay; 4 en coop y 2 (+ IA) en Duelo; 8 como posible. Sin servidor dedicado por ahora.
5. **WebGL: DENTRO del alcance desde el inicio** (cambia la recomendacion del plan): el transporte debe ser **WebSocket (wss)** para todos los jugadores desde F1; el reconocimiento en WebGL corre en CPU (KI-103). Hay que resolver KI-104 antes de F1 y verificar el soporte de Relay/Sesiones sobre WebSocket en el SDK instalado.
6. **F0 ya**, antes de instalar Netcode (limpieza de estaticos, ownerId, reloj inyectado, StrokeCodec, partir PlayerInteractor).
7. **Duelo local** (D1/D2: arena, reglas, IA) despues de F2, en paralelo con F3-F4.

Contratos de F0 ya creados: `Core/IPlayerContext`, `Core/PlayerRegistry`, `Core/IGameClock` (+ `GameClock`).
