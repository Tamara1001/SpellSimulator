# Auditoría de Código: Áreas MAPA y UI

**Fecha:** 2026-11-22  
**Alcance:** Scripts del mapa (`World/Map`, `Editor/Map`), flujo de aprendizaje, estructuras, puestos, interfaz de usuario (`UI`, `Player/Hud*`, `Player/VrUi*`) y su alineación con `Docs/STATUS.md`, `Docs/KNOWN_ISSUES.md` y `Docs/MAPA_FLUJO_PLAN.md`.  
**Total de hallazgos:** 15 (8 de Mapa, 7 de UI).

---

## 🗺️ Área MAPA

### MAP-01: Inconsistencia y desactualización en `WorldZones.json` respecto a la Torre y el Portal del Estudio
- **Severidad:** 🟠 Media
- **Tipo:** Bug / Falta de sincronización
- **Archivo:Línea:** `Assets/_Project/Data/WorldZones.json:3` y `Assets/_Project/Data/WorldZones.json:19`
- **Descripción:** La entrada `tower` describe: `"use": "Biblioteca y estaciones de viento y agua; arriba, el estudio con portal y la alquimia."`, y `tower-study` indica: `"Primer piso de la torre. El portal está ahí: cruzalo para viajar."` con `origin: "Tower"`. Con el rediseño v2/v3 de la torre (pasos TV5 y T1-T4), el portal del estudio se mudó a la pradera exterior (a 14 m al sur de la puerta), las estaciones de viento/agua y la mesa de alquimia fueron retiradas. Al resolver posiciones en `MapTowerMap.Resolve`, el marcador del portal se calcula sobre el centro de la torre `(0, 16)`, mostrando en el mapa un rombo dentro de la torre en vez de su ubicación física en la pradera.
- **Alternativas de arreglo:**
  1. Actualizar `WorldZones.json` actualizando los textos de descripción y estableciendo la posición real en el exterior (`origin: "NexusPortal"` o coordenadas fijas en la pradera).
  2. Agregar resolución en `WorldZoneCatalog.TryResolveShape` para el portal exterior y sincronizar `TowerMapTests`.

---

### MAP-02: Zonas del Archivo, Fuego y Viento ausentes en el catálogo del mapa (`WorldZones.json`)
- **Severidad:** 🟠 Media
- **Tipo:** Falta
- **Archivo:Línea:** `Assets/_Project/Data/WorldZones.json:2-17`
- **Descripción:** El catálogo `WorldZones.json` aún no incluye las entradas para `archive`, `fire-field` y `wind-field`. Los puestos del Archivo ya existen en el mundo en `(0, -46)` (`MapRestocker.cs`) y los caminos ya llegan a sus bordes (`ValleyShape.Paths`), pero `TowerMap` no tiene marcadores ni filas en la leyenda para estas zonas.
- **Alternativas de arreglo:**
  1. Agregar las tres zonas en `WorldZones.json` con los IDs, colores y propósitos definidos en `MAPA_FLUJO_PLAN.md` §10.
  2. Agrupar los cuatro campos de elemento en una sola fila con chips de color en la leyenda de `TowerMap` (siguiendo la recomendación v2 de §12b.9) para no desbordar el alto de fila.

---

### MAP-03: Detección prematura de niveles completados en `TutorialRules.PassedAt`
- **Severidad:** 🟠 Media
- **Tipo:** Bug
- **Archivo:Línea:** `Assets/_Project/Scripts/World/Map/TutorialRules.cs:32-37`
- **Descripción:** En la torre-tutorial, el cálculo de niveles completados evalúa:
  ```csharp
  float floorBelow = (Levels - 2 - level) * FloorHeight;
  if (local.y < floorBelow + ArrivalMargin) passed = level + 1;
  ```
  Con `ArrivalMargin = 2.6f` y `FloorHeight = 5.0f`, para pasar del nivel 0 (terraza, y=15) al piso 2 (y=10), la condición se activa cuando `local.y < 12.6f`. Con una altura de pivote de 1.7 m, en cuanto el jugador desciende apenas 2.4 m por la rampa superior (todavía a mitad de camino y lejos del descanso), se da por completado el nivel 1 y se enciende el farol, sin haber alcanzado el piso 2 ni interactuado con la pila de cajas.
- **Alternativas de arreglo:**
  1. Reducir `ArrivalMargin` a `0.8f`–`1.0f` para que solo se active cuando los pies del jugador toquen efectivamente la cota del piso inferior.
  2. Requerir una comprobación combinada de altura y ángulo angular (verificar que esté en el descanso sur `[180°, 360°]`).

