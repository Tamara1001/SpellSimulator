---
tags: [doc, changelog]
---
# Changelog

Entradas más nuevas arriba. Una por tarea/commit relevante: fecha, qué, por qué, docs tocadas.


## 2026-10-10 — F6 del multijugador + Modo Libre diferido: efectos del agua, espectadores, coberturas, anti-trampas, hechizos propios y portales por libros

- **Anti-trampas simple** (`Net/World/NetRateLimiter.cs`, 4 tests): cubo de fichas por (cliente, canal) en el anfitrión (agarrar, sostener, soltar, reiniciar, cruzar portal, trazos, lanzar); lo que excede se descarta y se cuenta (`Dropped`). Además `OnHold` descarta poses a más de 12 m de quien sostiene. Sin validación de velocidad de jugadores: no habrá competitivo con premios (ADR-035).
- **Duelo:** espectadores (el tercero que entra mira desde una tribuna sobre la arena, sin tinta ni lanzamiento; HUD "Jugador 1/2"); coberturas destructibles (`DuelCover`, 60 de vida, solo por hechizos; el anfitrión manda la máscara en `du.state` y se restauran cada ronda); un sello compuesto ejecuta todos sus interiores (`SpellCaster.CompileAll`) y cobra tinta una sola vez (ventana de 0,6 s por jugador).
- **Tinta limitada en coop** (opcional): casilla «Tinta limitada (sala cooperativa)» al crear sala (`NetCast.CoopInk`); mismas reglas que el duelo, un saldo por jugador.
- **Efectos del agua replicados** (`Net/World/NetEffectsSync.cs`; cierra KI-121): charcos (cada 0,25 s si cambian), inundaciones, hielo y su congelación llegan a los clientes; quien entra tarde recibe todo.
- **Hechizos propios** (ADR-034, `Learning/SpellLibrary*`, `SpellRecording`, `Casting/CastSnapshot`; 9 tests): «Guardar el último hechizo» en la lista del grimorio guarda los trazos del último lanzamiento (en unidades de anillo) en `hechizos.json`; nombre automático («Fuego propio 2») y renombrar con el teclado desde su página (solo PC; en VR queda el automático); borrar con doble toque.
- **Portales por libros** (ADR-036, informativo; `Learning/PortalKnowledge`, 3 tests): libros «Caminos del valle» y «Caminos de las alturas» anotan en la nueva sección Portales del grimorio la identidad de 4 portales; no es requisito para usarlos. `GlyphLore` lee `portals` de `glifos.json`; `Grimoire.portalsFile` lo asigna `Setup Grimoire`.
- **Mapa:** los 3 libros de hechizos pendientes (`spells-siege`, `spells-water-deep`, `spells-crystal`) y los 2 de portales están en atriles (`MapStructures.StandBook`) en la torre de vigía, el círculo de piedras y el santuario. **Pistas «Se dice que...»**: la página de Hechizos del grimorio muestra la de un hechizo aún no conocido (rota al cambiar de página).
- **Harness:** `Run Bots Duel Extras (local)` 20/20 y `Run Bots Effects (local)` 9/9 (nuevos), `Duel Ink` 8/8, `Duel` 16/16; EditMode 464/464.
- **Pendiente:** Quest/VR, WebGL wss, IA rival (descartada), compensación de latencia (no se hará, ADR-035), pruebas a mano.

## 2026-10-10 — F5 del multijugador (segundo lote): arena propia, tinta y lectura del servidor

- **Arena** (`Net/World/DuelArena.cs`): plataforma cuadrada de 40 m en el cielo (centro 0, 100, 0; dentro del límite de 150 m de `Respawnable`), con muros del perímetro, 4 pilares, 6 muros bajos de cobertura simétricos y una fuente de agua (`WaterVolume` reutilizando el aspecto del lago). Se arma en cada máquina con las mismas medidas y se desarma al terminar; cada jugador vuelve a donde estaba. Como allá no hay mesas, el anfitrión crea 3 papeles limpios por lado en cada ronda (clones de la plantilla de `SupplyPoint`, replicados a los clientes y destruidos al terminar).
- **Tinta del servidor** (`Combat/DuelInk.cs`: `InkLedger` + `DuelInk.CanvasCost`, 4 tests): en el duelo cada jugador tiene un saldo (100, recarga 3/s, `DuelRules`) que el servidor descuenta al aceptar un hechizo según el peso de los trazos del lienzo; si no alcanza o no es fase de ronda, el hechizo no se ejecuta (también para el anfitrión: `SpellOutputController` consulta `NetCast.DeniedForHost`). La barra azul del HUD muestra el saldo autoritativo (`du.state`).
- **Re-lectura obligatoria en el duelo** (`NetCast.OnPropose`): el servidor ignora el hechizo propuesto, vuelve a leer el lienzo (`SpellCaster.Compile`) y ejecuta SU lectura; cuenta cuántas propuestas corrigió.
- **Harness:** `Run Bots Duel Ink (local)` (8/8): un rival tramposo dibuja tierra y propone fuego 25 veces; el servidor corrigió las 10 aceptadas (ejecutó tierra) y cortó 15 por falta de tinta. `Run Bots Duel (local)` sigue en 16/16 con la arena; coop (`Run Bots Cast`) 23/23; EditMode 448/448; Play Smoke en verde.
- **Falta** (KI-122): coberturas destructibles, daño real con hechizos probado a mano, tinta de los trazos fuera del duelo (coop), IA (D2), compensación de latencia, balance con partidas reales.

## 2026-10-10 — F5 del multijugador (primer lote): Duelo 1 contra 1 en red

- **Reglas puras** (`Combat/DuelMatch.cs`): `DuelRules` (2 rondas para ganar, cuenta atrás 3 s, ronda 90 s, pausa 4 s, tope 5 rondas) y `DuelMatch`, máquina de estados Countdown → Round → RoundEnd → MatchEnd; gana la ronda quien deja al otro en 0 o, al acabarse el tiempo, quien tiene más vida (empate = nadie suma); `Forfeit` si el rival se va. 8 tests EditMode (444 en total).
- **Red** (`Net/World/NetDuel.cs`, integrado en `NetWorld`): solo el anfitrión (jugador 0) corre las reglas y el daño (`Health` agregado a su jugador y al avatar del rival, jugador 1; fuera de la ronda nadie pierde vida); los clientes reciben el estado a 10 Hz (`du.state`: fase, ronda, victorias, vidas) y la orden de reaparecer (`du.tp`). HUD IMGUI con ronda, victorias, tiempo, barras de vida y carteles de cuenta atrás / ronda / final. Arena provisoria: el duelo se juega donde está parado el anfitrión y los dos reaparecen a 9 m a cada lado.
- **Menú:** «Modo Duelo» ya no es «próximamente»: abre la hoja de Multijugador con el aviso del duelo; la sala que crea el anfitrión es un duelo y arranca cuando entra el rival.
- **Harness:** `Run Bots Duel (local)` (16/16): un bot entra de rival; el anfitrión mata a uno por ronda (1-0, 1-1, 2-1) y se comprueba que el rival ve las 3 rondas, el 1-1, el final con ganador 0, las vidas a 0 y su reaparición en el punto correcto.
- Regresión: EditMode 444/444 y Play Smoke en verde.
- **Falta de F5** (ver KI-122): daño real con hechizos entre jugadores sin probar a mano (la prueba usa daño directo), arena propia con coberturas y fuente de agua, tinta validada por el servidor y corrección del elemento declarado, compensación de latencia, IA (D2), prueba a mano con dos personas.

## 2026-10-10 — F4 del multijugador: suministros, portales y mundo reactivo

- **Suministros y reciclaje** (`SupplyPoint`, `RecyclingStation`): solo el anfitrión repone y recicla; lo que crean (clones de plantillas de la escena) se replica con `NetSpawnKind.Clone` + `NetTemplates` (el cliente clona la misma plantilla en la misma pose; las runas recicladas conservan su dueño). Cualquier cuerpo que se destruye (reciclaje, expiración, tope de objetos) avisa a los clientes (`OnBodyDestroyed`). Los lienzos de los cuerpos creados en juego (papel, runas) se registran en el dibujo compartido (`NetCanvasSync.RegisterDynamic`, id derivado del id del cuerpo): se puede dibujar en el papel de suministro.
- **Portales** (`Portal`): solo el anfitrión lee la placa y abre o cierra la ruta; el estado viaja a los clientes (`pt.state`, `Portal.NetApply`); para cruzar, el cliente pide permiso (`pt.cross`), el anfitrión valida (portal abierto, avatar a ≤ 8 m, enfriamiento POR JUGADOR) y le ordena teletransportarse (`pt.tp`, `Portal.NetTeleportLocal`). Quien entra tarde recibe el estado de los portales abiertos. Verificado con bots: un cliente dibuja la identidad de `watchtower` en la placa de `tower-study`, el anfitrión la lee y abre la ruta, y otros dos clientes cruzan casi a la vez y llegan a 0,9 m del destino (14 de 16 chequeos; los 2 que fallaban eran un error de lectura de la coma decimal en el harness, ya corregido).
- **Mundo reactivo** (`NetReactiveSync`, `GrassFire`, `ElementReactive`, `MagicWorld`): solo el anfitrión simula las reacciones; un cliente recibe el estado: las 4900 celdas del pasto a 2 bits (1225 bytes) cada 0,5 s si cambian (`GrassFire.NetFollower`, `NetPackState`, `NetApplyState`) y el estado de cada objeto reactivo (humedad, quemado, integridad, banderas) a 4 Hz; quien entra tarde recibe todo. **Verificado con bots** (`Run Bots World (local)`, 23/23): fuego del pasto, un objeto que arde y se moja, suministros y un trazo dibujado en un papel de suministro; dos bots y un tercero que entra tarde ven lo mismo (coinciden entre sí y a pocas celdas del anfitrión, que sigue evolucionando).
- **Arreglos al probarlo:** los suministros que se crean antes de que empiece la red (relleno inicial) no estaban registrados: `SupplyPoint.NetBegin` hace que el anfitrión los adopte y un cliente descarte los suyos; un clon de plantilla hereda el `NetBody` de la plantilla, así que `CreateReplica` reutiliza el existente (antes daba NullReferenceException al entrar tarde).
- Harness: `Run Bots Portal (local)` y `Run Bots World (local)`.
- Regresión: EditMode 436/436, Play Smoke y entrada real (30 chequeos) en verde; portales 16/16.
- Sin hacer (límites en KI-121): cristalización y rotura en los clientes, `HarnessNetEconomy` (2 jugadores no agotan los suministros en 5 min); charcos/hielo/inundaciones siguen saliendo de los hechizos en cada máquina (no se replican como estado).

