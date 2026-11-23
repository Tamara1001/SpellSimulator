---
tags: [doc, estado]
---
# Estado actual (leer primero)

> Una pagina para retomar rapido. Actualizar al cerrar cada hito; el detalle va en CHANGELOG y KNOWN_ISSUES.

## ▶ Estado 2026-11-23 (rama `main`) — leer esto primero

`rework-mapa` ya está **mergeada en `main`**; se trabaja en `main`. Ese día: auditoría con Antigravity (`Docs/AUDIT/`: informes 01-05, plan urgente, briefs 01-16; tareas cargadas en el Trello «SpellSimulator») y arreglos:
- **Físicas:** viento acotado y enfriamiento (FIS-01/02), agua con varios colliders, flotabilidad y apuntado (FIS-03/06/07), cristalizados, estado estático de `EarthShapes`, buffers 256, limpiezas.
- **Daño en duelo** (PVP-03), **VFX:** `Binarize` HLSL, presupuesto de partículas por calidad/VR, viñeta y post-proceso off en VR, quemado atenuado en muros, culling de ondas de agua.
- **UI/libros:** menú sin resaltado pegado y hover inmediato; **grimorio sólido y empujable** (cuerpo dinámico), sus páginas con stick en VR, modo fijo con B; **lector de libros en VR** (panel + rayo) y gamepad; CanvasPile con cooldown.
- **Trello:** las tarjetas hechas están en «Por Revisar». Pendientes y pospuestos: KI-145 y KI-146 en [KNOWN_ISSUES](KNOWN_ISSUES.md).
- **Lo primero:** probar a mano y en Quest (KI-145). Los tests EditMode estaban en 692 (el último ajuste, `Cerca_NoVuelve`, sin re-ejecutar). Flujo de agentes: [HARNESS](HARNESS.md) y `D:/Archivos/Claude/agy-jaula` (ahora admite corridas concurrentes).

## Traspaso 2026-11-19 (histórico) (rama `rework-mapa`) — leer esto primero

**Dónde estamos.** El trabajo vigente vive en la rama **`rework-mapa`** (no en `main`; ver el protocolo y el tracker de [MAPA_FLUJO_PLAN](MAPA_FLUJO_PLAN.md)). Desde el 2026-11-16 se reorganizó el modo Libre por flujo de aprendizaje: torre-tutorial de tres pisos y terraza, Nexo, campo de entrenamiento, laboratorio en la pradera y **el Archivo** (rotonda al sur de la torre, centro en el mundo `(0, −46)`).