---

### MAP-04: Falta de límite exterior de radio en `TutorialRules.InDoorway`
- **Severidad:** 🟠 Media
- **Tipo:** Bug
- **Archivo:Línea:** `Assets/_Project/Scripts/World/Map/TutorialRules.cs:55-63`
- **Descripción:** `TutorialRules.InDoorway` comprueba que la altura sea baja (`local.y <= ArrivalMargin + 0.6f`), que el radio sea mayor al interior (`radius >= InnerRadius + 0.35f = 6.15f`) y que el ángulo coincida con el vano (270° ± 12°). Sin embargo, no posee una cota superior para el radio (`radius <= OuterRadius + ...`). Si el jugador camina por el exterior de la torre en el pasto a cualquier distancia hacia el sur (ej. radio 15 m) dentro de ese ángulo, la función evalúa `true`. Si el tutorial está activo, cruzar ese sector exterior completa el tutorial y apaga la barrera erróneamente.
- **Alternativas de arreglo:**
  1. Agregar cota superior de radio: `if (radius > OuterRadius + 0.6f) return false;`.
  2. Implementar un `BoxCollider` con trigger en el vano de la puerta administrado por `TowerTutorial` en lugar de una comprobación puramente matemática.

---

### MAP-05: Zonas de práctica de senderos (`CanvasDesigns.Practice`) en conflicto con el Laboratorio
- **Severidad:** 🟠 Media
- **Tipo:** Bug / Deuda técnica
- **Archivo:Línea:** `Assets/_Project/Editor/Map/CanvasDesigns.Practice.cs:28-35` y `57-60`
- **Descripción:** `PathSpots` continúa trazando las estaciones de práctica entre la Torre y el punto del campo viejo `TrainingLayout.LegacyFieldPoint` `(30, -16.5)`. Esto genera estaciones (`Practice/*_Path8` y `Path9`) en torno a `(20..28, -2..-6)`, cayendo sobre el borde norte de la plataforma y pista del nuevo Laboratorio en `(20, -8)` (riesgo advertido en `MAPA_FLUJO_PLAN.md` §12b.1).
- **Alternativas de arreglo:**
  1. Actualizar las rutas de `PathSpots` para conectar la Torre con el nuevo Nexo y el nuevo Entrenamiento en `(-6, -25)`.
  2. Añadir `(MapStructures.LaboratorySite, 18f)` a la lista `KeepOut` de `CanvasDesigns.Practice`.
  3. Purgar las entradas huérfanas `Practice/*_Path8` y `_Path9` de `Data/MapOverrides.json`.

---

### MAP-06: Spawning infinito y sin retardo en `CanvasPile` al tomar lienzos
- **Severidad:** 🟠 Media
- **Tipo:** Bug / Rendimiento
- **Archivo:Línea:** `Assets/_Project/Scripts/World/CanvasPile.cs:33-42`
- **Descripción:** En cada frame de `Update`, `CanvasPile` verifica `if (!top.IsFloating)`. En el instante en que el jugador toma un lienzo o éste cae al suelo, llama a `restocker.Release(top.gameObject)` e inmediatamente a `Refill()` de forma síncrona en el mismo frame. A diferencia de `DisplayStock` que posee `refillDelay = 0.6f`, `CanvasPile` no tiene delay ni comprobación de distancia respecto al pedestal. Esto instancia un nuevo lienzo con su propia `RenderTexture` (DrawingCanvas) de inmediato bajo las manos del jugador, pudiendo generar duplicaciones masivas si los lienzos son desplazados.
- **Alternativas de arreglo:**
  1. Incorporar un temporizador de enfriamiento (`refillDelay = 1.0f`) antes de reponer el lienzo.
  2. Comprobar que el lienzo previo se haya alejado del pedestal (`(top.position - anchor.position).sqrMagnitude > takenDistanceSq`) antes de disparar el reemplazo.

---

### MAP-07: Asignación de memoria en `DisplayStock.TrackLoose` vía `List.RemoveAll`
- **Severidad:** 🟢 Baja
- **Tipo:** Mejora / Rendimiento
- **Archivo:Línea:** `Assets/_Project/Scripts/World/DisplayStock.cs:57` y `78-89`
- **Descripción:** Cuando un objeto es retirado de un exhibidor (`takenDistance = 0.12m`), se ejecuta `TrackLoose()`, que ejecuta `loose.RemoveAll(x => x == null)`. Pasar un predicado anónimo genera recolección de basura (GC alloc) en runtime. Además, un umbral de 12 cm puede activarse erróneamente si el mostrador es empujado por física.
- **Alternativas de arreglo:**
  1. Reemplazar `RemoveAll` por una iteración simple en reversa (`for (int i = loose.Count - 1; i >= 0; i--)`) sin asignación de delegados.
  2. Detectar la extracción mediante la desvinculación del agarre del jugador (`IInteractable.OnGrab`) en lugar de polling por distancia euclidiana.