## 2026-10-10 — F3 del multijugador: dibujo compartido y hechizos replicados

- **Dibujo compartido** (`NetCanvasSync`): se replican los TRAZOS de todos los lienzos de la escena (no texturas). Cada máquina pinta al instante y cada 50 ms compara la sesión de cada lienzo con lo último sincronizado (trazos nuevos, puntos agregados a un trazo en curso, trazos quitados, lienzo borrado); sirve igual para lápiz, goma, deshacer, trapo, estampas y lo que rompe o limpia un lienzo, sin tocar cada herramienta. El servidor valida el alcance (15 m) y reenvía; quien entra tarde recibe los lienzos con trazos. Los trazos se identifican por (dueño, id): dos jugadores dibujando a la vez en el mismo lienzo no chocan (`Session.SetOwnerForNewStrokes` con el id de cliente). Mensajes fragmentados y en orden (un sello entero pasa del máximo de 1264 bytes de un mensaje `Reliable`).
- **Lanzamiento compartido** (`NetCast`, `SpellIRCodec`): quien cierra el anillo lo lee con el modelo en su máquina (brillo, carga y anotaciones al instante) y propone al anfitrión el `SpellIR` ya compilado; el anfitrión valida (lienzo, alcance, frecuencia) y lo ejecuta (cuerpos y fuerzas); los demás clientes reciben el hechizo y lo muestran. El efecto local de un cliente es solo visual. Las runas y placas (`SpellCaster.Trigger`) solo las decide el anfitrión. `SpellOutputController.SpawnSpellEffect` consulta `NetCast.Intercept`; `SpellBodies.Spawn` recibe `visualOnly` (la roca la crea solo el anfitrión; el cristal corre en todas las máquinas).
- **Cuerpos creados en juego** (`NetSpawn`, `NetWorld.RegisterSpawned`): la roca del hechizo de tierra se replica: el anfitrión avisa y cada cliente arma una copia (`RockSpell.BuildReplica`) que crece igual y sigue sus poses; al desaparecer en el anfitrión se quita en los clientes; quien entra tarde la recibe en el estado completo.
- `SentisGlyphClassifier`: sin GPU de cómputo (ejecutable sin gráficos, equipos viejos) el reconocedor usa la CPU. `SpellCaster.Glyphs` expone el diccionario.
- Harness: `Run Bots Draw (local)` (14 chequeos: dos bots dibujan a la vez en el mismo lienzo, otro deshace, el anfitrión dibuja, un cuarto bot entra tarde y ve lo mismo) y `Run Bots Cast (local)` (23 chequeos: sellos de tierra y viento lanzados por clientes, propuestas aceptadas, roca replicada también a quien entra tarde, lienzos y cuerpos iguales en todas las máquinas y el viento del cliente empuja una caja). 436 tests EditMode (+13: `SpellIRCodec`, `NetSpawn`).
- Límites conocidos (F3): la unión de mitades y de runas (`CanvasJoinSystem`, `RuneJoin`) crean lienzos en juego que no se replican; la tinta se descuenta en el cliente (no la valida el servidor: PvP); los hechizos de otros no muestran el brillo de carga; la cristalización de cuerpos solo la aplica el anfitrión (sin el tinte en los clientes); no se corrió la matriz de lienzos desde un cliente (la lectura es la misma que en solitario).

## 2026-10-09 — F2 del multijugador: mundo compartido (objetos físicos y agarre)

- `Scripts/Net/World/`: `NetWorld` (gestor; vive en el GameObject del NetworkManager, solo en el singleton), `NetBody` (un cuerpo replicado) y `NetBodyMath` (parte pura, 13 tests). **No se usan `NetworkObject` por cuerpo**: los objetos son de la escena, no prefabs; la replicación va con mensajes con nombre de Netcode (`sw.*`).
- **Quién simula qué:** el anfitrión simula lo suelto y manda las poses cambiadas a ~15 Hz (+ cuadros completos rotativos por si se pierde un paquete, + estado completo al entrar un cliente). Los clientes ven los cuerpos como seguidores cinemáticos. El que agarra algo lo lleva con su propia física (el agarre empieza al instante) y manda su pose a ~20 Hz; al soltar manda pose y velocidad y el servidor retoma la simulación.
- **Agarre validado:** un cliente pide al servidor (`sw.grab`); si otro lo tiene o está muy lejos (20 m) el servidor lo rechaza y el cliente lo suelta. `PlayerInteractor.TryGrab` rechaza al instante lo que ya sostiene otro (`NetWorld.AllowLocalGrab`) y tiene `ForceRelease`. El servidor reproduce `OnRemoteGrab/OnRemoteRelease` (lienzos) o `OnDrop` + `PinnedBody.Pin` (el resto), así que soltar, arrojar y dejar flotando dan lo mismo en todas las máquinas.
- **Ids:** orden de las claves de ruta de los objetos con `Rigidbody` + `Respawnable` que existen al cargar la escena (los raíz se distinguen por posición: en un ejecutable el índice de hermano de la raíz es siempre 0). Una huella (FNV-1a) avisa si el anfitrión y el cliente tienen versiones distintas del mundo y apaga la replicación.
- **Reinicio del mundo en red:** un cliente se lo pide al anfitrión (`MagicWorld.ResetWorld` consulta `NetWorld.InterceptReset`); el anfitrión reinicia y avisa a todos. Al salir de la sala o perderla, el mundo se reinicia (no hay guardado de partida).
- **F1 restos:** botón «Reconectar a CÓDIGO» (si se cortó la conexión y la sala sigue viva); el mundo se reinicia al salir; la entrada tardía recibe el estado completo.
- Harness: `Run Bots Grab (local)` y `Run Bots Grab (150 ms)` (tres bots: pelean por `Crate_0`, se pasan `Crate_1` —arrojada, flotando, rechazada mientras la tiene otro—, el anfitrión empuja 30 objetos; todos vuelcan posiciones y se comparan, 20 chequeos). Opción `-botlatency`/`NetSession.SimulatedLatencyMs` (latencia simulada en el transporte).
- Verificado: 428 tests EditMode, smoke de Play, entrada real (30), pruebas de red y bots (caminar, churn, Relay, agarre con y sin 150 ms).
- Límites conocidos (F2): lo que se crea durante el juego (reposición, hechizos, reciclaje) no se replica todavía (F3/F4); un cliente que empuja una caja caminando la encuentra como una pared (sus cuerpos son cinemáticos); el dibujo de los lienzos no se replica (F3); VR no se probó.

## 2026-10-09 — F1: bots de red
- `Scripts/Net/Bots/` (`BotOptions` con 6 tests, `NetBot`): jugador automatico que se une por Relay o IP directa, camina y salta con un gamepad virtual, informa lo que ve de los demas y sale con codigo 0/1. Guion `churn`: entra y sale varias veces.
- `NetSession.AuthProfile`: perfil de autenticacion por bot (si no, varios bots en la misma PC compartian el jugador anonimo y Relay respondia «player is already a member of the session»).
- Editor: `BotBuild` (menu `Build/Windows (bots)`) y `HarnessBots` (`Run Bots (local | Relay | churn local)`). Verificado: 3 bots por loopback y por Relay real, el anfitrion los ve moverse; churn con entradas y salidas sin avatares fantasma. 415 tests EditMode.
- Hallazgo: el jugador solo rota con la vista si el cursor esta capturado (`PlatformDetector.IsLookCaptured`); un proceso sin ventana no lo captura. Los bots giran el cuerpo directamente.

## 2026-10-09 — F1: arreglos tras la primera prueba entre dos personas

- Probado por Ale y Tami: crear sala y unirse funcionan. Fallos hallados y corregidos: (1) el cliente quedaba en el menú tras unirse (la cabeza y el nombre de su avatar se veían a 34 m, en la órbita de la cámara del menú); ahora la entrada al juego la dispara el estado de `NetSession` y no la continuación del `await`; (2) volver al menú principal desde el juego sale de la sala; (3) si el anfitrión se va o cierra la sala, el cliente vuelve al menú con aviso (evento `NetSession.Disconnected`); (4) el avatar no copia la cámara del menú.
- `NetSession`: los eventos y el HUD salen siempre en el hilo principal (`Update`); se engancha a `RemovedFromSession`/`Deleted` de la sesión; avisa si un `await` termina fuera del hilo principal.
- Harness: `Run Net Disconnect Probe` (13 chequeos: el anfitrión se va, volver al menú sale de la sala, sin jugadores fantasma).

## 2026-10-09 — F1 del multijugador: menú Multijugador

- `MainMenu` (ahora `partial`, con `MainMenu.Net.cs`): botón **Multijugador** y hoja con nombre, **Crear sala** (muestra el código), **Unirse** con código y **Salir de la sala**. Al conectar entra directo al juego. Reemplaza el cartel «Multijugador: próximamente».
- `NetHud`: cartel «Sala CÓDIGO · N jugadores» arriba a la derecha mientras hay conexión (IMGUI, solo PC/WebGL; la versión VR queda con KI-095).
- Harness: `Run Net Menu Probe` (abre la hoja, comprueba botones y campos, captura `Logs/menu_net.png`; 6 chequeos).

## 2026-10-09 — F1 del multijugador: sesión y avatar de red (primera mitad)

- `Scripts/Net/`: `NetSession` (crea o se une a una sala por código de Relay con WebSocket `wss`, o por IP directa para pruebas; arma el `NetworkManager` en tiempo de ejecución, sin tocar la escena), `NetPlayer` (avatar de red: raíz con `ClientNetworkTransform` de autoridad del dueño, cabeza, manos y nombre sincronizados por `NetworkVariable`; los remotos se registran en `PlayerRegistry`) y `ClientNetworkTransform`.
- `PlayerRegistry.LocalOwnerId`: el jugador local usa el id de cliente de Netcode (antes era 0 fijo).
- Editor: menú `SpellSimulator/Net/Crear avatar de red` (genera `Resources/Net/NetPlayer.prefab`) y `HarnessNetSmoke` (`Run Net Smoke (local)`: anfitrión y cliente virtuales por loopback, 15 chequeos; `Run Net Relay Probe`: sala real en Relay por WebSocket, 7 chequeos). Resultado en `Logs/harness-netsmoke.txt`.
- Verificado: Relay y Authentication activos en el proyecto de Unity Cloud; Relay exige `UseWebSockets = true` en el transporte. Pendiente de comprobar en el build WebGL real que la conexión sea `wss` (el transporte informa `UseEncryption=false` en el editor).
- Falta de F1: menú Hospedar/Unirse en `MainMenu`, cuerpo de avatar más fino, reconexión y entrada tardía, prueba PC↔Quest.

