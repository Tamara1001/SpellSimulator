---
tags: [doc, estado]
---
# Estado actual (leer primero)

> Una pagina para retomar rapido. Actualizar al cerrar cada hito; el detalle va en CHANGELOG y KNOWN_ISSUES.

**Hito:** modo Libre con el plan de la octava tanda COMPLETO (bloques 1-6) y **F0 del multijugador hecho** (preparacion sin red). Siguiente: **F1 (sesion y conexion con Netcode for GameObjects + Relay por WebSocket)** segun [MULTIPLAYER_PLAN](MULTIPLAYER_PLAN.md).
**Tests (2026-10-09):** 409 EditMode (`HarnessTestRunner`) + smoke de Play (`HarnessPlaySmoke`) + matriz de lienzos (`HarnessCanvasMatrix`, 425 chequeos; modos `Only=` RUNE, FOCUSRAY, CHARGE, SPELLS, RUNEJOIN, HALVES, NESTED, PORTAL) + entrada real (`HarnessRealInput`, 30). Todo en verde. Ale probo a mano toda la checklist de PC de [TESTING](TESTING.md): en orden. VR/Quest sigue sin probarse (M7).
**Escenas:** `Pradera.unity` (mapa, indice 0; las posiciones movidas a mano se conservan en `Data/MapOverrides.json`) y `Escena.unity` (laboratorio).

**Hecho en la octava tanda ([PLAN_OCTAVA_TANDA](PLAN_OCTAVA_TANDA.md)):** controles nuevos (E agarra/deja en el aire, T grimorio, Esc pantalla+pausa, Tab vista completa), tinta por metro normalizada + cartuchos + clases de tinta + cinturon (V cambia), retardo de lanzamiento con brillo, hechizos con fisica (roca, cristal, agua, viento), runas por material con desgaste y fusion, mitades de cualquier lienzo, circulos anidados, estructuras y 4 portales circulares con identidad de 3 runas, reposicion y reciclaje, 19 hechizos prearmados con descubrimiento, menu de pausa dentro del grimorio.
**Siguiente:** F1 del multijugador (KI-104, WebGL, ya resuelto); verificar Relay sobre WebSocket (WebGL esta DENTRO del alcance). Contenido diferido del modo Libre: crear y guardar hechizos propios, aprender identidades de portal en libros, colocar los 3 libros nuevos y mostrar las pistas de hechizos en el grimorio. F0 incompleto: KI-118.

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