---

### MAP-08: Discrepancia en sitios fijados de peñasco 1 y cabaña norte
- **Severidad:** 🟢 Baja
- **Tipo:** Mejora / Deuda técnica
- **Archivo:Línea:** `Assets/_Project/Editor/Map/MapStructures.cs:68-70` y `Assets/_Project/Tests/EditMode/StructureSitesPinTests.cs:28`
- **Descripción:** En el plan v2 (`MAPA_FLUJO_PLAN.md` §12b), el peñasco 1 (`Crag_1`) estaba planificado para mudarse a `(40, 30)` (antiguo sitio del taller de tierra) para despejar el camino hacia el campo de viento. Sin embargo, en el código y en `StructureSitesPinTests`, `Crag_1` permanece anclado en `(20, -28)`.
- **Alternativas de arreglo:**
  1. Mudar `Crag_1` a `(40, 30)` cuando se implemente el paso 5 (campo de tierra).
  2. Actualizar el plan y comentarios si se decide conservar `Crag_1` en `(20, -28)`.

---

## 🖥️ Área UI

### UI-01: Pistas de interacción muestran `Tab: Vista completa` en VR para paneles como `TowerMap`
- **Severidad:** 🟠 Media
- **Tipo:** Bug / Inconsistencia
- **Archivo:Línea:** `Assets/_Project/Scripts/Player/PlayerInteractor.Hints.cs:58` y `87`
- **Descripción:** Cuando el jugador apunta a cualquier `IFocusPanel` (como `TowerMap`), `ComputeHoverHints()` activa el flag de panel y devuelve el texto `"Tab: Vista completa"`. En VR, no existe teclado físico para pulsar Tab, y además `SurfaceFocus.ShowPanel()` aborta inmediatamente con `if (PlatformDetector.IsVR) return false;`. El usuario en Meta Quest recibe una instrucción inaplicable y confusa.
- **Alternativas de arreglo:**
  1. Filtrar el flag de vista completa cuando `PlatformDetector.IsVR` sea verdadero (`mask &= ~2`).
  2. Generar mensajes contextuales específicos por plataforma en `BuildHoverHintTable()`.

---

### UI-02: Marcadores del mapa (`MapZoneMarker`) ocultan su acción interactiva bajo la pista del panel
- **Severidad:** 🟢 Baja
- **Tipo:** Mejora / UX
- **Archivo:Línea:** `Assets/_Project/Scripts/Player/PlayerInteractor.Hints.cs:72` y `Assets/_Project/Scripts/World/Map/MapZoneMarker.cs:43-46`
- **Descripción:** `MapZoneMarker` implementa `IPressable` con `PressWithClick => true` y devuelve `zoneName` en `GetPressPrompt()`. No obstante, `ComputeHoverHints()` evalúa:
  ```csharp
  if (hoveredPressable.PressWithClick) return viewPanel != null ? HoverHintTable[2] : "";
  ```
  Al ser el mapa un `viewPanel`, la pista resultante es exclusivamente `"Tab: Vista completa"` (o vacío), ocultando al jugador que puede cliquear los marcadores circulares y rombos para resaltarlos en la columna de leyenda.
- **Alternativas de arreglo:**
  1. Modificar la condición para que si `hoveredPressable` es un marcador, devuelva `"[Clic] Resaltar | Tab: Vista completa"`.
  2. Mostrar un toast o tooltip contextual sobre el cursor al sobrevolar marcadores.

---

### UI-03: `BookReaderUI` desactivado en VR causando aprendizaje instantáneo sin lectura
- **Severidad:** 🟠 Media
- **Tipo:** Falta / Regresión UX
- **Archivo:Línea:** `Assets/_Project/Scripts/UI/BookReaderUI.cs:85` y `Assets/_Project/Scripts/Learning/LibraryBook.cs:141`
- **Descripción:** En VR, `BookReaderUI.Open` devuelve siempre `false`. En `LibraryBook.cs`, la llamada `if (!UI.BookReaderUI.Open(info.Title, BuildPages(info), Learn)) Learn();` provoca que al pulsar el gatillo sobre un libro en VR, éste se aprenda de forma inmediata e invisible sin mostrar las páginas, ilustraciones ni textos del tratado (reintroduciendo el problema histórico documentado en KI-089 y KI-095).
- **Alternativas de arreglo:**
  1. Enrutar `BookReaderUI` hacia el canvas flotante de `VrUiPanel` permitiendo hojear con el stick.
  2. Renderizar un panel 3D en world-space con el texto del libro frente al atril.
  3. Permitir sostener el libro en la mano secundaria y pasar páginas con el stick antes de desbloquear.