## 2026-10-09 — F1 del multijugador: paquetes

- Instalados Netcode for GameObjects 2.13.3 y Multiplayer Services 2.4.0 (trae Unity Transport 2.7.4, con WebSocket y `wss` para Relay: WebGL entra en el alcance). KI-104 (WebGL en equipos ajenos) marcado como resuelto. 409 tests EditMode en verde.

## 2026-10-09 — Cierre de la octava tanda
- Ale probo a mano toda la checklist de PC de `TESTING.md`: en orden. Se marcan como resueltos KI-111, 112, 113, 115 y 116; quedan como **diferidos** (mejoras o contenido futuro, no bugs) KI-106..109, 114, 117 y 118. `STATUS.md` reescrito con el estado real (409 tests, plan 1-6 completo, F0 hecho, siguiente F1). Documentacion al dia para retomar el multijugador.

## 2026-10-09 — F0 del multijugador (preparacion, sin red ni cambios visibles)
- **Contexto de jugador:** `Core/IPlayerContext` + `PlayerRegistry` + `Player/LocalPlayerContext` (lo agrega `PlayerMovement`); la logica de mundo (`WorldObjectBudget`, `Crystallized`, `ExpiringBody`, `MagicWorld`, `CrystalSpell`, `Movable`, `Respawnable`, `Portal`, `GrassFire`, `SpellOutputController` con el lanzador como punto de vista) consulta el registro en vez de `Camera.main`/`FindFirstObjectByType`. Enfriamiento de portales POR jugador; presupuesto de objetos con `ownerId` y tope por dueno (solo con mas de un jugador).
- **Reloj inyectable:** `Core/IGameClock` + `GameClock`: 29 temporizadores de reglas migrados (carga de sellos, enfriamientos, flotado, uniones, suministros, reciclaje, portales); lo visual sigue con `Time`. Contrato `IGameRandom` (aun sin usos: no hay azar de reglas). `FakeGameClock` para pruebas.
- **Trazos con identidad:** `Stroke.OwnerId/StrokeId`, `DrawingSession.Revision`, `Core/StrokeCodec` (UV de 12 bits con deltas, ~2 B/punto, error < 1/2048, tolera datos truncados). `ownerId` en `Rune`, `Stamp`, `RecyclingStation` y `RockSpell`.
- **`PlayerInteractor` partido** (727 -> 420 lineas): `PlayerInventory` (estado puro), `Holdable` (quien lo agarra), pedidos `TryGrab/Release/Throw` (`GrabRequest`), y partials `.Grab`/`.Hints`. API publica intacta.
- Pruebas: 409 EditMode, smoke, entrada real (30), matriz de lienzos completa (425) en verde. Hecho por subagentes (Opus: refactor del interactor; Sonnet: contexto, reloj, ownerId/codec). Pendiente de F0: ver KI-118.

## 2026-10-09 — Plan de multijugador, mas hechizos con descubrimiento y menu de pausa
- **`Docs/MULTIPLAYER_PLAN.md`** (planificacion y auditoria, sin codigo): estaticos y singletons que rompen la red, modelo de autoridad (servidor autoritativo con Netcode for GameObjects + Relay), replicacion de trazos (codificador compacto, ~5 KB contra 4 MB de textura), reconocimiento (el cliente propone y el servidor valida lo barato), economia en red, PvP/Duelo, presupuesto de ancho de banda y fases F0-F6 (70-95 dias; coop jugable F0-F4, 45-60). Incluye 8 preguntas para Ale con recomendacion.
- **19 hechizos prearmados** (12 nuevos en `Data/Spells`) y TRES caminos de descubrimiento: por experimentacion (lanzar una composicion que coincide con el catalogo la descubre: `SpellCatalog.MatchCast`, `SpellDiscoveryTracker`, `Discovery.OnCast`), por libros (3 libros nuevos `spells-water-deep`, `spells-crystal`, `spells-siege` y los 2 existentes ampliados; falta colocarlos en el mapa) y por pistas (`hint` en cada hechizo: «Se dice que...»; falta mostrarlo en la UI del grimorio). Guardado de descubrimientos v2 (migra solo).
- **Menu de pausa dentro del grimorio** (seccion VII «Pausa», se abre directo con Esc): Reanudar, Reiniciar el mundo (confirmacion con segundo clic en 3 s), Controles, Opciones y sonido, Salir al menu; ajustes rapidos de sensibilidad, invertir Y, calidad y mano principal. Pistas «V: cambiar tinta» en la pluma y el tintero fijo (solo con cinturon y 2+ clases de tinta).
- Pruebas: 354 EditMode y smoke en verde. Hecho por subagentes (Opus: plan; Sonnet: hechizos y pausa). Pendiente de ver en pantalla: layout de la pagina Pausa, siluetas de los hechizos nuevos. Docs: PLAN_OCTAVA_TANDA, KNOWN_ISSUES.

## 2026-10-09 — Ajustes tras la prueba a mano: portales v2, mitades, vista completa de runas chicas
- **Bug de Tab con runas (RESUELTO, causa medida con `HarnessRealInput`):** `PhysicalCanvas.SetFocus` buscaba el componente `Camera` en `CameraRootPC` (no esta ahi) y usaba un plano cercano de 0,1 m en vez de 0,3 m: las runas chicas (`ImpactStone_S`, `ImpactGold_S`) quedaban a 0,22-0,25 m, DELANTE del plano cercano (invisibles) y el rayo del puntero (`ScreenPointToRay` arranca en ese plano) las pasaba de largo. Ahora usa la camara real. Nueva sonda con teclado y mouse virtuales (`SpellSimulator/Harness/Run Real Input Probe`, 30 chequeos con 6 lienzos: E, Tab, dibujar con el mouse, Tab, soltar).
- **Portales v2** (rediseno de Ale): cada portal tiene una IDENTIDAD de 3 runas (sigilo + 2 signos; `Data/Portals/portals.json` v2) grabada en su anillo; se escriben en la placa de un portal las runas del DESTINO (tolera 1 signo de mas); la ruta se abre 30 s en ambos sentidos, se cancela si otro abre hacia el mismo destino, y se CRUZA por un trigger (no hay teletransporte por cercania). Portales circulares con disco animado (`PortalSurface.shader`, `PortalVisuals`): oscuro, carga, remolino y aviso de cierre. Identidades: tower-study = crystal+collection+region; watchtower = aeroform+column+pull; field-circle = earth+crush+weave; cave = fire+dispersion+column.
- **Mitades mas faciles:** tolerancias generosas, cierre automatico de la costura y cada mitad debe aportar al menos 25 % del anillo (40-60 % por lado; 80 % total no se une, 90 % si). Log `[Mitades]` con el motivo de cada rechazo.
- **Reiniciar el mundo ya no crashea** (`SealAnnotations` con textos destruidos). Arreglos visuales: marco de puerta de la cabana sur sin solape, barandas de la torre de vigia mas altas y con colision, canilla de la estacion de agua eliminada.
- Pruebas: 334 EditMode, smoke, `PORTAL` (15), `HALVES` (11) y la sonda de entrada real (30) en verde. Hecho por subagentes (Sonnet: logica de mitades y portales; Opus: portales circulares y arreglos).

## 2026-10-09 — Bloque 6 del plan: estructuras, portales, reposicion y reciclaje
- **Estructuras** (`Editor/Map/MapStructures.cs`, raiz `Map/Structs`): torre de vigia sobre una loma (con rampa y portal), 2 cabanas (mesa, tintero fijo, papeles y un libro de hechizos), cueva (portal, runas de metal), 4 afloramientos de roca como cobertura, circulo de piedras con portal, santuario con estacion de reciclaje y puesto de suministro. Se generan desde `MapBuilder.Build()` sobre `ValleyShape`.
- **Portales** (`World/Portal`, `Casting/PortalRecipe`, `Data/Portals/portals.json`): 4 portales en 2 pares (`tower-study`<->`watchtower`, `field-circle`<->`cave`). Cada uno tiene una placa donde se dibuja una receta de glifos (sigilo + 3 o 4 signos; `field-circle` es ANIDADA); si el reconocedor la lee igual, el portal carga 2 s, se abre 10 s y el jugador a menos de 2,5 m viaja al par. Un signo de mas o la receta de otro portal no abren. Aun sin aprendizaje de recetas ni efectos (estados y `Progress01` listos).
- **Reposicion anti-agotamiento:** `SupplyPoint` (clona una plantilla cada X s con limite de vivos por `ownerId`), `RecyclingStation` (1 runa agotada = 1 nueva de su clase; una no agotada cuesta 2; papel = papel limpio; 8 s) y `WorldObjectBudget` (tope 40 sueltos y 12 por tipo; los mas viejos fuera de manos y a mas de 15 m de la camara se achican). Runas de oro solo en un relicario lento (600 s). Aviso al agotarse una runa y «(agotada)» en su nombre.
- Pruebas: 318 EditMode; smoke (+ chequeo de 4 portales, suministros y estacion); `HarnessCanvasMatrix Only=PORTAL` (7 chequeos con el reconocedor real: receta correcta abre, signo de mas / receta de otro no abren, el jugador viaja al par). Capturas revisadas. Hecho por subagentes (Sonnet: reposicion y portales; Opus: estructuras). Pendiente: prueba a mano (alturas de terreno, rampa de 30 grados, portal del estudio junto a mesas), aprendizaje de recetas por libros, efectos de portal. Ver KI-113 a KI-115.

