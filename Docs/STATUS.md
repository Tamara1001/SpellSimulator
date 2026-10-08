---
tags: [doc, estado]
---
# Estado actual (leer primero)

> Una pagina para retomar rapido. Actualizar al cerrar cada hito; el detalle va en CHANGELOG y KNOWN_ISSUES.

**Hito:** M6 completo (aprendizaje, estampas, sellos en objetos y partidos, mundo inflamable, inventario de dos manos, HUD UI Toolkit,
grimorio nuevo, contornos). Falta M7: build de Quest, medicion de rendimiento, tutorial, tests PlayMode ampliados.
**Tests:** 141 EditMode (`HarnessTestRunner`) + test de humo de Play (`HarnessPlaySmoke`, ver HARNESS.md).
**Escenas:** `Pradera.unity` (mapa, indice 0) y `Escena.unity` (laboratorio, sin HUD nueva ni UI Toolkit).

## Mapa mental rapido
- Pipeline: `PlayerInteractor`/`PhysicalTool` -> `DrawingCanvas` -> `SpellCaster.Cast` -> `SpellOutputController` -> `MagicField`/`MagicWorld`.
- Inventario: 3 slots de mano principal + 1 secundaria arrastrada (`PlayerInteractor`, `ObjectMover`, ADR-033). HUD en `Scripts/UI`.
- Mundo: se genera TODO desde `Editor/Map/MapBuilder.Build()` (no editar la escena a mano). Setups: `InteractionSetup`, `FlammableSetup`, `UISetup`.
- Fuego: `GrassFire` (grilla de pasto) + `ElementReactive` (objetos). Reglas de reaccion: `ReactionTable`, `MagicWorld`.

## Controles
Fuente de verdad: `GAME_DESIGN.md#controles`. Se repite en el texto del grimorio (`Learning/GrimoireTexts.cs`) y en `PlayerInputReader`:
si cambian, tocar los tres (pendiente: generar los textos desde un solo dato).

## Abierto (resumen; detalle en KNOWN_ISSUES.md)
Volumenes de musica/efectos sin cablear (KI-094) | UI y grimorio solo PC, no VR (KI-095, KI-054) | Quest sin medir (KI-049, KI-085) |
efectos de 6 glifos | fuente serif (KI-093).

## Reglas de trabajo baratas (aprendidas)
1. Un tema por pedido; los rediseños empiezan con un plan de media pagina aprobado.
2. Antes de implementar, correr los tests; despues, `HarnessPlaySmoke`. Un chequeo nuevo por cada sistema nuevo.
3. Delegar a agentes solo con especificacion cerrada y archivos propios; los agentes Opus cuestan ~250k tokens cada uno.
4. Docs obligatorios por commit: CHANGELOG y, si aplica, KNOWN_ISSUES. El resto, solo si cambia lo que describe.
