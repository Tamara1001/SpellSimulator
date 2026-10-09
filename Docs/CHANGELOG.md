---
tags: [doc, changelog]
---
# Changelog

Entradas más nuevas arriba. Una por tarea/commit relevante: fecha, qué, por qué, docs tocadas.

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