## 2026-10-09 — Circulos magicos anidados
- Un anillo EXTERIOR cerrado que contiene 2 a 4 anillos INTERIORES cerrados (radio <= 60 % del exterior, dentro con margen del 8 %, sin solaparse), cada uno con su glifo, se lee como un sello compuesto: cada interior es un hechizo completo (elemento y signos propios) y todos salen a la vez desde su posicion; el exterior AMPLIFICA (`1 + 0,5 ln(exterior/mayor interior) + 0,5 ln(n)`, tope 2,5x sobre `inkPower` y `strength`). El retardo de lanzamiento es la suma de los de cada interior (maximo 4 s). Maximo 2 niveles. Si no hay interiores cerrados validos, se lee como siempre (`NestedSealReader.TryRead`, `NestedMath`, `SpellCaster.CastNested`, evento `NestedCompiled`).
- Para que el sello compuesto se reconozca hay que ABRIR el exterior primero (un arco) y cerrarlo AL FINAL: si se cierra un interior antes de que exista el exterior se lanza como sello normal (canon: el anillo se cierra al final).
- Pruebas: 287 EditMode; `HarnessCanvasMatrix Only=NESTED` (2 interiores = 2 lanzados, 1 interior = lectura normal, interiores abiertos = sin compuesto, carga y lanzamiento: 5 chequeos) y regresion de CHARGE, SPELLS y smoke en verde. Hecho por un subagente Opus. Pendiente: probar a mano, anotaciones (`SealAnnotations`) con varios `SealAnalyzed`, limite de hechizos activos (`maxActive` 6).

## 2026-10-09 — Tintas por tipo con cinturon y mitades para cualquier lienzo
- **Cinturon = tintas:** la barra vuelve a 3 slots. Sin cinturon se lleva UNA clase de tinta; con cinturon un slot de cartuchos por clase (normal, poderosa, resistente; 4 cartuchos de 5 usos cada una). **V** cambia la tinta activa, **R** recarga con ella (tambien en vista completa). `InkCartridgeStock` reescrito; HUD con slot de cartuchos grande o tres slots chicos con la activa resaltada; las pistas del panel de contexto no saltan de linea.
- **Mitades (canon Sylph Shoes):** cualquier lienzo con el anillo ABIERTO que queda pegado borde con borde (cualquier borde, mismo plano y cara) 0,4 s a otro se funde con el en un solo lienzo y el sello se completa y lanza; ambos se borran (`CanvasJoinMath`, `CanvasJoinSystem`, evento `SpellCaster.CanvasEdited`). Conviven con la fusion de runas armadas.
- Pruebas: 272 EditMode, smoke de Play y `HarnessCanvasMatrix Only=HALVES` (lado a lado, arriba/abajo y separados 0,5 m: PASS). Hecho por subagentes. Docs: PLAN_OCTAVA_TANDA (4b), GAME_DESIGN. Pendiente: circulos anidados (propuesta aprobada en el plan), pistas «V: cambiar tinta» en la pluma y el tintero.

## 2026-10-09 — Correcciones de la prueba a mano (rueda, recarga en vista completa, pistas)
- **Rueda estandarizada:** con cualquier cosa agarrada (tambien un cuaderno) la rueda acerca/aleja; en vista completa vuelve a ser el grosor de la herramienta (`PlayerInteractor.HandleWheel`, `ObjectMover`).
- **Recargar tinta (R) en vista completa** de lo que se mira sin agarrar (`SurfaceFocus`): ahora funciona.
- **Pistas de la HUD:** con algo en la mano secundaria se quitan de la principal «Q: arrojar» y «Rueda: grosor» (`HudText.DropHeldConflicts`), asi no se repiten ni saltan de linea; la secundaria muestra siempre «Rueda: distancia».

## 2026-10-09 — Bloques 4 y 5 del plan: runas por material, fusion, cinturon y grimorio
- **Runas:** todas con la misma logica (`Rune` + `SealTrigger` + `DrawingCanvas`); tipos Piedra (2 usos de por vida), Metal (5, resiste agua y fuego) y Oro (10, enfriamiento corto, brillo y luz propios). Se **agrietan** al gastarse (`RuneVisuals`) y una agotada ya no sirve («hay que reciclarla», bloque 6). Se eliminan las «mitades» (`SplitSealHalf`). Colocadas: 2 piedras de campo + 2 para probar la union + 1 metal (campo) + 1 oro (pedestal del estudio) + la placa de presion.
- **Fusion:** dos runas armadas que se tocan (`RuneJoin`, `SpellFusion`) lanzan UN hechizo: la mas grande manda tamano/origen/direccion y se unen las primitivas del plan; ademas se emite el efecto de la chica en el mismo origen (combos fuego+viento, agua+cristal). Cada una gasta un uso. `SpellCaster` gana `Compile`/`Emit`.
- **Cinturon** (`World/Belt`, `MapBelt`): en la mesa de alquimia (piso 2); al equiparlo suma 2 slots chicos laterales (teclas 4 y 5, `HotbarMath`). **Grimorio:** si queda suelto y te alejas mas de 12 m (2 s) vuelve al inventario (`GrimoireMath`, T lo reabre).
- Pruebas: 256 EditMode; smoke de Play (+chequeos de runas metal/oro y del cinturon); `HarnessCanvasMatrix Only=RUNEJOIN` (fusion y desgaste). Hecho por subagentes (Sonnet: logica; Opus: arte y HUD). Pendiente de probar a mano.

## 2026-10-09 — Bloque 3 del plan: roca, cristal, agua, viento y efectos de la tinta
- **Punto de enganche:** `Output/Bodies/SpellBodies.cs` (llamado desde `SpellOutputController.SpawnSpellEffect`) agrega el cuerpo fisico propio de cada elemento. Modulos: `RockSpell`, `CrystalSpell`, `WaterSpell`, `WindSpell` (+ `*Math` puros con tests: 231 EditMode en total).
- **Roca (tierra):** una roca fisica (radio = anillo, mas grande con tinta poderosa) que se forma flotando y cae; agarrable, arrojable, sirve de muro/escalon/proyectil; maximo 10; desaparece a los 120 s (`ExpiringBody`). **Cosas en el aire:** lo fijado con E se cae solo al minuto (`PinnedBody`, `PhysicalCanvas`).
- **Cristal:** esfera de radio anillo x4 (8-25 s): cristaliza los cuerpos cercanos (rigidos, quietos, indestructibles, celestes), placa de cristal en el piso y congela agua (`IFreezable`).
- **Agua:** gotas que colisionan, charcos que escurren por la pendiente (16), recipientes que se llenan (`WaterContainer`, menu `SpellSimulator/Water/Setup Containers`), zona inundada que hace flotar objetos livianos y hielo sobre lagos/charcos.
- **Viento:** arrastre ~ v^2/masa en un cono (los livianos vuelan mas), columna de aire que hace planear al jugador (`WindColumn`/`PlayerMovement`), el fuego del pasto se propaga segun el viento (`GrassFire.AddWindBias`) y el cono apaga llamas chicas (`MagicWorld.SnuffSmallFlames`).
- **Tinta:** la poderosa ya agranda roca, cristal y caudal de agua/viento (`inkPower`); la resistente impide que el agua rompa el sello (`SealBreaker`: funciona mojado y bajo el agua).
- Verificado: compilacion, 231 tests, smoke de Play y `HarnessCanvasMatrix Only=SPELLS` (lanza tierra, cristal, agua y viento sin errores: 8 chequeos). Hecho por subagentes (Sonnet: roca, cristal, viento; Opus: agua). Pendiente de probar a mano. Pendientes conocidos en KNOWN_ISSUES (KI-106 a KI-109).

## 2026-10-09 — Retardo de lanzamiento con brillo del anillo
- Cerrar el anillo de un sello (sin disparador) ya no lanza al instante: pasa a la fase `Charging` entre 0,5 y 2 s segun el tamano del anillo (`SealCharge.Duration`). Se puede mover el lienzo y apuntar; si se borra o edita durante la carga se cancela, y un trazo nuevo la reinicia. Runas, placas, estampas y `Cast` directo siguen instantaneos. `SpellCaster.castDelayEnabled` lo apaga.
- Brillo (`Output/SealGlowDirector` + `Resources/SealGlow.shader`): el anillo brilla del color del elemento (fuego rojo, agua celeste...), pulsa mas rapido y hace un destello al lanzar. Se crea solo junto al `SpellCaster`.
- Pruebas: `SealChargeTests`, `SealActivationTests` (+5), `SealGlowCurveTests`; harness `Only=CHARGE` (carga, lanzamiento y cancelacion, 5 chequeos). Hecho por subagentes (Sonnet: logica; Opus: brillo). Falta ver el brillo a mano (Bloom, Quest y WebGL: ver el shader en `Resources`).

## 2026-10-09 — Bloque 2 del plan: tinta, cartuchos y tinteros fijos/portatiles
- **Logica** (`Core/InkKind`, `InkMath`, `InkCartridgeStock`; +14 tests, 175 en total): el gasto por metro se normaliza al tamano del lienzo (referencia 0,5 m, tope x8); tintas Normal/Poderosa/Resistente (las especiales gastan x2; poderosa x2,5 de potencia; resistente = `waterproof`). Cada `Stroke` lleva su `Ink` y `SpellCaster` fija `SpellIR.inkPower/waterproof` con la tinta dominante (el efecto real va en el bloque 3).
- **Cartuchos:** `InkSupply.Cartridges` (5 usos cada uno, 2 al empezar, maximo 4). R gasta un uso, llena la pluma y la deja con la clase del cartucho; apuntando a un tintero fijo recarga desde el.
- **Tinteros:** fijos (pedestal de piedra, inagotables, no se levantan) y portatiles (frasco; E lo guarda como cartucho). Torre: 3 fijos + 4 portatiles (Normal x2, Poderosa, Resistente) y 1 portatil en el campo de practica.
- **HUD:** slot de cartuchos (frasco del color de la clase, contador y 5 pips de usos) y medidor de la pluma con el color/nombre de la tinta. Chequeo nuevo en `HarnessPlaySmoke`.
- Hecho por subagentes (Sonnet: logica; Opus: HUD y arte); verificado con compilacion, tests, smoke y captura. Pendiente de probar a mano. Docs tocadas: PLAN_OCTAVA_TANDA, STRUCTURE.

