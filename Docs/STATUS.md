---
tags: [doc, estado]
---
# Estado actual (leer primero)

> Una pagina para retomar rapido. Actualizar al cerrar cada hito; el detalle va en CHANGELOG y KNOWN_ISSUES.

**Hito:** modo Libre con el plan de la octava tanda COMPLETO (bloques 1-6) y **F0 del multijugador hecho** (preparacion sin red). **F1 (sesion y conexion) casi completa:** Netcode for GameObjects 2.13 + Relay por WebSocket, menu Multijugador, avatar de red y bots de prueba; Ale y Tami la probaron por Internet. **F2 (mundo compartido: objetos fisicos y agarre) hecha y verificada con bots**, tambien con 150 ms simulados. Falta de F1 lo no importante (avatar mas fino, prueba PC-Quest). Siguiente: **F3 (dibujo compartido y hechizos replicados)** segun [MULTIPLAYER_PLAN](MULTIPLAYER_PLAN.md); antes, que Ale pruebe F2 a mano con dos personas.
**Tests (2026-10-09):** 428 EditMode (`HarnessTestRunner`) + smoke de Play (`HarnessPlaySmoke`) + matriz de lienzos (`HarnessCanvasMatrix`, 425 chequeos; modos `Only=` RUNE, FOCUSRAY, CHARGE, SPELLS, RUNEJOIN, HALVES, NESTED, PORTAL) + entrada real (`HarnessRealInput`, 30) + red (`HarnessNetSmoke`: local 15, Relay 7, menu 6, desconexion 13) + bots (`HarnessBots`: local, Relay, churn y agarre con/sin 150 ms; ver [HARNESS](HARNESS.md#bots-de-red)). Todo en verde. Ale probo a mano toda la checklist de PC de [TESTING](TESTING.md): en orden. VR/Quest sigue sin probarse (M7).
**Escenas:** `Pradera.unity` (mapa, indice 0; las posiciones movidas a mano se conservan en `Data/MapOverrides.json`) y `Escena.unity` (laboratorio).

**Hecho en la octava tanda ([PLAN_OCTAVA_TANDA](PLAN_OCTAVA_TANDA.md)):** controles nuevos (E agarra/deja en el aire, T grimorio, Esc pantalla+pausa, Tab vista completa), tinta por metro normalizada + cartuchos + clases de tinta + cinturon (V cambia), retardo de lanzamiento con brillo, hechizos con fisica (roca, cristal, agua, viento), runas por material con desgaste y fusion, mitades de cualquier lienzo, circulos anidados, estructuras y 4 portales circulares con identidad de 3 runas, reposicion y reciclaje, 19 hechizos prearmados con descubrimiento, menu de pausa dentro del grimorio.
**Siguiente:** que Ale pruebe F2 a mano; F3 (dibujo compartido y hechizos replicados); comprobar en el build WebGL real que la conexion sea `wss`. Contenido diferido del modo Libre: crear y guardar hechizos propios, aprender identidades de portal en libros, colocar los 3 libros nuevos y mostrar las pistas de hechizos en el grimorio. F0 incompleto: KI-118.

## Mapa mental rapido
- Pipeline: `PlayerInteractor`/`PhysicalTool` -> `DrawingCanvas` -> `SpellCaster.Cast` -> `SpellOutputController` -> `MagicField`/`MagicWorld`.
- Inventario: `PlayerInventory` (estado puro, 3 slots + cartuchos de tinta por clase con cinturon) + `PlayerInteractor` (entrada, partials `.Grab` y `.Hints`) + `ObjectMover`/`Holdable` (ADR-033). HUD en `Scripts/UI`.
- Multijugador (preparacion F0): `Core/PlayerRegistry` (en vez de `Camera.main`), `Core/GameClock` (reloj de reglas), `Stroke.OwnerId/StrokeId` + `StrokeCodec`. Los eventos de `SpellCaster` son de instancia.
- Mundo vivo: `SupplyPoint` + `RecyclingStation` + `WorldObjectBudget`, `Portal` (identidad de 3 runas, rutas de 30 s, cruce por trigger), `MapStructures`.
- Mundo: se genera TODO desde `Editor/Map/MapBuilder.Build()` (no editar la escena a mano). Setups: `InteractionSetup`, `FlammableSetup`, `UISetup`.
- Fuego: `GrassFire` (grilla de pasto) + `ElementReactive` (objetos). Reglas de reaccion: `ReactionTable`, `MagicWorld`.

## Controles
Fuente de verdad: `GAME_DESIGN.md#controles` (E agarra / deja en el aire, Q arroja, T grimorio, Esc pausa a pantalla, Tab vista completa, F da vuelta, R recarga, V cambia tinta). Se repite en el texto del grimorio (`Learning/GrimoireTexts.cs`) y en `PlayerInputReader`:
si cambian, tocar los tres (pendiente: generar los textos desde un solo dato).

## Abierto (resumen; detalle en KNOWN_ISSUES.md)
Diferidos: KI-106..109, 114, 117, 118 | Volumenes de musica/efectos sin cablear (KI-094) | UI y grimorio solo PC, no VR (KI-095, KI-054) | Quest sin medir (KI-049, KI-085) |
efectos de 6 glifos | fuente serif (KI-093).

## Reglas de trabajo baratas (aprendidas)
1. Un tema por pedido; los rediseños empiezan con un plan de media pagina aprobado.
2. Antes de implementar, correr los tests; despues, `HarnessPlaySmoke`. Un chequeo nuevo por cada sistema nuevo.
3. Delegar a agentes solo con especificacion cerrada y archivos propios; los agentes Opus cuestan ~250k tokens cada uno.
4. Docs obligatorios por commit: CHANGELOG y, si aplica, KNOWN_ISSUES. El resto, solo si cambia lo que describe.