---

### UI-04: Tarjetas flotantes (`FloatingNote`) con una sola cara de texto quedando ilegibles al rotar
- **Severidad:** 🟠 Media
- **Tipo:** Bug / UX
- **Archivo:Línea:** `Assets/_Project/Scripts/World/FloatingNote.cs:1-60` (y `MapTutorial.cs`)
- **Descripción:** Las notas flotantes del tutorial de la torre poseen texto únicamente en su cara frontal (-Z). La cara posterior es un plano liso de color violeta. Al poseer física de Rigidbody con resorte y reaccionar al viento y colisiones del jugador, si una nota gira 180° queda dando la espalda al jugador, dejando las instrucciones («C agacharse», «Espacio saltar», «E saltear») totalmente ilegibles.
- **Alternativas de arreglo:**
  1. Duplicar el componente TextMeshPro en la cara posterior invertida (+Z con rotación Y=180).
  2. Aplicar un torque estabilizador hacia la orientación inicial en `FloatingNote.FixedUpdate`.
  3. Hacer que la tarjeta siempre encare hacia la posición de la cámara del jugador (FaceCamera / Billboard suave).

---

### UI-05: Elipsis `…` ausente en el asset compilado `Nunito SDF` (KI-142)
- **Severidad:** 🟢 Baja
- **Tipo:** Bug / Pendiente
- **Archivo:Línea:** `Assets/_Project/Editor/FontBuilder.cs:21`
- **Descripción:** El carácter `…` fue añadido a `FontBuilder.Characters`, pero el asset `Assets/_Project/Resources/Fonts/Nunito SDF.asset` no fue regenerado. Esto genera advertencias continuas en la consola del editor: «The character used for Ellipsis is not available in font asset [Nunito SDF]», forzando a TextMeshPro a degradar el modo de desbordamiento de texto a Truncate.
- **Alternativas de arreglo:**
  1. Ejecutar `SpellSimulator/Fuentes/Generar tipografias` en Unity y luego reconstruir el mapa para preservar los GUIDs.
  2. Configurar el carácter de elipsis en el fallback asset `LiberationSans SDF`.

---

### UI-06: Panel modal del tutorial en `MainMenu.Tutorial.cs` construido con estilos inline sin soporte VR
- **Severidad:** 🟢 Baja
- **Tipo:** Mejora / Arquitectura
- **Archivo:Línea:** `Assets/_Project/Scripts/UI/MainMenu.Tutorial.cs:70-113`
- **Descripción:** El diálogo modal `¿Saltear el tutorial?` construye todos sus elementos programáticamente mediante estilos directos en C# (`box.style.paddingLeft = 36f`, etc.) en lugar de utilizar clases dentro de `SpellUI.uss`. Además, en VR el inicio del juego omite el menú principal y el diálogo por completo (`showOnStart && PlatformDetector.IsVR`), privando a los usuarios de VR de la opción de saltear o repetir el tutorial.
- **Alternativas de arreglo:**
  1. Migrar la estructura del diálogo a `MainMenu.uxml` y las reglas visuales a `SpellUI.uss`.
  2. Integrar el cuadro de diálogo en `VrUiPanel` para que pueda responderse en visor.

---

### UI-07: Polling por frame para nivel de tinta y parpadeo en `HudController.UpdateInk()`
- **Severidad:** 🟢 Baja
- **Tipo:** Mejora / Rendimiento
- **Archivo:Línea:** `Assets/_Project/Scripts/UI/HudController.cs:504-539`
- **Descripción:** En `HudController.UpdateInk()`, el parpadeo de tinta baja evalúa un temporizador y alterna clases CSS (`ink.EnableInClassList("ink--blink", blinkOn)`) mediante polling en cada frame de `Update()`. Asimismo, el nivel de tinta de la herramienta principal se sondea cuadro a cuadro en vez de responder a eventos de gasto de tinta (`InkReservoir.Drained` o `ToolUsed`).
- **Alternativas de arreglo:**
  1. Delegar la animación de parpadeo a una animación CSS/USS `@keyframes` nativa en UI Toolkit.
  2. Hacer que la actualización de porcentaje de tinta sea guiada por eventos emitidos al dibujar.

---
*Fin del reporte de auditoría.*