## 2026-10-08 — Cuaderno agarrado + vista completa a veces no dejaba dibujar
- Causa medida (`HarnessCanvasMatrix` con `Only=FOCUSRAY`, 40 chequeos): el rayo del lapiz tomaba el PRIMER collider; si otro objeto (otro papel, un mueble) quedaba entre la camara y el lienzo enfocado, no se dibujaba. Ahora `FocusMode.FocusedCanvas` (lo fijan `PhysicalCanvas.SetFocus` y `SurfaceFocus`) hace que, en vista completa, `PhysicalTool.TryShootRay` solo vea el lienzo enfocado (sin allocations: `RaycastNonAlloc`).
- Diagnostico nuevo: `Respawnable` loguea por que reaparece un objeto (cualquier objeto que reaparece pierde su vista completa). Docs tocadas: PLAN_OCTAVA_TANDA (pendientes: mas hechizos prearmados + descubrirlos, crear/guardar hechizos, menu de pausa), ROADMAP, STATUS, Home.

## 2026-10-08 — Bloque 1 del plan: controles nuevos (PLAN_OCTAVA_TANDA)
- E agarra y, con algo agarrado, lo deja en el aire (PinnedBody/flotar); Q arroja (se elimina «soltar»); T abre el grimorio; Esc lo abre a pantalla completa / lo cierra y reemplaza a B; Tab = vista completa del lienzo (antes V); F da vuelta la hoja y la saca de la vista completa; clic de la rueda sigue fijando; rotar con clic derecho es ~2,4x mas sensible (`RotateDegreesPerUnit` 0.6).
- Textos actualizados (HUD, grimorio, libros, sellos) y tabla de controles de GAME_DESIGN. Nuevo `PLAN_OCTAVA_TANDA.md` con todas las decisiones del plan (bloques 1-7). Pendiente: menu de pausa dentro del grimorio; confirmar a mano rotar mientras se camina.

## 2026-10-08 — Runa: diagnostico de "no sale el hechizo" sin cerrar la vista completa
- En Play, soltar la runa armada sin vista, con V abierta y tras cerrar V dispara el hechizo en los tres casos (`HarnessCanvasMatrix` con `Only=RUNE`, 3 chequeos PASS). Hipotesis mas probable: tras lanzar, la runa queda `Spent` y un circulo nuevo encima no la rearma (regla de `SealActivation`: hay que borrar el sello).
- `SealTrigger` ahora loguea cada golpe >= 1 m/s (velocidad, umbral, fase, usos) y el resultado del disparo, para diagnosticar a mano desde la consola. Docs tocadas: CHANGELOG.

## 2026-10-08 — Matriz de pruebas de lienzos (`HarnessCanvasMatrix`)
- Nuevo harness `SpellSimulator/Harness/Run Canvas Matrix` (Play, ~7 min, `Logs/harness-canvasmatrix.txt`): soltar, V + dibujar + soltar, fijar y desfijar, arrojar fuerte contra el piso, para 11 lienzos agarrables/runas, vista completa de 8 fijos, y caida fuerte/fijado de herramientas, sellos y movibles. Estado actual: PASS (413 chequeos). Filtro opcional: `SessionState "HarnessCanvasMatrix.Only"`.
- Docs nuevas: `Docs/TESTING.md` (tipos de lienzo, casos automaticos y checklist manual PC/VR). Docs tocadas: TESTING.

## 2026-10-08 — Lienzo/runa agarrada y en vista completa (V) quedaba pegada a la pantalla
- Causa (reproducida en Play): `PhysicalCanvas.SetFocus(true)` lo hace hijo de la camara; al soltarlo con E, Q o el clic de la rueda, `OnRemoteRelease` ponia `remoteHeld=false` antes de `SetFocus(false)`, que entonces no lo desparentaba. Quedaba hijo de la camara (congelado en el centro, sin poder agarrarlo ni dibujar). Ahora `OnRemoteRelease` hace `SetParent(null)`.
- Extra: `ObjectMover.FixedUpdate` deja quieto el objeto mientras `SurfaceFocus.IsActive` (antes la camara y el movedor se perseguian y el objeto huia).

## 2026-10-08 — Cuatro bugs de la prueba manual (fijado, flotacion, runa de fuego, atravesar el piso)
- **Fijar/desfijar:** `ObjectMover` dejaba el objeto generico (banco, mesa) como `isKinematic=true` y al volver a agarrarlo guardaba ese estado como "original", asi que nunca se soltaba. Nuevo `PinnedBody` (World): recuerda la fisica original, se quita al agarrar de nuevo (`PinnedBody.Unpin`) y restaura todo.
- **Flotacion:** `PinnedBody` aplica el mismo vaiven que `PhysicalCanvas` (menos cabeceo, para muebles grandes).
- **Crash de la runa de fuego:** `BurnEffect` mezclaba modos en `velocityOverLifetime` (x/z constante, y rango) -> "Particle Velocity curves must all be in the same mode". Ahora los tres ejes son TwoConstants.
- **Atravesar el piso:** los Rigidbody iban en deteccion Discrete. `Respawnable.Awake`, `ObjectMover` y `PhysicalCanvas.Fall` fijan `ContinuousDynamic`. Falta confirmar a mano con lapices lanzados fuertes.
- Test de humo: +1 chequeo (fijar y desfijar un objeto generico). Docs tocadas: STRUCTURE.

## 2026-10-08 — Graphify en Windows
- HARNESS: cómo instalar y correr graphify en Windows (`python -m graphify`, mover `graphify-out` fuera de `Assets/`). Docs tocadas: HARNESS.

## 2026-10-08 — Cristal con look propio (celeste)
- El sigilo `crystal` sigue siendo elemento Tierra (reacciones/fisica/danio intactos) pero usa su propio `ElementLook` (`Data/Elements/Crystal.asset`): esquirlas facetadas celestes (`LowPolyMeshes.Crystal`), motas brillantes, impactos con fragmentos y destellos, y estacas de cristal + escarcha celeste en el suelo (`GroundMarks`, `ElementLook.crystalline`).
- `SpellOutputController.LookFor(element, sigilId)` elige la variante; `MagicField.SigilId`. `ElementVfxSetup` (idempotente, menu VFX/Setup Cristal) crea el look y lo asigna en `SystemManagers.prefab`.

## 2026-10-08 — Traspaso del proyecto
- `Docs/ONBOARDING.md` (instalacion, primer arranque, verificacion con y sin Editor, MCP, builds, trampas), backlog priorizado al final de `ROADMAP.md`, `README`/`CLAUDE.md`/`STATUS`/`Home` actualizados y rutas absolutas quitadas de la documentacion.
- `ProjectSetup.FixBuildScenes`: las escenas del build eran `SampleScene` (inexistente); ahora `Pradera` (0) y `Escena`. Ajustes WebGL de `ProjectSettings.asset` versionados.
- Menu `SpellSimulator/Harness/Run Play Smoke`. Verificado en batch (sin el Editor abierto): compila y pasan los 161 tests EditMode.

## 2026-10-08 — Septima tanda (parte 3): mundo
- **Carteles:** el poste va detras del tablero (antes tapaba el texto del medio).
- **Zonas de practica:** piso casi plano (desnivel < 0,28 m a 2,5 m a la redonda, 8 sondas) y zonas prohibidas (campo de practica, colina de la torre, spawn); bajaron de 57 a 44 piezas. Tus ajustes manuales siguen aplicandose (`MapLayout`).
- **Estacion de agua:** pizarra chica fija en el muro sobre la pila (nada que sumergir); el cartel explica el chorro y que el agua borra la tinta.
- **Estacion de viento:** papeles y plumas mas livianos y con menos freno (masa 0,02/0,01, damping 0,7-0,8) y giran. Sin verificar en Play: la sonda `HarnessWindProbe` no pudo lanzar un hechizo valido sobre la pizarra rectangular (el harness dibuja en UV cuadrado).
- **Reconocimiento agua/viento:** evaluado y descartado el desempate por chamfer (KI-105).

## 2026-10-08 — WebGL: diagnostico y precision de shaders
- `WebGLBuild` con dos modos (menus `WebGL (diagnostico)`: Development + compilacion rapida, y `WebGL (itch.io)`: release Gzip). El diagnostico no mostro errores propios; el libro, los lienzos y el anillo de tinta del grimorio se ven bien en el navegador de prueba (build release tambien).
- Shaders de tinta, libro, calcomanias, marcas de suelo y particulas en `float` en lugar de `half` (GPUs con precision reducida en WebGL/movil). Hipotesis sin confirmar para lo que la usuaria ve invisible en itch.io (KI-104): pedir navegador, GPU y consola F12.

## 2026-10-08 — Septima tanda (parte 2): textos, creditos y tinta
- 'Taller' -> 'Simulador' en el menu; creditos en grilla de roles (Programacion, Arte, Diseño de juego, Diseño de interfaz, Musica y sonido, VFX) con cierre 'Hecho con Unity 6 / v0.1.0 · prototipo'.
- 'Estampa' -> 'Sello' en HUD, hints, grimorio y libros.
- Tinta visible: 'Tinta 72 %' en el contexto, barra fina en los slots con herramienta de tinta y hint del tintero ('Tintero 72 %', `[R] Recargar tinta`; `Inkwell.Infinite`).
- Cristal: VFX celestes propios (ver arriba). Regenerar el mapa conserva los 11 ajustes manuales (`MapLayout`). 161 tests y PlaySmoke en PASS.

## 2026-10-08 — Septima tanda (parte 1): sellos, calidad grafica y libros
- **Hechizos a sello:** boton 'Cargar en el sello' en el detalle de un hechizo (`Stamp.LoadSpell`, `StampPattern.Compound`); sellos con dibujo ~1,8x mas grande (`Stamp.PatternScale`, tope `MaxCanvasFill` 0,9 del lienzo).
- **Calidad grafica Baja/Media/Alta** en Opciones del grimorio (`GraphicsQuality`, `GraphicsQualityApplier`): escala de render, sombras, MSAA, contorno y particulas; default Alta en PC y Media en WebGL/Quest; persiste en PlayerPrefs.
- **Libros:** atril sin libro modelado detras (se veia doble), contorno mas marcado y dorado pulsante si no se leyeron; luz a la mitad.
- 161 tests en PASS. Falta regenerar el mapa para ver los atriles. Pendiente de la tanda: textos/creditos/tinta, mundo (viento, agua, carteles), reconocimiento agua/viento, cristal y WebGL.