**Lo último que se hizo (todo en CHANGELOG, entradas del 2026-11-18/19):**
- **Archivo, entrada: cuatro puestos de bazar** (`Editor/Map/MapRestocker.cs`, raíz `Restocker` de la escena): *Tienda de plumas* (cinco lapiceros con plumas reales de cinco tamaños), *Almacén de lienzos* (nueve pilas de lienzos/libretas reales, L en bahías laterales), *Puesto de tintas* (frascos portátiles de las tres clases) y *Puesto de sellos* (S/M/L vacíos). Todo lo exhibido es un objeto real que se agarra y se repone solo (`CanvasPile`, `DisplayStock`). Detalle en [Archivo/ARCHIVO_PLAN](Archivo/ARCHIVO_PLAN.md#estado-real-2026-11-19).
- **Cinco modelos de pluma/pincel** (`ToolModels`, `BrushRules.Catalog`): pluma fina, lápiz, pincel, pincel plano y brocha. Plantillas `Pen`/`Pencil`/`Brush`/`FlatBrush`/`BigBrush` (las tres últimas con `BrushTier`); repartidas por las mesas del campo y la torre.
- **Libretas:** siempre 8 hojas; la circular (S) ahora tiene hojas circulares (`CanvasFactory.DiscMesh`).
- **Tipografía/estética de UI** ([UI_ESTETICA](UI_ESTETICA.md)): Nunito (HUD y texto general, fuente por defecto de TMP), Lora (libros, grimorio y carteles). Caveat Brush quedó generada pero sin uso.
- **Tutorial de la torre:** piso 1 de tierra, piso 2 con pedestales, rampas arregladas, caballete en A con botón de invertir (otra sesión; ver CHANGELOG).
- **VFX** de fuego, viento, float y levitation; **VR**: cuatro rondas de pruebas de Tamara aplicadas.

**Qué NO está verificado (hacelo primero):** nada de lo reciente se probó a mano con mouse/teclado ni en VR/Quest: agarrar plumas/frascos/sellos de los puestos, ver el HUD y el lector de libros con las fuentes nuevas, el costo en Quest de ~20 lienzos de exhibición con RenderTexture (KI-141). Los EditMode y `HarnessPlaySmoke` **no se corrieron** después de la tanda de puestos (sí compila limpio y hay capturas en Play).

**Cómo retomar:**
1. `git checkout rework-mapa && git pull`; leé `CLAUDE.md`, este archivo, [ONBOARDING](ONBOARDING.md) y la sección «Traspaso» de [MAPA_FLUJO_PLAN](MAPA_FLUJO_PLAN.md).
2. Abrí Unity 6000.3.14f1, esperá la compilación, corré `HarnessTestRunner` (EditMode) y `HarnessPlaySmoke`; corregí lo que falle antes de tocar nada.
3. Si cambiás algo del mapa: `MapBuilder.Build()` (menú `SpellSimulator/Map/Build Pradera`) regenera la escena; **no editar `.unity` a mano**. Los ajustes manuales viven en `Data/MapOverrides.json`.
4. Seguí el «Siguiente» de [ROADMAP](ROADMAP.md) y el tracker del plan del mapa (pasos V, 3…12 pendientes).

**Ruido del Editor que NO se commitea** (Unity lo regenera; si aparece en `git status`, ignorarlo o `git checkout --`): `Assets/XR/Settings/OpenXRPackageSettings.asset`, `SpellSimulator.slnx`, `ProjectSettings/{ProjectSettings,QualitySettings,EditorBuildSettings}.asset`, `Assets/TextMesh Pro/.../LiberationSans SDF - Fallback.asset`, `Assets/Settings/*_RPAsset.asset`.

**Publicación (itch.io):** el build web se hace con `SpellSimulator/Build/WebGL (itch.io)` y se sube con `butler push Builds/WebGL tamara1001/spellsimulator:html5 --userversion <ver>` (butler en `D:\Programas\butler\`, ya logueado). Ver [ONBOARDING §6](ONBOARDING.md#6-builds).

---

## Estado histórico (octubre 2026)

**Hito:** modo Libre con el plan de la octava tanda COMPLETO (bloques 1-6) y **F0 del multijugador hecho** (preparacion sin red). **F1 (sesion y conexion) casi completa:** Netcode for GameObjects 2.13 + Relay por WebSocket, menu Multijugador, avatar de red y bots de prueba; Ale y Tami la probaron por Internet. **F2 (mundo compartido: objetos fisicos y agarre) hecha y verificada con bots**, tambien con 150 ms simulados. Falta de F1 lo no importante (avatar mas fino, prueba PC-Quest). **F3 (dibujo compartido y hechizos replicados) hecha y verificada con bots** (trazos, lanzamiento, roca replicada, viento del cliente). **F4 (mundo reactivo, portales y suministros en red) hecha y verificada con bots** (portales, fuego del pasto, objetos reactivos, suministros y papel dibujado; limites en KI-121). **F5 hecha en dos lotes:** Duelo 1 contra 1 al mejor de 3 en red (reglas puras, anfitrion autoritativo, HUD, boton «Modo Duelo», arena propia con coberturas y fuente, tinta del servidor y re-lectura de los hechizos del rival; bots 16/16 y 8/8; limites en KI-122). Siguiente: que Ale pruebe F2-F5 a mano con otra persona; despues arena propia, tinta del servidor e IA.
**Tests (2026-10-10):** 473 EditMode (antes 436) (`HarnessTestRunner`) + smoke de Play (`HarnessPlaySmoke`) + matriz de lienzos (`HarnessCanvasMatrix`, 425 chequeos; modos `Only=` RUNE, FOCUSRAY, CHARGE, SPELLS, RUNEJOIN, HALVES, NESTED, PORTAL) + entrada real (`HarnessRealInput`, 30) + red (`HarnessNetSmoke`: local 15, Relay 7, menu 6, desconexion 13) + bots (`HarnessBots`: local, Relay, churn, agarre con/sin 150 ms, dibujo y lanzamiento; ver [HARNESS](HARNESS.md#bots-de-red)). Todo en verde. Ale probo a mano toda la checklist de PC de [TESTING](TESTING.md): en orden. VR/Quest sigue sin probarse (M7).
**Escenas:** `Pradera.unity` (mapa, indice 0; las posiciones movidas a mano se conservan en `Data/MapOverrides.json`) y `Escena.unity` (laboratorio).

**Hecho en la octava tanda ([PLAN_OCTAVA_TANDA](PLAN_OCTAVA_TANDA.md)):** controles nuevos (E agarra/deja en el aire, T grimorio, Esc pantalla+pausa, Tab vista completa), tinta por metro normalizada + cartuchos + clases de tinta + cinturon (V cambia), retardo de lanzamiento con brillo, hechizos con fisica (roca, cristal, agua, viento), runas por material con desgaste y fusion, mitades de cualquier lienzo, circulos anidados, estructuras y 4 portales circulares con identidad de 3 runas, reposicion y reciclaje, 19 hechizos prearmados con descubrimiento, menu de pausa dentro del grimorio.
**Ultima tanda (2026-10-10, prueba manual de Ale, 13 puntos):** signos con efecto propio (crush, collection, billowing, repetition, weave, region), tierra con impacto (muros, puentes, estacas, lanzamiento, Wall Breaker que recorta mallas sin tocar libros ni portales), tutoriales nuevos (taller de piedra y laboratorio del lago), hojas de viento, caldero/barquito, numeros de viento/agua/fuego, lapiz fino, circulo permisivo, trazos del suelo y agarre de objetos grandes. Detalle en CHANGELOG, ADR-037, KI-123/124. **Nada de esto se probo a mano todavia.**
**Siguiente (para quien continue):** 1) probar a mano esta ultima tanda y F2-F5 con otra persona, y ajustar numeros (`RockMath`, `MagicField`; KI-123); 2) reconocimiento de luz/glifos raros que se leen como «viento especial» (KI-124): juntar sellos reales de `persistentDataPath/Sellos` en `Tests/Fixtures` con `expect`; 3) comprobar `wss` en el build WebGL; 4) Quest (M7), arena/duelo pulidos e IA solo si se pide. Diferido con decision explicita: IA rival, Quest/VR, WebGL `wss`. F0 incompleto: KI-118.

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

## Reglas de trabajo (aprendidas)
1. Un tema por pedido; los rediseños empiezan con un plan de media pagina aprobado.
2. Antes de implementar, correr los tests; despues, `HarnessPlaySmoke`. Un chequeo nuevo por cada sistema nuevo.
3. Delegar tareas a asistentes de IA solo con especificacion cerrada y archivos propios; revisar siempre el resultado con los tests.
4. Docs obligatorios por commit: CHANGELOG y, si aplica, KNOWN_ISSUES. El resto, solo si cambia lo que describe.