## 2026-10-08 — Los ajustes manuales de la escena sobreviven a MapBuilder.Build()
- `Editor/Map/MapLayout.cs`: al empezar un Build compara la escena con la linea base (`Logs/MapBaseline.json`, al terminar el Build anterior); lo movido a mano (posicion/rotacion/escala locales) se guarda en `Data/MapOverrides.json` y se reaplica al terminar el Build (mismo camino jerarquico). Menus `SpellSimulator/Map/Capturar mi layout manual` y `Borrar overrides del layout`. Primera captura: 11 objetos (zonas Path8/Path9, GiantPaper_Field).
- Quedan fuera terreno, vegetacion y agua. Si un objeto movido a mano ya no existe tras un Build, su override se ignora.

## 2026-10-08 — Build de WebGL para itch.io
- `Editor/WebGLBuild.cs` (menu SpellSimulator/Build/WebGL (itch.io) o `WebGLBuild.Run()`): Gzip, sin hilos, memoria 256 MB-1 GB, solo `Pradera`; salida `Builds/WebGL` y resultado en `Logs/build-webgl.txt`. Build de 32 MB (~19 min la primera vez), `Builds/SpellSimulator-WebGL.zip` de 34 MB.
- `SentisGlyphClassifier` usa `BackendType.CPU` en WebGL (no hay compute shaders).
- Probado en el navegador con un servidor local con `Content-Encoding: gzip`: carga, muestra el menu y entra a Sandbox sin errores de consola. Sin probar dibujo/reconocimiento en la web ni rendimiento. Ver KI-103.

## 2026-10-08 — Volumenes de musica y efectos
- `AudioDirector` aplica los volumenes de Opciones del grimorio (KI-094) y refresca la musica y el viento al instante.

## 2026-10-08 — Sexta tanda, Fase 3: menu principal y VFX de elementos
- **Menu principal** (`MainMenu`, UI Toolkit, en Pradera): Sandbox, Duelo (proximamente), Informacion (grimorio a pantalla completa), Creditos (Tamara D'Angelo) y Salir; camara orbitando la torre; en VR se entra directo. `PlayerInputReader.Gate` (Full/BookOnly/None) congela el input mientras esta abierto. Se vuelve desde Opciones del grimorio ("Menu principal").
- **VFX:** tierra rehecha (esquirlas y estacas low-poly que se desmoronan, polvo y fragmentos al impactar), agua (gotas azules translucidas, espuma y spray), viento (estelas curvas y hojas en vez de humo gris), objetos prendidos emiten desde su malla. Costo medido: agua ~520, tierra ~315-370, viento ~210 particulas. `ElementVfx`, `LowPolyMeshes`, `ElementVfxSetup` (menu SpellSimulator/VFX).
- **Harness:** `HarnessTestRunner` y `HarnessPlaySmoke` guardan la escena antes de empezar (evita el cartel de "guardar escena" que colgaba el MCP).
- 154 tests y PlaySmoke en PASS. Sin probar a mano; ver KI-100 a KI-102.

## 2026-10-08 — Sexta tanda, Fase 2: torre, puerta y apartado Hechizos
- **Torre rediseñada** (Opus): piedra y madera calidas, alfombras, vigas, faroles, cortinas, ventanas y banderas; recorrido puerta-atril; **estacion de viento** (tablero, papeles, plumas, banderines) y **estacion de agua** (pila con agua real y barquito) con carteles; sin luz violeta. `SaveMesh` reescribe la malla por la API (la copia dejaba mallas viejas en pantalla).
- **Puerta interactuable** (`TowerDoor`, E abre/cierra, arranca abierta, el reinicio la deja abierta) y el hint de E se actualiza si cambia el texto.
- **Apartado Hechizos** del grimorio (menu I-VI): 7 hechizos como data (`Data/Spells`), 2 iniciales (Rafaga, Chorro de agua) y 2 libros nuevos (`spells-wind-water`, `spells-fire-earth`); `SpellDef/SpellStrokeBuilder/SpellCatalog`. Desvio del ADR-034: el JSON guarda la composicion y los trazos se arman desde el diccionario.
- 151 tests y PlaySmoke (con chequeos de la puerta) en PASS. Sin probar a mano.

## 2026-10-08 — Ajustes tras la prueba: outlines, circulo inicial, zonas de practica y spawn
- **Bug del generador:** la plantilla `PaperCanvas` habia desaparecido de `Pradera.unity` y `CanvasDesigns`, las zonas de practica y los props se omitian en silencio al regenerar; `MapBuilder.EnsureCanvasTemplate` la rehace desde `Paper_S`. Ahora hay 57 piezas en `Practice` y `Props_Field`.
- Outline mas leve en herramientas (1,8 mm) y mas fuerte en libros (7,5 mm). `magic_circle` se conoce desde el inicio (5 glifos iniciales).
- El jugador nace dentro de la torre, y el atril con el libro de los cuatro elementos esta junto a la puerta.

## 2026-10-08 — Sexta tanda: ajustes de la Fase 1 y primera parte de la Fase 2
- **Vista completa:** V sobre lo mirado (`SurfaceFocus`: lienzos enteros, ambiente = zona de 1,2 m, nunca el suelo), ESC la cierra, correccion del desfase en objetos chicos (UV por interseccion con el plano real, centrado en la superficie), hints `E / V / R` junto a la mira.
- **Libros y herramientas:** luz mas tenue (~35 %) y contorno propio `OutlineHighlight` (casco invertido, shader en `Resources/`); "Circulo Magico" primero en Glifos con boton doble.
- **Movibles:** masa realista y esfuerzo segun peso (`Movable`, `MovablesSetup`); sillas de la torre, props nuevos y 10 zonas de practica (lago y senderos, `CanvasDesigns.Practice`).
- **Plan:** ADR-034 (guardar hechizos) y fases H1-H5 en ROADMAP. Pendiente de Fase 2: torre (Opus), spawn, puerta, apartado Hechizos; Fase 3: menu principal y VFX.
- 143 tests y PlaySmoke en PASS. Sin probar a mano.

## 2026-10-08 — Sexta tanda, Fase 1: controles, grimorio, libros y HUD
- **Controles:** la rueda solo cambia grosor (pincel) o distancia; paginas con Z/X; C = agacharse (toque) / tumbarse (mantener); zoom con Alt izq.
- **Grimorio:** TAB visible (4.o slot fijo a la izquierda de la hotbar), ESC lo abre/cierra, ya no oculta la HUD fuera del modo fijo (B); layout de controles arreglado.
- **Sigilos del libro:** textura 1024, trazo fino, remuestreo por arco y 2,5 s fijos (`GrimoireStrokeMath` + 2 tests).
- **Aprendizaje:** se empieza solo con fuego, agua, viento y tierra; `levitation` pasa al libro "motion", `column` y el nuevo `magic_circle` al libro "shape". El libro de elementos explica Fuego.
- **Libros:** luz pulsante, "Libro X de Y" y cuantos faltan. **Sellos:** copian solo el simbolo (sin circulo); glifo "Circulo Magico" (`magic_circle`, solo anillo abierto).
- **HUD:** contexto en dos lineas sin cortes, mayusculas, sin rotulos de mano, mas margen arriba, glow sin recorte.
- 143 tests EditMode y PlaySmoke en PASS. Sin probar a mano.

## 2026-10-08 — Plan de la sexta tanda
- Feedback de la prueba manual desglosado en `Docs/PLAN_SEXTA_TANDA.md`: 3 fases con reparto de agentes (Haiku/Sonnet/Opus) y decisiones por confirmar (teclas Z/X, slot del grimorio). Sin cambios de codigo.

## 2026-10-07 — Quinta prueba manual: UI Toolkit, inventario de dos manos, grimorio nuevo, contornos y torre
- **Inventario (ADR-033):** barra de 3 slots (1-2-3) en la mano principal (derecha/izquierda en Opciones) + mano secundaria que arrastra lo
  agarrado con E (`PlayerInteractor` reescrito, `ObjectMover` con cinematicos conducidos y volteo). Rueda = grosor (pluma/goma) o pagina
  (cuaderno) o distancia. Clic de la rueda deja flotando. Cuadernos de 10 paginas (`NotebookPages`).
- **HUD UI Toolkit** (`Scripts/UI`, `Assets/_Project/UI`): barra con miniaturas 3D, contexto con keycaps, avisos compactos arriba a la derecha,
  [E] junto a la mira, lector de libros literal (`BookReaderUI`) con "Aprender este conocimiento". Se oculta con el grimorio abierto.
- **Grimorio** rehecho: se agarra de cualquier parte, secciones Magia/Glifos/Sellos/Controles/Opciones (mano principal, sensibilidad,
  invertir Y, volumenes), estetica de cuero violeta y pergamino procedural.
- **Render:** contorno cartoon por pantalla (RenderGraph) y rim cel en `StylizedLit`; piso quemado organico; llamas del pasto bajas.
- **Mundo:** torre sin luces violetas ni lienzos flotantes, piso 2 amoblado, columnas sobre piso firme, herramientas dentro de las mesas;
  reinicio repone todo y rescata objetos bajo el terreno.
- Trabajo repartido: 6 agentes (Opus: UI, grimorio, render; Haiku: cuadernos, torre, reinicio). 141 tests. KI-084 a KI-095.
- Pendiente: probar a mano; cablear volumenes de musica/efectos (KI-094); Quest (KI-085, KI-095).

## 2026-10-07 — Cuarta prueba manual: HUD tipo Minecraft, tinta lisa, agarre unificado (SIN VERIFICAR EN PLAY)
- Tinta lisa: shader `InkBrush` (Min/Max) sin circulos apilados (verificado con captura); lienzos a 1024 px/m (tope 2048); 6 grosores en teclas 5-0 (1,6 mm a 8,8 cm).
- HUD (`PlayerHud`, `Hud`, `IHudInfo`, `IWidthSelectable`): barra de 4 slots (1-2 mano derecha, 3-4 izquierda), barra de contexto con teclas
  de uso, fila de grosores y barra de tinta; avisos arriba a la derecha (permanentes para "sin tinta"). Compila; no se vio en Play.
- Interactor con slots por mano, E sostiene a distancia lo movible (ObjectMover.TryBegin/End), clic de la rueda deja flotando, se quito la
  rueda de distancia de las herramientas, estampas se ven chicas en la mano, agacharse mas bajo.
- Pendiente: probar todo en Play, tests 131 (pasaban antes de los ultimos cambios), build, docs de controles (GAME_DESIGN aun dice G/1-4 grosor).

## 2026-10-07 — Tercera prueba manual: mover objetos, controles, fuego contenido y más
- **Mover objetos (G)** estilo Garry's Mod (`ObjectMover`, `IRemoteGrabbable`): rueda, clic derecho + mouse para rotar, Shift + rueda, T para
  dejarlo flotando. Las manos de PC cuelgan de la cámara y las herramientas se sostienen inclinadas (pluma/pincel/goma/trapo/estampa).
- **Zoom (X)**, **grosor de la pluma (1–4)**, **tumbarse (Z)** ya existía; esquema de controles reordenado (ADR-032) y documentado en el
  grimorio. Panel de depuración oculto (F3) y avisos como toasts. V = lienzo, B = grimorio a pantalla.
- **Fuego contenido** (`GrassFire`: decaimiento por generación; árboles más difíciles) y **reinicio total** (lienzos, flotantes, estampas).
- **Sellos chicos** se leen mejor (agrandado alrededor del centro), **estampas XL y XXL**, **lienzos gigantes**, caballete corregido,
  sin avisos sobre los botones del grimorio. KI-076 a KI-083.
- Trabajo repartido con agentes Haiku (GrassFire, ObjectMover/zoom, HUD/depuración); corregí un error de compilación suyo.

## 2026-10-07 — Grimorio con clic y estampas arregladas
- Los botones del grimorio se aprietan con **clic izquierdo** (`IPressable.PressWithClick`, `PlayerInteractor.HandleClickPress`); el aviso dice "[Clic]".
- Estampas: botón **"Cargar en la estampa"** en el detalle de cada glifo (`Stamp.Held`/`Stamp.Load`); estampan también sobre el piso y
  objetos (`AimAtCanvas`), con una calcomanía a su medida (`Inscribable.CreateDecal(.., sizeOverride)`); causa del fallo en el piso: el
  anillo de 10 cm quedaba bajo el radio mínimo relativo (6 %) de una calcomanía de 3,4 m. Calcomanías grandes bajan a 2,4/2/1,8 m.
- Un botón delante ya no permite dibujar o estampar en lo que tiene detrás. KI-074, KI-075.

## 2026-10-07 — M6: sellos partidos (SplitJoin)
- `SplitJoinMath` (puro): `Merge` fusiona dos sesiones UV por el ancho de cada mitad; `IsRightNeighbour` decide si B está pegada a la
  derecha de A (tolerancia 8 cm, misma cara). `SplitSealHalf`: al estar pegadas 0,4 s crea un lienzo temporal del tamaño de las dos,
  lanza con `SpellCaster.Cast` y borra las mitades; sola nunca lanza (`ISealTrigger`).
- Dos mitades de piedra junto a las piedras de impacto (`MapContraptions`). 5 tests nuevos (131). Verificado en Play: fuego válido.
- KI-059 resuelto, KI-073 abierto.

## 2026-10-07 — Segunda prueba manual: interacción, grimorio-menú, tumbarse y fuego en el mundo
- **Causa raíz de varios fallos:** la máscara del rayo de E solo veía las capas Canvas/Interactable; libro, estampas y cajas no
  se podían apuntar (`InteractionSetup.FixInteractMask`). Agarrar ahora intercambia de mano.
- **Lienzos:** soltar = caer (cara arriba); **G** = flotar con vaivén suave (`IPinnable`, `PhysicalCanvas.Pin`); caballetes y mesa de
  dibujo bien orientados; papeles/cuadernos con gravedad (no flotan sobre las mesas).
- **Tumbarse (Z)** además de agacharse (`PlayerMovement`, `stanceT`).
- **Objetos grandes:** `Movable` hasta 600 kg, distancia y resorte según tamaño.
- **Grimorio como menú** (`GrimoireScreen`): índice de glifos, detalle, controles, sellos, opciones; no se cierra al alejarse; V = fijo
  a cámara con clic; los 8 sigilos y 4 signos empiezan descubiertos.
- **Tinta del entorno** oscura y calcomanías más grandes (3,4 m en el piso): ya no se cortan los sellos grandes.
- **Mundo inflamable:** pinos y mobiliario reactivos (`FlammableSetup`, perfil Arbol) y `GrassFire` (grilla de pasto con propagación,
  quemado instanciado y llamas con pool). ADR-030/031, KI-066 a KI-072.
- Trabajo repartido: agente Haiku para PlayerMovement (tumbarse), Movable/Interactor, Grimorio y GrassFire; yo, editor y verificación.

## 2026-10-07 — Prueba manual: movilidad, lienzos, entorno, torre y herramientas
- **Cielo:** la cámara de PC usaba un color liso marrón; ahora Skybox (`MapBuilder.SetupLighting`).
- **Movimiento:** correr (Shift), agacharse (Ctrl/C, con chequeo de altura libre) y saltar; velocidades caminar 4,2 / correr 7,4 / agachado 2.
- **Objetos:** `Movable` (resorte hacia la mano, sin empujar al jugador), rueda para acercar/alejar, Q arrojar (`IThrowable`),
  E ignora lo que llevás y los triggers inútiles (`PickHit`); `Movable` en todo lo reactivo.
- **Lienzos:** `PhysicalCanvas` reescrito: suelta = apoya (mesa/piso/pared) o flota; arrojado aterriza cara arriba; F da vuelta
  la hoja (`IFlippable`); V fija a la cámara con mouse como puntero (`FocusMode`, `IFocusable`). Emisión `InkSide` en todos
  (`DrawingCanvas.NoteInkViewer`/`FaceNormal`). Cuadernos, lienzos grandes con marco, pergaminos flotantes, pizarra, caballetes y
  mesa de dibujo (`CanvasDesigns`). Corregida la orientación de piedras de impacto y placa (emitían hacia abajo).
- **Entorno:** `Inscribable` + `InscribedDecal` + shader `InkDecal`: se dibuja sobre el piso, la torre y las cajas; la calcomanía
  sigue al objeto y se rompe con cráter/quemadura/charco (menos los de su propio hechizo), agua, fuego o fractura.
- **Grimorio:** flechas y rueda pasan página, se puede llevar (agarradera inferior) y queda flotando; páginas de Controles y Sellos.
- **Torre:** mesas de trabajo con herramientas, cajas y baúles; **modelos nuevos** de pluma, pincel, goma, trapo y tintero (`ToolModels`).
- **Audio:** salpicadura, chorro de agua y cascada resintetizados (burbujas con subida de tono, murmullo grave y ruido con variación lenta).
- Docs: ADR-027/028/029, KI-060 a KI-065. 126 tests (sin cambios), build y smoke test OK.

## 2026-10-07 — M6: sellos en objetos (impacto y presión)
- `SealPhase.Armed`: con un `SealTrigger` en el objeto, cerrar el anillo arma el sello en vez de lanzarlo
  (`SealActivation.Evaluate` con `deferred`, `ShouldFire`). `SpellCaster.Trigger(canvas)` lo lanza; `ISealTrigger` evita
  que Casting dependa de World.
- `SealTrigger` (Impact por velocidad relativa, Pressure por masa apoyada) y `PhysicalCanvas.settleFlat`.
- `MapContraptions`: 2 piedras de impacto, placa de presión y pesa junto a la mesa de estampas.
- 3 tests de `SealActivation` (125 en total). Verificado en Play: ambos quedan Armed y se lanzan al golpe y al peso.
- Pendiente: `SplitJoin` (KI-059).
- Sello permanente: `SealTrigger.SetPermanent(usos, enfriamiento)` + `SpellCaster.Rearm` / `SealActivation.Rearm`; la placa
  tiene 5 usos y 3 s. Los usos se reinician si el papel se rompe. 126 tests.

## 2026-10-07 — M6: estampas
- `StampPattern` (puro): sigilo + anillo abierto del diámetro de la estampa; signo del tamaño del anillo existente,
  orientado hacia su centro. `Stamp`: se agarra, se carga del grimorio abierto (`Grimoire.CurrentGlyph`, 5 usos) y
  estampa sobre papeles; etiqueta flotante con el glifo y las cargas (`FaceCamera`). Sonidos de estampar y cargar.
- Estampas S/M/L en una mesa del campo + una M en el escritorio de la biblioteca (`MapDetails.Stamps`).
- 3 tests con el modelo (`StampTests`). Verificado en Play: grimorio → cargar → estampar → sello preparado. 122 tests.
- Harness: el asmdef de Editor necesitaba `Unity.TextMeshPro`; nota sobre errores que no llegan a la consola.

## 2026-10-07 — Arreglos: entrar a la torre, moverse y dorso de los lienzos
- KI-056: zócalo de la torre cortado en la puerta (se entra caminando).
- KI-057: `PlayerMovement` sube escalones bajos y no resbala quieto en rampas; cápsula del jugador más angosta y sin
  fricción; muebles fuera del camino de la puerta a la rampa. Verificado: cápsula pasa por la puerta y llega a la rampa.
- KI-058: lienzos visibles por detrás.

## 2026-10-07 — M6: grimorio, libros y descubrimiento
- Nuevo módulo `Learning`: `GlyphLore` (textos en `Data/Lore/glifos.json`), `DiscoveryState` (puro), `Discovery`
  (guardado local + descubrir por experimentación), `LibraryBook`, `Grimoire` (libro-menú que flota frente al jugador,
  glifo que se dibuja solo, explicación, opciones) y `GrimoireButton`.
- `IPressable` + `PlayerInteractor.HoverPrompt` + `InteractionPromptUI`: acciones instantáneas con la tecla de agarrar
  y texto de ayuda bajo la mira, avisos "Descubriste: …". Tab / Y / menú izquierdo abre el grimorio.
- 5 libros en la torre. Sonidos de libro y de descubrimiento. "Reiniciar el mundo" desde el grimorio.
- Verificado en Play: grimorio abre y dibuja el glifo; leer *Los cuatro elementos* pasa de 3 a 6 glifos. 119 tests.
  ADR-026. KI-055 arreglado (textos de ayuda sin GC).

## 2026-10-07 — Arreglos de la prueba manual de M5
- KI-052: rampas de la torre con 3 m de altura libre (antes 2.0 m: no se podía subir al segundo piso). Estantes del
  estudio reubicados fuera del hueco nuevo.
- KI-053: las hojas soltadas caen y se acuestan (`PhysicalCanvas`: gravedad al soltar, frenado del aire, torque de
  asentado). Los papeles fijos (pared exterior y escritorio) no se pueden agarrar.

## 2026-10-07 — M5: audio
- Nuevo módulo `Audio`: `AudioLibrary` (37 cues como datos), `AudioDirector` (pool de 24 fuentes, loops por objeto,
  música y viento de fondo, pájaros al azar), `GameAudio` (eventos → sonidos), `AmbientEmitter` (cascada),
  `FootstepAudio` (pasto/madera/piedra), `CollisionAudio` (golpes de cajas, piedras y muñecos).
- Eventos nuevos para el audio en `PhysicalTool`, `ElementReactive`, `WaterVolume`, `GroundMarks`, `SealBreaker`,
  `Health` y `MagicWorld` (nadie depende del audio).
- 29 sonidos sintetizados con `Tools/Audio/synth_audio.py` + 281 de Kenney CC0 (RPG Audio, Impact Sounds, Interface
  Sounds; descargados con permiso de Tamara). `AudioImportRules` (Vorbis mono).
- Verificado en Play: música, viento, cascada, lanzamiento y 10 loops de fuego sobre 10 objetos ardiendo. 116 tests.

## 2026-10-07 — M5: cascada, pasto, detalles de la torre, papeles y post-proceso
- Cascada (`Waterfall.shader`: vetas que caen, espuma) que sigue la ladera real + arroyo con `WaterVolume` hasta el
  lago, salpicaduras y bruma al pie. `ValleyShape`: cauce del arroyo (+1 test).
- Pasto y flores agrupados en hasta 36 mallas (sin colliders ni sombras). Puerta de tablones abierta, velas con llama, motas
  mágicas en biblioteca y estudio, atril con libro abierto.
- Papeles: fijo en la pared exterior de la torre (emite hacia el campo), chico y mediano agarrables en la mesa de lectura.
- Post-proceso global (bloom, tonemapping neutral, saturación/contraste, viñeta) y cámaras con post-proceso.
- Verificado en Play: partículas vivas, papeles en su lugar, el papel de la pared lanza hacia el campo. 114 tests.
  Mapa: ~76k triángulos.

## 2026-10-07 — M5: blockout del mapa (Pradera)
- Nueva escena `Pradera.unity` (build index 0) generada por `MapBuilder`: terreno low-poly de 208 m con montañas, lago
  con `WaterVolume`, ~250 pinos, 45 rocas, caminos, campo de práctica con mesa de herramientas, y la **torre**
  (`TowerBuilder`): muro de piedra con puerta y ventanas, 3 pisos con rampas en espiral y barandas, almenas, techo cónico
  violeta con aro dorado, biblioteca y estudio con estantes, libros, mesas, sillas y luces cálidas/violetas.
- Shader `StylizedLit`: color por vértice, luz en bandas, sombras de URP, luces puntuales, niebla. Cielo procedural,
  ambiente trilight y niebla celeste.
- `ValleyShape` (puro) + 5 tests. `Escena.unity` queda como laboratorio. ADR-024. 113 tests.

## 2026-10-07 — M4: suelo y muñecos de práctica
- Suelo (`GroundReactions` + `GroundMarks`): donde un hechizo toca el piso deja cráter con 3-5 rocas que emergen
  (tierra; las rocas son piedra y las atrae el pull de tierra), quemadura (fuego) o charco que se seca en 20 s (agua).
  Shader `GroundMark` (ADR-023). Presupuestos: 24 marcas, 30 rocas. El reinicio las borra.
- Nuevo módulo `Combat`: `Health`, `DamageTable` (valores del Duelo), `DamageModel` (puro, tests), `ImpactDamage`,
  números de daño flotantes. `MagicWorld` aplica el daño de los hechizos y la quemadura.
- `TrainingDummy`: muñeco de tela sobre un poste con resorte (se balancea), arde, se rompe y reaparece a los 4 s.
  Dos en la escena: detrás de los blancos y a la izquierda.
- Verificado en Play Mode: tierra al piso → cráter + 4 rocas; fuego al muñeco → 26 de daño, arde, se consume y
  reaparece con 100. 108 tests.

## 2026-10-07 — M4: agua y pull canónico
- `StylizedWater.shader`: olas suaves por vértice y hasta 16 ondas de impacto calculadas en el fragmento, con espuma
  (sin render textures: apto Quest).
- `WaterVolume`: flotación según el material (`MaterialProfile.buoyancy`: madera flota, piedra se hunde), arrastre,
  salpicaduras al caer, ondas cuando un hechizo toca el agua, **vapor** si es fuego y **chorro de agua** hacia el sello si
  es agua con pull. Lo sumergido se moja y se apaga (`MagicWorld`).
- KI-037: el pull ya no atrae cualquier objeto, solo materia de su elemento (`IElementMatter`: piedra = tierra, lo que
  arde = fuego). `MaterialProfile.element`.
- Escena: estanque de piedra a la derecha del papel (4×4 m) con dos cajitas que flotan y una piedra que se hunde; una
  piedra suelta para probar el pull de tierra. Verificado en Play Mode con capturas.

## 2026-10-07 — M4: el mundo reacciona (núcleo)
- Nuevo módulo `Reactions`: `ReactionModel` (reglas puras), `MaterialProfile` (Madera, Papel, Piedra, Tela),
  `ReactionTable` (elemento × material como datos), `ElementReactive`, `MagicWorld` (ticks a 15 Hz), `BurnEffect`,
  `Shatter` (fractura por grilla), `WorldResetter`.
- El fuego enciende la madera, se propaga a los vecinos, carboniza y la consume hasta romperla; el agua apaga y moja
  (lo mojado no prende hasta secarse); el viento aviva lo que arde y enfría lo que no; la tierra rompe la madera y
  no la piedra. `MagicField.ExposureAt` usa la misma geometría que la física.
- `SealBreaker`: si el papel se empapa o se prende fuego, el sello se pierde (canon). El hechizo no afecta a su propio papel.
- Reinicio del mundo: Backspace / Select / Y izquierdo. Escena: 21 objetos de madera/piedra + papel reactivos.
- Harness: `HarnessSeals` dibuja y lanza sellos de plantilla. Verificado en Play Mode (fuego → 9/9 blancos ardiendo,
  tierra → 7 rotos, reinicio → 22/22 sanos). Arreglados KI-045 y KI-046. 103 tests.
- Docs: ROADMAP, ARCHITECTURE, STRUCTURE, DECISIONS (ADR-022), KNOWN_ISSUES, GAME_DESIGN, MAGIC_SYSTEM, HARNESS.

## 2026-10-07 — M2: pincel y lectura del sello sobre el papel
- `ToolDefinition` (SO) para cada herramienta; `PhysicalTool` lo lee. Assets en `Data/Tools`: Lápiz (4 cm),
  **Pincel** (10 cm, 20 tinta/m, 150 de capacidad) — nuevo objeto `Brush` en la escena —, Goma y Trapo.
- `Stroke.Width` (metros): cada trazo se repinta con su ancho. El espaciado de sellos depende del ancho (antes un trazo
  rápido o ancho quedaba punteado).
- `SealAnnotations`: tras cada lectura escribe sobre el papel el nombre y la confianza de cada glifo (verde sigilo,
  azul signo, naranja ambiguo, rojo desconocido), una flecha por signo y las advertencias debajo. Se borran al tocar
  el lienzo. Nuevo evento `SpellCaster.SealAnalyzed` (AST + hechizo).
- 91 tests. Docs: ROADMAP, STRUCTURE, ARCHITECTURE, SYSTEMS_DESIGN.

## 2026-10-07 — Primeras fixtures reales y sigilo sin partes sueltas
- Dos sellos grabados por Tamara (mismo dibujo, uno prolijo y uno a propósito desprolijo) pasan a `Tests/Fixtures`.
- El desprolijo fallaba porque una columna cercana se fusionaba con el fuego (confianza 0.43). Nueva hipótesis del
  `SealReader`: separar del sigilo las partes que no lo tocan y leerlas como signos. Ahora lanza fuego.
- Descubierto KI-042: columna con barra curva/separada se lee como `region`. Convención aclarada: la columna apunta
  desde la barra corta hacia la punta del palito.
- 90 tests. Docs: KNOWN_ISSUES, HARNESS, ARCHITECTURE.

## 2026-10-07 — Grabación de sellos y lectura de signos en el panel
- La prueba manual de M3 mostró columnas leídas hacia afuera (abanico) y una columna leída como `region`, sin forma de
  saber qué se dibujó. Ahora `SealRecord` guarda cada sello lanzado (trazos crudos + resultado) en
  `persistentDataPath/Sellos` y `SealFixtureTests` los reproduce como tests.
- El panel lista cada signo: lado del anillo, hacia dónde apunta (adentro/afuera/horario/antihorario) y el giro en grados.
- Abanico más fino (partículas 45%, casi sin flotación): ya no tapa la cámara.
- Docs: HARNESS, STRUCTURE, ARCHITECTURE, ROADMAP. 89 tests (1 ignorado sin fixtures). Build actualizada.

## 2026-10-07 — M3: el hechizo tiene forma y fuerza
- **Pose**: el clasificador lee la salida `angle`, se calibra con las plantillas y da la rotación de cada glifo.
- **Plan** (port de `reading/` + `plan/`): ley vectorial de columnas (chorro, abanico, vórtice), levitación (orbe),
  pull (succión), convergencia (lente), dispersión (fuga), spin-up R‑21. Zona muerta + umbral relativo contra el ruido.
- **Manifestación 3D**: `SpellManifestation` con una pista de partículas por primitiva, tiempos R‑01, en el centro del
  anillo y con el radio real del sello. `ElementLook` × 5 y shader `SpellParticle` (Shuriken, apto Quest).
- **Física**: `MagicField` por arrastre al flujo (empuja cajas y al jugador); `Respawnable` para lo que cae del mundo.
- Papeles emiten hacia el lado opuesto a quien mira (ADR-020). Panel de diagnóstico muestra el plan.
- Escena: plataforma con 9 blancos, cajas y una pesada. Harness: `HarnessCapture` + galería de primitivas.
- 86 tests (pose y leyes del plan). Build de Windows actualizada.

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
