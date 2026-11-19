---
tags: [doc, changelog]
---
# Changelog

Entradas más nuevas arriba. Una por tarea/commit relevante: fecha, qué, por qué, docs tocadas.

## 2026-11-19 — Sin marcos de cara, atriles físicos, puerta de la torre hacia afuera y puerta con bisagra en el piso de viento

- Se quitan los marcos dorado/azul, la flecha y el botón del marco (`CanvasFrame`, `CanvasFaceButton`, `CanvasFrameBuilder`) y `FiringFace`: todo lienzo dispara por la cara donde se escribe (como antes de Bloque A); F da vuelta los sueltos.
- Atril con slot: ahora es un objeto físico pesado (`MovablesSetup.MakeMovable`: Rigidbody 25 kg + `Movable`), con base maciza; el lienzo lo sigue (`CanvasEasel.LateUpdate` + `PhysicalCanvas.FollowEasel`). Botón con flechas circulares (`EaselReverseButton`) invierte el sentido de disparo del atril (`DrawingCanvas.SetReversedByEasel`, regla pura `CanvasRules.EffectiveInkSign`); se apaga al sacar el lienzo. Se elimina el giro de 45° por clic.
- Puerta principal de la torre: cierra en la cara exterior del muro y se abre hacia afuera (90°, contra la mejilla del zaguán).
- Piso 1 (viento): la «tela» pasa a ser una hoja de tablones con `HingeJoint` (eje vertical, pegada a la pared) y resorte flojo: viento, agua o una caja la abren con fisicas (no se teletransporta); una vez abierta queda donde la dejan y deja pasar al jugador; el rearme del tutorial la cierra.
- Docs: `GAME_DESIGN.md`, `LIENZOS_PLAN.md`.

## 2026-11-18 — Almacén de lienzos: pilas con lienzos reales (sin botones)
- El selector (Tipo/Tamaño/Forma/Hojas/SACAR) se reemplazó por **9 pilas** con un lienzo real flotando sobre cada una: 3 libretas (S redonda, M, L; **siempre 8 hojas**) y 6 hojas sueltas (S/M/L × cuadrada/rectangular). Las dos L van en peanas de piso a los lados del puesto.
- Nuevo `CanvasPile` (Scripts/World): cuando el jugador agarra el lienzo de exhibición (deja de flotar) la pila repone otro igual; lo llevado cuenta para el tope de 8 sacados de `CanvasRestocker` (`SpawnDisplay`/`Release`). `PhysicalCanvas.PinUntimed()` lo deja flotando sin el límite de 60 s. En red no hay exhibición.
- Verificado en Play: las 9 pilas aparecen y al sacar una se repone (59 → 60 lienzos). Falta probar a mano el agarre con mouse/VR.
- Costo: 9 lienzos de exhibición suman 9 RenderTextures permanentes (vigilar en Quest).

## 2026-11-18 — Restocker rehecho como dos puestos en el Archivo
- `MapRestocker` arma ahora la **Tienda de plumas** (cinco tarros con un pincel cada uno; tocar el tarro saca ese pincel) y el **Almacén de lienzos** (rollos, pilas y un tablero de pedido inclinado con botones redondos y SACAR), con toldo rayado, farol y cartel. Mismo `CanvasRestocker` y mismas acciones: la lógica no cambia.
- Ya no están en la torre: van a la entrada del Archivo, en (±7,5, +5,5) respecto de (0,−46), girados 25° hacia el camino. El plano del Archivo (Antigravity) los ponía en z = +11,5, pero ahí quedaban dentro del entrenamiento y de su línea de tiro; se corrieron.
- Plano y plan del Archivo: `Docs/Archivo/archivo_plano.svg` y `ARCHIVO_PLAN.md` (generados por agente externo; sin revisar por Tamara, las coordenadas de las tiendas están desactualizadas).
- Falta probar a mano (PC/VR) el clic sobre los tarros y botones.

## 2026-11-18 — Restocker: pinceles y selector de lienzo/libreta (Bloque C de LIENZOS_PLAN)

- Mueble `Restocker` (armado por `MapRestocker` con primitivas): 5 pinceles del catalogo (`BrushRules`) y selector de tipo (lienzo/libreta), tamaño S/M/L, forma y hojas (1-8) con boton SACAR. Logica pura en `RestockRules` + `RestockRulesTests`.
- `CanvasFactory` crea en juego el lienzo/libreta desde un `CanvasSpec` (marco de cara, hojas, tapa); `CanvasRestocker` lleva el tope (8 por clase, se borra el mas viejo) y no guarda nada: al recargar desaparece. En red no saca nada.
- Ubicacion provisoria: biblioteca de la torre, junto a la mesa de trabajo (el Archivo de la pradera aun no existe; al construirlo basta mover el objeto).
- Harness: checks «lienzos C» en `HarnessPlaySmoke`. Docs: `LIENZOS_PLAN.md`.

## 2026-11-18 — VFX: signos con forma propia sin la base generica debajo

- `SpellManifestation`: con crush, orb, billowing, focus, gather, collection, repetition o region ya no se lanza la rafaga/chorro/abanico generico (misma distancia y tamano en todos los hechizos, sobre todo en fuego): el signo es el efecto. La fisica (`MagicField`) sigue leyendo el plan completo. Cool, empower, entwine y weave siguen modificando la base.
- Pendiente (propuesto, sin implementar): float y levitation sin efecto fisico claro sobre objetos apoyados en el papel (`MagicField` solo sostiene con `Hold`, y float no genera `Hold`).

## 2026-11-18 — VFX de signos, segunda pasada: forma propia, region, tinta resistente y estabilidad

- `SpellManifestation` (ahora `partial`): crush = onda de choque plana + escombros en golpes repetidos; billowing = nube opaca y grande que crece; focus = haz largo + punto de mira giratorio; gather = estelas planas hacia el sello; collection = nucleo estable sobre el sello + pulsos desde todos lados; repetition = flujo invertido (lluvia que cae al sello) + anillo-reloj. Base reducida a ~10 % con esos signos.
- `region` (`SpellManifestationExtras.ApplyRegion`): anillo de fuego, cortina de agua, muro de viento, perimetro de luz, domo de cristal y cerco de polvo de tierra.
- `waterproof`: anillo de destellos palidos que repele gotas. `stability` baja: ruido en particulas, chispas (`Sign_Unstable_Crackle`) y parpadeo de luz. Logica pura en `SpellVisualExtrasMath` + `SpellVisualExtrasMathTests`.
- Harness `HarnessModifierMatrix`: encuadre aislado (capa propia, fondo liso y disco de suelo, sin arboles ni postes), cuerpos fisicos del hechizo incluidos y borrados entre capturas, espera en tiempo real para tierra/cristal (anclaje) y 3 extras (region, waterproof, stability_low) fuera del conteo: `DONE n=84 extras=18`.
- Fix: `HarnessPlaySmoke.cs` tenia un `Split('\n')` roto con salto de linea literal (no compilaba).
- Sutiles aun: viento (crush, spread, gravity, waterproof), tierra solida (cool, entwine, spread, gravity: el cuerpo es la roca), waterproof en general. Docs: `MODIFICADORES_AUDITORIA.md` (tandas 2 y 3 hechas, salvo la parte fisica de region/waterproof).

## 2026-11-18 — Lienzos, bloque B: pinceles (BrushSpec) sin deslizador de grosor

- `Core/BrushSpec.cs`: `BrushSpec` (grosor, punta, forma) y `BrushRules` (catálogo de 5 pinceles, ancho de los ToolDefinition viejos, gasto de tinta). `PhysicalTool.ApplyBrush` convierte una pluma en un pincel; el ancho del lápiz y el pincel de siempre no cambia.
- Se quita el grosor elegible de plumas/pinceles (rueda, stick VR y fila de la HUD); la goma conserva su tamaño. Textos del grimorio y `GAME_DESIGN` actualizados.
- Reconocimiento: `TensorRasterizer` usa 2 px fijos, así que el trazo grueso no lo afecta (test `BrushRulesTests`). Punta y forma son datos para el restocker; todavía no cambian cómo se estampa la tinta.

## 2026-11-18 — Lienzos, bloque A: lienzos unificados, cara de disparo y atriles con slot

- `CanvasSpec`/`CanvasRules` (tamaños S/M/L, formas, 8 hojas máx.) con `CanvasRulesTests`; `CanvasDesigns` ajusta todo lienzo del mapa al preset más cercano (`Snap`).
- Marco de cara de disparo (`CanvasFrame`, `CanvasFaceButton`, `CanvasFrameBuilder`): dorado/azul + flecha; el botón de la esquina invierte `InkSign` sin mover el lienzo.
- Atriles con slot (`CanvasEasel`): acople al soltar/dejar flotando cerca, cambio de lienzo, giro de 45° con clic en la base. `PhysicalCanvas` suma `Dock/Eject/Home`. Los caballetes del tutorial siguen fijos.
- `NotebookPages`: máximo 8 hojas. Docs: `GAME_DESIGN.md` (lienzos), `LIENZOS_PLAN.md` (plan y limitaciones).

## 2026-11-18 — Lienzos, bloque 0: laboratorio sin poste y «Limpiar efectos»

- El papel de prueba del campo flota (se quitó `LabPaperStand`); sigue moviéndose y girando con los controles del banco (Lado/Distancia/Altura/Giro/Inclinac./Tam. papel).
- Botón «Limpiar efectos» (`LabBenchAction.ClearEffects`, `Lab/LabEffects.cs`): despawnea manifestaciones, fragmentos, marcas/rocas del suelo y fuego de pasto; no toca cajas, papel ni lienzos. Sin efecto en red.
- Botones del panel derecho: 2 filas de 4 (`LabBenchLayout.ButtonColumns`, paso 0,105 m).
- Plan general: `Docs/LIENZOS_PLAN.md`. HarnessPlaySmoke: chequeo nuevo «lienzos 0» y primer error de consola en el resultado.

## 2026-11-18 — Modificadores VFX: Alto contraste, diferenciación clara de signos y corrección de ancla

- **Diferenciación inconfundible de signos (`SpellManifestation.cs`, `SpellVisualModifierMath.cs`, `ElementVfx.cs`):**
  - **Atenuación base:** con signos dominantes la emisión base (Jet/Burst) baja al 20–25%, evitando opacar las pistas de signo.
  - **Crush:** fragmentos sólidos grandes en 3D (mesh cubes/rock shards vía `ParticleSystemRenderer`) cayendo y estallando con destello de choque.
  - **Orb:** membrana/esfera traslúcida con borde Fresnel del color elemental envolviendo el efecto y columna base al 20%.
  - **Billowing:** nube enorme lenta y esponjosa (partículas ~3.8x, baja velocidad, sin hundimiento).
  - **Cool:** tinte blanco/azul forzado en todas las partículas del hechizo (`colorOverLifetime`/`startColor`) más vaho gélido descendente.
  - **Empower:** efecto 1.6x más grande, destellos brillantes continuos e intensidad de luz x2.6.
  - **Entwine:** espiral estrecha densa de alto contraste alrededor del eje con avance longitudinal.
  - **Focus:** haz fino láser directo hacia delante con base reducida y destello en la boca.
  - **Gather vs Collection:** `gather` con succión centrípeta radial continua; `collection` con pulsos periódicos hacia un punto focal sobre el sello.
  - **Repetition:** reflujo cinemático donde las partículas retornan al sello acompañadas de pulsos de eco.
  - **Weave:** dos hélices gruesas entrelazadas en rotación opuesta.
  - **Spread / Gravity:** `spread_high` amplía notablemente el cono y radio; `gravity_low` genera flotación ascendente visible.
  - **Ancla y gravedad (Tierra y Cristal):** elevación del ancla en superficies y atenuación de gravedad extrema en Shuriken para evitar que el efecto se hunda bajo el terreno.
- **Tests EditMode:** `SpellVisualModifierMathTests.cs` actualizado con validaciones para `GetCollectionParams` y suite completa en verde.

## 2026-11-18 — Saltear tutorial: sin pared invisible en la puerta

- `TowerTutorial.Skip()` apaga los colliders de las trabas de la puerta (el sensor de viento invisible `DoorLatch_Bar` bloqueaba el vano); `ResetTutorial` los reactiva. Sin probar a mano.

## 2026-11-18 — Modificadores VFX: Tanda 1 (signos y parámetros visuales visibles)

- **Tanda 1 de auditoría (`Docs/MODIFICADORES_AUDITORIA.md` 3.5, 3.6, 3.3, 3.4 y 4.3–4.5):** se hicieron visibles en el VFX (`SpellManifestation.cs`) los 10 signos modificadores presentes en `SpellPlan`: `crush` (estallido de fractura/impacto), `orb` (membrana/caja esférica orbital), `billowing` (nubes voluminosas y lentas), `cool` (tinte frío y vaho gélido), `empower` (sobretensión energética y multiplicador de luz), `entwine` (zarcillos constrictores en espiral), `focus` (mira fija/haz guía directo), `gather`/`collection` (succión centrípeta), `repetition` (inversión temporal y pulsos de eco) y `weave` (emisores gemelos en doble hélice desfasada 180°).
- **Diferenciación elemental sextupla:** cada signo cuenta con parámetros visuales únicos (forma, color, tamaño, emisión y dinámica orbital/radial) para Fuego, Agua, Viento, Tierra, Cristal (Tierra cristalina) y Luz mediante `ElementLook`.
- **Apertura de cono (`spell.spread`):** el ángulo y radio del cono ahora combinan la apertura de dispersión con el foco y la convergencia en todos los elementos.
- **Gravedad de partículas (`spell.gravity`):** modula el `gravityModifier` en todos los sistemas Shuriken del hechizo, permitiendo flotación gradual o ingravidez.
- **Rendimiento y arquitectura:** lógica pura en `SpellVisualModifierMath.cs` (sin `MonoBehaviour`), bucles indexados con cero allocations en `Update`, y suite de pruebas EditMode en `SpellVisualModifierMathTests.cs`.

## 2026-11-18 — VR: interfaz de PC en panel flotante, altura, lápiz y objetos grandes

- **Interfaz (`VrUiPanel`):** el HUD y el menú principal de PC (UI Toolkit, el mismo `PanelSettings`) se dibujan en una RenderTexture 1920x1080 mostrada en un cuadro delante de la cabeza (sigue la mirada con retraso; con el menú abierto queda quieto). Se apunta con el rayo de cualquier mano y el gatillo aprieta el botón (`NavigationSubmitEvent`). Clic del stick der. = menú (se quitó ahí el atajo de «dar vuelta la hoja»). El menú ya no se lleva la cámara a la órbita en VR. Se borraron `VrMenu` y `VrWristHud` (los de la 4.ª prueba).
- **Altura:** el origen XR va a los pies del cuerpo (tracking Floor); agacharse con botón baja ~0,95 m (antes 0,5); agacharse de verdad no baja de más. (Antes la cabeza quedaba ~1 m por encima.)
- **Viga del tutorial:** vuelve a ser obligatoria; en VR se abre un pasillo en el medio (zona `TutorialBypassZone`) que da por hecho «agacharse». La reja sigue igual.
- **Lápiz:** en VR se sostiene con la punta hacia adelante (como en PC); apoyar la punta contra un lienzo (≥ 2 mm de presión) dibuja/borra sin gatillo (`PhysicalTool.TipPressing`); el rayo a distancia se suaviza según la velocidad (temblor); ayuda de controles de VR en `HudHints`.
- **Objetos grandes** (cajas, rocas, grimorio): en VR ya no se pegan a la mano; se sostienen a distancia como en PC (`ObjectMover` con el rayo/rotación de la mano; stick der. adelante/atrás acerca/aleja; grip suelta, soltar quieto = flota, con envión = arroja).
- Sin probar en el visor. Riesgos: orientación vertical de la textura del panel, y que el `PanelSettings.targetTexture` se aplique en caliente.

## 2026-11-18 — VR: lápiz con colisión y movimiento más suave

- **Lápiz con colisión (`PhysicalTool.LateUpdate`):** la punta no atraviesa lienzos ni el mundo; se empuja la herramienta a lo largo de la normal de la superficie, recalculado cada cuadro desde la mano (sin estado), así la punta no tiembla y se desliza sobre el papel.
- **Movimiento VR (`PlayerMovement`):** cuerpo sin fricción contra paredes, escalones de hasta 0,5 m que se suben deslizando (no de golpe) y también al avanzar en diagonal, para no quedar trabado en umbrales (laboratorio) ni tener que saltar.
- Sin probar en el visor; los 0,5 m y la velocidad de subida (2,2 m/s) son estimaciones.

## 2026-11-18 — VR, 4.ª prueba: causa raíz (Camera.main nulo), menú VR, dibujo por contacto, perillas

- **Causa raíz:** la cámara del visor no tenía el tag `MainCamera`, así que `Camera.main` era null en VR. Eso rompía: el avance según la mirada (caminaba según el cuerpo, que solo gira con el giro a saltos), la HUD de la muñeca (hotbar), agacharse, el giro y el enderezado de lo agarrado. `PlatformDetector` ahora etiqueta la cámara VR.
- **Menú VR** (`VrMenu`, clic del stick der.): panel flotante con Continuar, Controles (ayuda), mano principal, saltear tutorial, ir al Nexo, salir. Rayo de cualquier mano + gatillo; bloquea la entrada mientras está abierto.
- **Dibujo por contacto:** punta a ≤ 3,5 cm de un lienzo = el trazo cae bajo la punta; más lejos sigue el rayo a distancia (`PhysicalTool.TryContact`).
- **Perillas del laboratorio:** además del giro de muñeca, el stick der. las gira (analógico); con una perilla capturada el stick no gira al jugador.
- **Tutorial VR:** agacharse ya no es obligatorio (la reja pide solo saltar).
- Sin probar en el visor.

## 2026-11-18 — VR, 3.ª prueba: giro con el stick, agacharse real, manos vacías al empezar, carteles con controles VR

- **Giro con stick der.:** el giro a saltos rotaba el transform pero el Rigidbody (con interpolación) lo pisaba; ahora se le pasa la nueva pose (`PlayerLook.HandleSnapTurn`).
- **Agacharse:** además del clic del stick izq. (ahora avisa «Agachado / De pie»), agacharse de verdad (cabeza ≥ 35 cm más baja que la más alta vista) agacha el cuerpo (`PlayerMovement.PhysicalCrouch`).
- **Lápiz al aparecer:** no se halló quién lo equipaba (ningún código lo da); `PlayerSpawn.Begin` ahora vacía las manos (`PlayerInteractor.ClearHands`). Pendiente de confirmar en el visor.
- **Tutorial:** `VrTutorialTexts` cambia al empezar (solo VR) los textos de los carteles `Note_Tutorial_*` por los controles Touch.
- Sin probar en el visor; pendiente: dibujar apoyando el lápiz sobre el papel.

## 2026-11-18 — VR, 2.ª prueba: tinta en los dos ojos, salto/agacharse, HUD en la muñeca, menos mareo

Segunda prueba de Tamara en Quest 3 (tinta solo en el ojo izquierdo, no salta ni se agacha, sin hotbar ni menú, agarrar con la izquierda raro, no deja flotar, no cambia el grosor, mareo, puerta abierta al empezar).

- **Tinta en un solo ojo:** `InkDecal`, `GrimoireInk` y `GrimoirePage` no tenían las macros de estéreo (`UNITY_VERTEX_INPUT_INSTANCE_ID`, `UNITY_SETUP_INSTANCE_ID`, `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO`, `multi_compile_instancing`): con single pass instanced solo se dibujaban en el ojo izquierdo. Corregido. (Los demás shaders propios ya las tenían.)
- **Menú:** el botón se llamaba `menuButton` en el binding, pero en el layout de los Touch es `menu` (uso `MenuButton`): nunca disparaba. Ahora lee `menu` y `{MenuButton}`.
- **HUD en VR:** la HUD de pantalla no se ve en el visor. Nuevo `VrWristHud`: panel de mundo junto a la mano izquierda con la barra (3 slots, el equipado marcado), tinta, lo agarrado y los avisos; aparece al mirar la mano y 3 s tras cada cambio.
- **Salto / agacharse:** A o X de la mano que no lleva la herramienta salta; el click del stick izquierdo alterna agacharse (baja la cápsula y la cámara; `PlayerMovement.HandleStance` ya no se corta en VR). Ya no se corre en VR. El tutorial de la terraza pedía ambos.
- **Grosor y slots:** stick derecho arriba/abajo cambia el grosor; B (toque) pasa al slot siguiente; B (0,6 s) arroja la herramienta.
- **Agarre:** un toque corto de grip (< 0,35 s) deja el objeto «pegado» (otro apretón lo suelta); mantenerlo funciona como antes. Cuadernos, lienzos y el grimorio se enderezan al agarrarlos (cara hacia la cabeza, derechos), así no salen boca abajo con la mano izquierda. Umbrales: ≥ 2 m/s arroja, ≤ 0,8 m/s deja flotando (antes 1 y 0,35: el pulso de la mano nunca bajaba tanto).
- **Mareo:** el cuerpo ahora se interpola en VR (se movía a 50 Hz con un visor de 72–90 Hz); caminar a 2,6 m/s con fuerza 14 (antes 4,2 m/s y 50), respuesta cuadrática del stick, frenada más rápida y velocidad proporcional al empuje.
- **Puerta final abierta al empezar:** en VR no hay menú de inicio, así que nunca se llamaba a `PlayerSpawn.Begin` y el tutorial no arrancaba (puerta con `startOpen`). `MainMenu.Start` ahora entra directo al tutorial en VR.
- **Verificación:** compila; 626 tests EditMode en PASS. `HarnessPlaySmoke` no es válido con el visor conectado por Link (el Editor arranca en modo VR y el smoke espera el menú de PC): hay que correrlo con Link cerrado. El pánel de muñeca y el resto de VR se construyeron sin errores en un Play con el visor conectado.
- **Pendiente:** dibujar «apoyando el lápiz». `PhysicalTool` ya tiene un modo de contacto (`vrContactDrawing`, alcance 4 cm, apagado) y el rayo sale de `raycastOrigin`; no se tocó porque falta saber qué se siente raro (¿trazo corrido de la punta?, ¿hay que mantener el gatillo?, ¿tiembla con el papel en la mano?).

## 2026-11-18 — VR: manos simétricas, soltar con gestos y modelos de control

Primera prueba de Tamara en Quest 3 (Air Link a Unity): el movimiento andaba, pero todo se agarraba con la derecha y las manos eran dos cubos grises que tapaban el lienzo.

- **Entrada por mano** (`PlayerInputReader`): grip, gatillo y A/X de cada mano por separado. Gatillo y A/X valen para la mano de la herramienta; el grip de cualquiera agarra. Stick derecho: golpe a los costados = giro a saltos de 45° (`PlayerLook`) o página (si la mano lleva un cuaderno); arriba/abajo = slot. Y: toque cambia la tinta, mantener 1,5 s reinicia el mundo (antes un toque reiniciaba).
- **Agarrar y soltar** (`PlayerInteractor.Vr.cs`): se agarra con la mano que aprieta el grip y se lleva mientras se mantenga; al soltar, la velocidad de la mano decide (≥ 1 m/s arroja, ≤ 0,35 m/s deja flotando si es fijable, si no cae). Herramientas y estampas pasan a la mano que las agarró. El rayo sale de la última mano usada, con puntero propio. B arroja la herramienta hacia donde apunta la mano. Corregido: el grip sobre perillas solo valía un frame.
- **Manos:** los cubos fueron reemplazados por los modelos de control de los Starter Assets de XRI (`VrHandVisual`), sin colisionadores y escondidos mientras la mano lleva algo. Los pulsos hápticos van a la mano de la herramienta (`Haptics.MainHand`).
- **Docs:** `GAME_DESIGN.md` (tabla de controles) y `GrimoireTexts.cs` (página «Realidad virtual») actualizados.
- **Sin verificar en el visor:** compila, 626 tests EditMode y `HarnessPlaySmoke` en PASS (PC). Los umbrales de arrojar/flotar y la posición de lienzos en la mano derecha hay que ajustarlos probando en Quest.

## 2026-11-18 — Torre v3: carteles al muro, pisos despejados y más cosas para probar

Segunda crítica de Tamara a la torre (carteles que no se entienden o al revés, cosas en el camino, pisos vacíos o con muebles sin sentido, runa de oro en la torre).

- **Bug de base:** `TowerBuilder.WallRadius` tenía las caras del muro corridas 9° (las caras del polígono están centradas en múltiplos de 18°, no en los vértices): los muebles quedaban hasta 7 cm adentro o afuera del muro. Corregido; todo lo pegado al muro va en el CENTRO de una cara (múltiplo de 18°) y sin ventana.
- **Carteles:** nuevo `WallCard`: pegados al muro, de frente al centro, nunca en diagonal ni sobre el pasillo. Textos más claros: «Agarrar y soltar / Practicá en el centro», «Lienzo en el aire», «Lienzo vertical / Clic der. + mouse: girar» (antes «Ponelo de canto»), «Tirá una caja / Q arrojar a la diana». Los de práctica flotan sobre el marco y se leen de los dos lados.
- **Piso 2:** pasillo del anillo libre salvo la pila; el centro es una zona de práctica llena (mesa con libros/maceta/vela, 2 marcos, diana, cajas, barriles, bancos, silla, banco largo y 2 lienzos flotantes).
- **Piso 1:** sin estante en la llegada (353°) ni atril ni baúles ni pedestal. Todo el estudio en el centro: mesa con pluma, tinteros, libro del viento y cinturón; mesa de cuadernos y hojas; 3 lienzos flotantes. Caballete más cerca de la cortina (215°, r 3,5). Borradas las herramientas viejas que quedaban en el piso (`Pen_933_0`…).
- **Planta baja:** estante a 216° y 144° (centro de cara), cajas solo en 108°/126° (no enterradas); pestillo 252°, contrapeso 288° y piedra 234° alrededor de la puerta (270°); caballete a 240° (la puerta queda libre); atril del libro de elementos a 324°; cartel único a 342°.
- **Runa de oro:** sacada de la torre; ahora sobre un pedestal en el campo de entrenamiento.
- **Harness:** `HarnessPlaySmoke` +1 chequeo (carteles pegados al muro y runa de oro fuera). Reach/floors/spell test igual que antes; 626 tests EditMode verdes.
- **Pendiente:** el menú de inicio no se ve al dar Play en el editor de Tamara (en el harness y en mis capturas sí aparece): hace falta su captura de consola/Game view.

## 2026-11-17 — Torre v2, rehecha completa (proporciones, ventanas, terraza sellada, pisos y muebles)

Tras la crítica de Tamara al paso 1 («terraza se puede pasar caminando, actividades mal acomodadas y mal formuladas, el piso del viento no se puede hacer, sigue el portal, escaleras finitas, muebles despegados, torre ancha y baja, todas las ventanas al sur»):

- **Proporciones:** `OuterRadius` 6,4 (antes 6,8), `FloorHeight` **5** (antes 4,5), terraza 7,7, base 8,2, postes 6,1; rampa de **1,65 m** de ancho (`RampInner` 4,1) con baranda de piedra en vez de madera maciza. `TowerBuilder.WallRadius(ángulo, fondo)` calcula el radio para dejar un mueble **pegado** a la cara interior del muro de 20 lados.
- **Ventanas en los cuatro lados:** bajas (filas 2–4) al sur/este/oeste y ranuras altas (filas 5–6) al norte, sobre la rampa.
- **Terraza sellada:** baranda circular completa en r 4,1 (con collider de 2,2 m: no se salta), viga/muro/reja de la baranda al parapeto. `HarnessTerraceReach`: caminar/agacharse/saltar/ambos → no llega a la bajada; sin la reja sí.
- **Piso 2:** pila (3 cajas grandes + 2 chicas + barril) en el pasillo a 198°; práctica opcional en el disco central (mesa, dos marcos de pie, diana en el muro) con rótulos de una línea. **Piso 1:** mesa de pluma y atril del Tomo I pegados al muro, **caballete inclinado con lienzo fijo**, plantilla fantasma y flecha de apunte siempre visible (`TutorialPaperGuide` sin `PhysicalCanvas`), a ~2,5 m de la cortina. **Planta baja:** atril con `elements`, caballete apuntando al pestillo, trabas pegadas al muro.
- **Carteles:** los 14 de `Docs/Torre/carteles.md` (1,30 × 0,50 m, 2 líneas) y mini-rótulos 0,90 × 0,28.
- **Quitado de la torre:** portal `tower-study` (ahora en la pradera, 14 m al sur de la puerta), mesa de alquimia + sillas (el cinturón va sobre `Workbench_Study`), estaciones de viento/agua, `WallBoard_Library`/`BigCanvas_Library`/`DraftingTable`/`Easel_Study`, velas sueltas, pedestal de la terraza y los libros variants/motion/shape/rare. El pedestal de la runa de oro quedó contra el muro.
- Harness: `HarnessTerraceReach.RunFloors()` (piso 2 y piso 1: sin quitar la pila/tela no se llega a la boca de la rampa; quitándola sí), `HarnessSpellTest` con la geometría nueva, smoke con `Easel_Wind_Canvas`.

## 2026-11-17 — Torre v2, paso 1: geometría más grande y rampas de media vuelta

- `TowerBuilder`: `OuterRadius` 5,5 → 6,8 m, `InnerRadius` 6,2, `RampInner` 4,8 (rampa de 1,35 m pegada al muro), **`RampTurn` 270° → 180°** (sube de 0° a 180° por el norte, se baja de 180° a 0°), hueco del piso de 48° a 180°. Queda libre el disco central (Ø 9,6 m) y el arco sur de cada piso (la plaza).
- Terraza/talud/postes/barrera al nuevo radio (`TerraceRadius` 8,2, `BaseRadius` 8,6, `PostRadius` 6,5, `BarrierRadius` 9,2); techo escalado; puerta de 1,92 m (el ancho del vano); alfombra del pasillo; vigas del cielorraso según el hueco nuevo.
- **Ventanas al arco sur** (las rampas tapan el norte): segmentos 11/13/17, 12/14/16/18 y 13/16/18 por piso.
- Spawn en el balcón norte (`TerraceOffset` (0; 6,8), rumbo 115°), `TerraceRules` (viga 25°, muro 325°, reja 195°, radio 8,2). `MapTutorial`: hueco 48°–180°, valla 115°, pila 198°, tela 195°, trabas 258°/282°/294°; props de la pila, diana (al muro SE, 315°) y trabas movidos a los radios nuevos; `Respawnable.TowerRadius` 9.
- Tests: `TutorialRulesTests` y `SpawnRulesTests` actualizados (media vuelta, hueco, disco de 9,6 m). Smoke +1 chequeo («torre v2»: rampa norte, hueco solo sobre la rampa, plaza sur, disco central). 626 EditMode.
- **Pendiente (pasos 2–5):** los obstáculos de la terraza, los carteles y los ejercicios del piso 2 todavía están en sus posiciones viejas (la viga y el muro cruzan el centro); se rehacen piso por piso.

## 2026-11-17 — Tutoriales v2, R3: piso 1 con plantilla fantasma y rayo de apunte

- Lienzo de práctica agarrable sobre una mesita del piso 1 (`TutorialPaper_Wind`) con `TutorialPaperGuide`: **plantilla fantasma** azul del sello de la receta `gust` (sigilo de viento, dos columnas y anillo; 14 trazos hechos con `LineRenderer`, no son trazos del lienzo ni entran al reconocimiento) y **rayo azul** que sale del papel por `EmissionNormal` mientras se lo sostiene.
- `LabSealBench.RecipeStrokes(id)` (trazos de una receta sin tocar el estado) y `PhysicalCanvas.IsHeld`.
- Cartel de 3 pasos («1 mojá la pluma · 2 calcá el dibujo azul · 3 agarrá el papel y apuntá a la tela») y cartel «apuntá el rayo azul a la tela» junto a la tela.
- Smoke +1 chequeo (14 trazos fantasma, rayo, sin tinta) y captura `piso1_plantilla.png`. 626 EditMode. Revisión de Codex: sin bugs graves; se hizo `Build()` idempotente.
- Pendiente de probar a mano: que el rayo apunte bien con el papel en la mano y que calcar la plantilla suelte la tela.

## 2026-11-17 — Tutoriales v2, R2: ejercicios opcionales en el piso 2

- Tres estaciones **opcionales** (no traban la bajada; la pila sigue siendo lo que se despeja): marco donde dejar un lienzo **flotando** (`E` dos veces), marco donde dejarlo **de canto** (girándolo) y una **diana** para arrojar una caja con `Q`. Cada una enciende un farolito; el rearme del tutorial los apaga.
- Mesita con un lienzo de práctica (copia de `PaperCanvas`) y cartel «Opcional» que explica el gesto.
- Nuevo `TutorialExercise` + `TutorialExerciseRules` (reglas puras, 3 tests EditMode → 626). Smoke: +1 chequeo (3 ejercicios, se cumplen, rearme).
- `Note()` acepta tamaño de letra máximo y reduce el mínimo para que los textos largos no se salgan de la tarjeta.

## 2026-11-17 — Tutoriales v2, R1: la terraza ya no se saltea

- **Bug:** la viga y el muro nacían en el centro de la terraza y se les daba la vuelta por el medio. Ahora cruzan de borde a borde (del parapeto a la baranda del hueco, pasando por el centro): hay que agacharse bajo la viga y saltar el muro.
- **Reja magica** (`TerraceGate`) tapa la rampa entre las barandas y se apaga cuando `TerraceRules` detecta las dos acciones (agachado bajo la viga, en el aire sobre el muro). Dos faroles testigo se encienden al cumplir cada una; `Begin()` la vuelve a poner.
- **Carteles** en orden y antes de lo que explican, mas grandes y con numero de paso («1 · Caminar», «2 · Agacharse», «3 · Saltar», «4 · Bajar»). `Note()` acepta ancho/alto.
- `PlayerMovement.IsLow` / `IsGrounded` publicos; `TerraceRules` (puro) + 5 tests; `HarnessTerraceReach` (inunda la terraza con una capsula de jugador: solo agachado, solo saltando o sin reja la bajada no se alcanza como corresponde). 623 EditMode y smoke en verde (+1 chequeo). Sin probar a mano.

## 2026-11-17 — Prueba con hechizos reales de la tela y las trabas, y notas sin borde violeta

- Nueva herramienta `HarnessSpellTest` (`SpellSimulator/Harness/Run Spell Test (torre)`, resultado en `Logs/harness-spelltest.txt`): pone el papel del banco del laboratorio frente a la tela y a cada traba y lanza recetas reales con `SpellCaster.Cast`.
- Resultado: **gust** suelta tela y pestillo de 0,8 a 1,8 m (~0,5 s); **updraft, water_jet, wind_breath y gust** levantan el contrapeso; **earth_spike / stone_wall / thrown_rock con Crush** rompen la piedra (hasta 2,5 m). **wind_fan y wind_breath no mueven la tela ni el pestillo** (su flujo es más suave).
- Bug corregido: `TutorialCloth.Rearm` movía el transform pero no el cuerpo físico, y la tela rearmada se volvía a soltar sola. Ahora mueve también el `Rigidbody` y sincroniza.
- Ajuste de umbrales: tela 18 kg (abre a 0,3 m o 1,0 m/s), pestillo 2,5 kg.
- Notas flotantes: sin borde violeta; tarjeta crema con marco fino de madera.

## 2026-11-17 — Torre-tutorial T4: la planta baja (abrir la puerta)

- `TowerDoor.Locked/Lock/Unlock`: con el tutorial activo la puerta arranca cerrada y trabada (E no hace nada, «Puerta trabada»). `TowerTutorial.Begin` la traba y rearma las trabas; `Skip` la abre de golpe.
- Tres trabas en el muro al este de la puerta (`MapTutorial.DoorLocks`): **pestillo** (barra con resorte que corre el viento o un golpe), **contrapeso** (bloque colgado que hay que subir 0,7 m: columna de agua o de aire; `TutorialCloth` con `liftOnly`) y **piedra** (`Breakable`: la tierra con Crush la rompe, Wall Breaker). Cualquiera destraba y abre la puerta con su giro.
- Se quitó el estante de 292° de la planta baja (ahí van las trabas). Dos notas: «puerta trabada» y «viento · agua · tierra».
- Smoke nuevo (trabada, E ignorada, cada traba la destraba, rearme, salteo abre). 618 EditMode y smoke en verde. Sin medir con hechizos reales.

## 2026-11-17 — Torre-tutorial T3: el piso 1 (objetos mágicos)

- `TutorialCloth` + `ClothRules` (lógica pura + 5 tests): tela pesada (cuerpo con resorte, 40 kg) que tapa la bajada del piso 1. Se suelta, vuela y desaparece cuando algo que no es el jugador la aparta (viento dibujado en papel, pared o piso; ≥ 0,55 m de su lugar o ≥ 2,2 m/s) o la golpea un objeto lanzado (impulso ≥ 6 N·s). El jugador no la mueve (para él es un muro). `TowerTutorial.Begin` la rearma.
- `MapTutorial.MagicFloor`: misma baranda del hueco que el piso 2 (`HoleRail`, compartida), la tela con su viga y 3 notas flotantes («pluma + tinta», «E leer el libro», «círculo + viento»).
- Libro nuevo `wind-primer` «Tomo I: el viento» (`glifos.json`, primero de la lista: círculo mágico, viento y columna) sobre la mesa central del piso 1. Pluma, tinta, papel (mesa de estudio, mesa de dibujo, caballete) ya estaban.
- `HarnessPlaySmoke`: chequeo nuevo (tela, libro, liberación y rearme). 617 EditMode y smoke en verde. Sin probar a mano: el umbral de apertura por viento real no está medido.

## 2026-11-17 — (EN CURSO, sin commitear) Rampas alineadas y notas flotantes

- Todas las rampas de la torre salen del mismo ángulo (`RampStart 0`): descanso de 90° entre una y otra.
- Los carteles del tutorial pasan a notas flotantes (`FloatingNote`) con 2-3 palabras; reaccionan a empujes y viento.
- Sin compilar ni verificar: ver «Traspaso» en `Docs/MAPA_FLUJO_PLAN.md`.

## 2026-11-17 — Torre-tutorial T2: el piso 2 (mover objetos)

- `MapTutorial`: baranda en el borde del hueco de la rampa del piso 2, pila de dos cajas grandes, dos chicas y un barril que tapa el descanso, y carteles «4 · Mover objetos» y «5 · El paso está tapado». `TowerTutorial.Begin` devuelve la pila a su lugar al rehacer el tutorial.
- `HarnessPlaySmoke`: chequeo nuevo (la pila tapa, hay baranda, se despeja y se rearma). 612 EditMode y smoke en verde.

## 2026-11-17 — Torre nueva (silueta) y torre-tutorial T1: la terraza

- `TowerBuilder`: la torre es más ancha abajo y arriba que en el medio y más alta. Talud de piedra en la base (hasta 7,4 m, con zaguán en la puerta), terraza en voladizo de 7 m sobre ménsulas, 10 postes sobre el muro y techo cónico más alto. El interior no cambia.
- Torre-tutorial, nivel 1: `World/Map/TutorialRules` + `TutorialProgress` (lógica pura, `TutorialRulesTests`), `World/TowerTutorial` y `TutorialSkipLever`, generador `Editor/Map/MapTutorial`. Barrera mágica que encierra la torre mientras el tutorial está activo, palanca «Saltear el tutorial» en la terraza, tres carteles de movilidad, un murete para saltar, una viga para pasar agachado y un farol por nivel.
- `PlayerSpawn.Begin` arranca o corta el tutorial; el punto de inicio de la terraza se movió al norte del pedestal. `HarnessPlaySmoke`: chequeo nuevo de barrera y palanca.
- [GAME_DESIGN.md](GAME_DESIGN.md): «dejar flotando» es **E** (decía T). [MAPA_FLUJO_PLAN.md](MAPA_FLUJO_PLAN.md): D29, D30 y tracker T1-T4.

## 2026-11-16 — Mapa por flujo, paso 2b: caminos completos

- `ValleyShape.Paths`: 10 tramos (antes 3). Columna torre→Nexo→Archivo con curva por detrás del entrenamiento, ramas al borde de los cuatro campos, arco fuego→tierra por el sur y camino torre→norte hacia la cabaña y el puesto. Los árboles ya no crecen sobre esos caminos.
- `MapStructures`: el peñasco 3 se muda de (−6, −40) a (47, −27), el sitio viejo del laboratorio (estaba sobre el futuro Archivo). `StructureSitesPinTests` actualizado.
- `ValleyShapeTests`: 2 tests nuevos de caminos. EditMode y smoke en verde.
- [MAPA_FLUJO_PLAN.md](MAPA_FLUJO_PLAN.md): D28 (el jugador recibe daño en modo Libre, con interruptor desbloqueable) y paso V nuevo.

## 2026-11-16 — Mapa por flujo, paso 2: el entrenamiento se muda al sur del Nexo

- `ValleyShape.TrainingOrigin` pasa de (16, −46) a (−6, −25) y `TrainingForward` a rumbo 225° (suroeste): queda a 20 m del Nexo, con las mesas del lado del camino al Archivo y los tiros hacia el hueco entre los futuros campos de agua y fuego. `WorldZones.json` con la posición nueva; escena regenerada.
- 604 tests EditMode y smoke en verde. Detalle y motivos en la bitácora de [MAPA_FLUJO_PLAN.md](MAPA_FLUJO_PLAN.md).
- El rework del mapa pasa a la rama `rework-mapa` (se pushea después de cada commit); `main` no se toca hasta mergear.

## 2026-11-16 — Mapa por flujo, paso N: Nexo, reaparición y pregunta para saltear el tutorial

- Nuevo `World/Map/SpawnRules` (lógica pura + `SpawnRulesTests`): el tutorial empieza en la terraza de la torre, el salteo y la reaparición llevan al Nexo (0, −6). Nuevo `Player/PlayerSpawn` en el jugador: `Begin(skip)`, `Respawn()` y vuelta al Nexo si se cae del mundo.
- Menú: al tocar «Modo Sandbox» por primera vez en la sesión se pregunta «¿Saltear el tutorial?» (`UI/MainMenu.Tutorial.cs`; teclado, mando y mouse). El harness, la red y VR entran como antes.
- El tablero del mapa sale de la pared de la torre y queda sobre dos postes junto al Nexo; «Vos estás aquí» apunta al Nexo.
- `HarnessPlaySmoke`: 2 chequeos nuevos (terraza/nexo/reaparición y tablero junto al Nexo). 604 tests EditMode en verde.
- [MAPA_FLUJO_PLAN.md](MAPA_FLUJO_PLAN.md): Tamara aprobó la torre-tutorial, el Nexo y el Archivo (D25), el circuito exterior (D26) y los faroles/estandartes que responden al progreso (D27). [GAME_DESIGN.md](GAME_DESIGN.md) pendiente de actualizar al cerrar la torre-tutorial (paso 12).

## 2026-11-16 — Mapa por flujo, paso 1: el laboratorio se muda a la pradera

- `MapStructures`: el laboratorio queda fijo en (20, −8), a 20 m del nexo, con la pista hacia el este (`LaboratorySite`, `LaboratoryFacing`). Se quitaron `LaboratoryFront` y `TrackClear` (elegían el frente; con el sitio fijo ya no hacen falta). `ValleyShape.Paths`: el camino del nexo termina en el borde de la plataforma.
- Tests: `StructureSitesPinTests` (sitio nuevo, pista libre de caminos y estructuras, camino que llega a la plataforma) y `TrainingLayoutTests`. 599 EditMode en verde. `WorldZones.json` con la posición nueva. Escena y mallas regeneradas con `MapBuilder.Build()`.
- [MAPA_FLUJO_PLAN.md](MAPA_FLUJO_PLAN.md) **v3**: correcciones de Tamara (D19-D24): la torre pasa a ser el tutorial, el spawn pasa a ser el Nexo, pregunta para saltear el tutorial, duelos sin prioridad; propuestas §17 (torre-tutorial y Nexo) y §18 (Archivo) pendientes de su sí. Dibujo `Docs/Capturas/mapa_propuesto.svg` actualizado.

## 2026-11-16 — Mapa por flujo, paso 0: posiciones medidas y dibujo del mapa propuesto (solo documentación)

- Medidos en Unity los sitios reales, el desnivel de cada objetivo v2 y los objetos de práctica con posición fija; resultados en la bitácora de [MAPA_FLUJO_PLAN.md](MAPA_FLUJO_PLAN.md). Todos los sitios nuevos son llanos; los pinos son inflamables; dos objetos de `Path9` pisan la pista futura del laboratorio.
- Nuevas capturas: `Docs/Capturas/mapa_antes_aerea.png` y `Docs/Capturas/mapa_propuesto.svg` (vista desde arriba para que Tamara corrija antes de mover nada).
- Sin cambios de código, escenas ni datos.

## 2026-11-16 — Plan de reorganización del mapa por flujo de aprendizaje (solo documentación)

- Nuevo [MAPA_FLUJO_PLAN.md](MAPA_FLUJO_PLAN.md): plan aprobado por Tamara (torre → entrenamiento → Archivo → 4 campos de elemento → late game; laboratorio en la pradera), con tracker de pasos, protocolo «avanzá con el mapa», coordenadas v2, reparto de libros, riesgos y tabla de agentes/modelos. Junto con [MAPA_FLUJO_CRITICA.md](MAPA_FLUJO_CRITICA.md) (revisión externa) y [MAPA_REORGANIZACION.md](MAPA_REORGANIZACION.md) (diagnóstico de distancias).
- Sin cambios de código, escenas ni datos. `ROADMAP.md` («Siguiente») y `CLAUDE.md` apuntan al plan.

## 2026-11-16 — Siete hechizos de la wiki de Witch Hat Atelier

- Nuevos en `Data/Spells/`: `pyreball`, `ring_of_fire`, `flame_shot`, `wall_bend`, `grasping_wind`, `wind_wall` y `water_orb` (este usa el signo orb). Composición tomada de las páginas de la wiki (telepedia); ángulos, dificultad y textos son adaptación propia, y las regiones de `flame_shot` se simplificaron (la wiki muestra grupos de cinco). Sumados a los libros `spells-fire-earth`, `spells-wind-water` y `spells-water-deep` en `Lore/glifos.json`.
- Sin hechizos nuevos con cool, entwine, focus ni gather: la wiki no muestra una composición clara que los use.

## 2026-11-16 — Seis signos sin efecto: cool, empower, entwine, focus, gather, orb

- Entradas JSON nuevas en `Data/Dictionary/signs/13..18` (trazo derivado de los SVG del repo base `wha-spell-simulator`; significado de la wiki de Witch Hat Atelier, *Signs Explained*). `GlyphDictionary.asset` regenerado: las 6 clases del modelo ya se reconocen y cuentan en el plan.
- `Manifestations`/`PlanResolver`/`SpellPlan` suman `Cool, Empower, Entwine, Target (focus), Gather, Orb`; `MagicField` los ejecuta (`ApplyCool/Entwine/Gather/Orb/Target` y Empower como multiplicador de caudal). Los valores numéricos son **interpretación propia**, sin medir a mano.
- Test: `SignEffectsTests.LosSeisSignosDeLaWikiQuedanEnElPlan`.

## 2026-11-16 — Laboratorio v6 (parte B): alas grandes sin paginas, atril invertido, fogata lejos y estacas legibles

- Tamara probó el v5 en VR: paneles chicos, muy bajos, con demasiadas subdivisiones. Plan en `LABORATORIO_V6_PLAN.md` (parte B; la parte A, etiquetas por mirada, es de `Scripts/Lab`). **Sin compilar ni probar** (lo compila el agente principal).
- **Dos alas grandes** (`LabBenchLayout`, `MapLab`): ~0,96 × 1 m, casi verticales (65°), borde de abajo a 0,94 m y de arriba a 1,85 m, a ~0,92 m del punto de pie y giradas 30° hacia adentro; solo un pie fino debajo (poste + base). **Sin páginas ni pestañas** (se dejó de construir `LabPageSet`/`LabPageTab`; los tipos siguen): izquierda el sello (grillas de sigilos y signos lado a lado + tres filas con título: «Hechizo y signo», «Forma y tamaño», «Tinta y disparo»), derecha el campo (casillas P/1–4 + dos filas de tres botones, «Objeto elegido», «Atajo y papel», «Viento y cajas», «Fuego»). 14 + 13 controles, 5 columnas (paso 0,19 m, plaquita 0,15 m), colliders de 12 cm. De más importante arriba a menos abajo.
- **Atril invertido** (inclinado 60°, desde 0,95 m): el lienzo arriba (centro a ~1,43 m), debajo LANZAR / «Borrar dibujo» / «Reiniciar sello» y la pantalla «Vas a lanzar» abajo de todo. Tab sigue abriendo la vista completa.
- **Fuera de la construcción:** la pizarra «Mediciones» (`LabLensCounters`), la banda verde de resultado (`LabLensResult`) y la lectura del atril (`LabLensPad`); `LabBenchDisplay` los tolera nulos.
- **Fogata** (anillo de piedras) a (−14,5; 11,5) del marco del laboratorio: ≥ 8 m del borde de la pista y de la plataforma (se avisa si no); el ancla del fuego queda dentro de la pista sin decorado; la limpieza de vegetación incluye el claro de la fogata.
- **Estacas de distancia** (`Lab_Stake_v6`): chapa crema de 40 × 22 cm con marco, número oscuro de ~15 cm de cuerpo (≥ 10 cm de mayúscula) a 6 mm delante de la chapa, cada estaca girada hacia la zona de pie.
- `LabPanelLayout`: `MinControlPitch` 0,22 → 0,19 y `PlaqueWidth` 0,18 → 0,15. `LabBenchLayout` reescrito (alcance del centro ≤ 0,9 m; de las alas ≤ 1,5 m, porque un ala de 0,96 m a 0,9 m del jugador no cabe en 0,9 m). `LabV5Tests` reescrito para el nuevo diseño; `HarnessPlaySmoke`: el chequeo de páginas pasa a «sin páginas, 14 + 13 controles» y se quitó el de las placas fijas con lentes.
- Docs: `GAME_DESIGN.md` (Laboratorio), `KNOWN_ISSUES.md` (KI-140).

## 2026-11-15 — Mapa de la torre v5: vista completa con Tab y sin viaje

- Tamara probó el v4 en VR. Plan en `LABORATORIO_V5_PLAN.md` (sección B). Se reusa el sistema de vista completa que ya existía en vez de uno propio del mapa.
- **Tab = vista completa, como cualquier superficie.** `TowerMap` pasa a ser un `IFocusPanel` (interfaz nueva en `InteractionInterfaces.cs`); apuntarlo muestra «Tab: Vista completa» y Tab llama a `SurfaceFocus.ShowPanel(centro, normal, ancho, alto)`, que encuadra el cuadro entero de frente (misma cámara fija, mismo `FocusMode.EnterCanvasView`) con `SurfaceFocus.FitDistance` (lógica pura, la usa también el lienzo). Tab, V o Esc cierran. En VR no hay vista completa: se apunta con el rayo en la pared.
- Dentro de esa vista el cursor toca los marcadores: `PlayerInteractor.HandlePanelHover` apunta solo a lo del propio cuadro y `HandleClickPress` deja pasar el clic únicamente en el panel (el modo dibujo sigue bloqueado y ninguna herramienta actúa).
- **Eliminado todo lo de la v4:** tecla M / botón X de VR (`IsTogglingMap`), `FocusMode.PanelView*`, el cuadro que «se rotaba y venía a la cámara» (`TowerMap.OpenView/CloseView`, `MapProjection.Fullscreen*` y `FitScale`), el clic en el cuadro para abrirlo y el manejo especial de Esc.
- **El mapa no teletransporta:** se quitó `TowerMap.Travel` y el evento `TraveledFromMap`. Los rombos de portal siguen como información (resaltan su fila de la leyenda; «Portales (cruzalos para viajar)»). `Portal.TeleportLocalPlayerHere` queda para el cruce normal.
- Tests: `ElMapa_NoTeletransporta`, `ElMapa_EsUnPanelEnfocable_ConElCuadroEntero`, `SurfaceFocus_FitDistance_EncuadraSinSalirse` (reemplaza `FitScale`). `HarnessPlaySmoke`: «presionar los rombos no teletransporta» y «Tab abre la vista completa de frente» reemplazan el chequeo de portales.
- Docs: `GAME_DESIGN.md` (controles), `KNOWN_ISSUES.md` (KI-135), `GrimoireTexts` (Tab en lugar de M/X).

## 2026-11-15 — Laboratorio v5: atril = lienzo normal, vista previa viva, consola frontal y cartel de la entrada

- Tamara probó el v4 en VR. Plan en `LABORATORIO_V5_PLAN.md` (sección A). Regla: reusar lo que ya existe.
- **Atril = lienzo normal** (`LabSketchPad` reescrito): un `DrawingCanvas` (clon de PaperCanvas, collider sólido, sin `PhysicalCanvas`) bajo un `ISealGate` siempre bloqueado. Se dibuja con la lapicera equipada (`PhysicalTool`: mismo input, trazo, estabilizador, deshacer y goma) y Tab abre su vista completa (`SurfaceFocus.ShowCanvas`) como cualquier lienzo. Se borró el dibujo propio (`IManipulable`, `PenTool` y cursor propios). Gasta tinta y se replica en red como cualquier lienzo (KI-138).
- **Vista previa viva**: sin dibujo a mano, el sello de los controles se dibuja en el atril (`LabSketchPad.ShowPreview`, trazos con dueño `LabSketchPreview.PreviewOwner`) y en el papel del campo; el primer trazo a mano saca la vista previa (`LabSketchPreview`, puro) y «Borrar dibujo» la repone. LANZAR lanza lo que haya.
- **Consola frontal baja y abierta** (`LabBenchLayout` reescrito, `MapLab`): tres paneles contiguos a 40°, borde de abajo 0,8 m y de arriba ≤ 1,25 m, gabinete bajo y marco fino de latón; izquierda el sello y derecha campo/entorno, cada uno en 4 páginas con pestañas (`LabPageSet`, `LabPageTab`); centro botones, atril y pantalla. Todo a ≤ 0,7 m y ≤ 90° del frente (los laterales giran ±63°: en 100° no entraban, KI-138).
- **Pantalla «Vas a lanzar»** sobre el atril (pergamino en marco de madera): hechizo grande + 4 datos (`LabSealSummary.ScreenRows`), letra ≥ 2,2 cm a 0,89 m del ojo, texto a 6 mm. Con dibujo a mano dice «Tu dibujo del atril». Banda de resultado, lectura del atril y mediciones (`LabLensCounters`, antes en el pizarrón) siguen solo con lentes.
- **Cartel de la entrada**: el pizarrón verde se cambió por `CanvasDesigns.Sign` (el cartel de madera de todo el mundo) de cara a la torre, con los 4 pasos.
- Tests: `LabV5Tests` (alcance, alturas, contigüidad, pestañas, pantalla legible, vista previa); se quitaron los de banco de `LabV4Tests`. `HarnessPlaySmoke`: atril como lienzo normal (lapicera lo ve, Tab, no se lanza solo), vista previa en atril y campo, el primer trazo la limpia, «Borrar dibujo» la repone, páginas, cartel hacia la torre; controles enlazados contando páginas ocultas.
- **Sin compilar ni probar** (lo hace el agente principal). Docs: `GAME_DESIGN.md`, `STRUCTURE.md`, `KNOWN_ISSUES.md` (KI-138).

## 2026-11-15 — Laboratorio v4: banco al alcance, objetos del campo, atril de dibujo y datos con lentes

- Tamara probó el v3 en VR (12 puntos). Plan y decisiones en `LABORATORIO_V4_PLAN.md`.
- Banco compacto (`LabBenchLayout`): mesa baja adelante y cuatro paneles casi verticales en arco; todo control a ≤ 0,7 m de donde se para el jugador, profundidad útil 0,24 m. `MinControlPitch` 0,30 → 0,22 m.
- Paneles con título y ayuda que dicen qué hacer; lo importante arriba; sigilos y signos como grillas de iconos generados de las plantillas (`LabGlyphGrid`, `LabGlyphIcons`).
- Objetos del campo (`LabField`, `LabFieldSlot`): papel de prueba con posición libre (variables 19–22, inclinación −90..90, atajos «Frente/Piso/Izquierda…»), hasta 4 cajas, aro en el elegido, borrar elegido/todo; viento ajustable en vivo; masa a la caja elegida.
- Atril de dibujo (`LabSketchPad`): se dibuja a mano con el rayo; LANZAR usa el dibujo o, vacío, el sello de los controles (sin tinta ni auto-lanzamiento).
- Cartel «Vas a lanzar» nuevo (pergamino, filas con color de grupo); «↺ Reiniciar sello» / «↺ Reiniciar entorno» con icono y ayuda; 3 luces + faroles.
- Datos de reconocimiento solo con lentes, cerca y mirando (`LabLensVisibility`, `LabLensGate`): banda de resultado, lectura del atril, contadores.
- Tests: `LabV4Tests` (lentes, alcance, iconos), `LabSealBenchTests` (pose libre, atajos, cartel, grupos) y `LabPanelLayoutTests`. `HarnessPlaySmoke`: atajos, pose libre en el piso, dibujo vs. sintético, 4 cajas y mover la elegida, datos con lentes.
- **Sin compilar ni probar todavía** (KI-137). Docs: `GAME_DESIGN.md`, `STRUCTURE.md`, `KNOWN_ISSUES.md`.

## 2026-11-15 — Mapa de la torre: leyenda nueva, pantalla completa, portales y zonas reubicadas

- Pedido de Tamara tras probar el mapa: leyenda poco llamativa, sin pantalla completa, sin portales y el entrenamiento en otro lugar.
- Leyenda rediseñada (`MapTowerMap.BuildLegend`, medidas en `MapProjection.Legend*`): una fila por zona y por portal con chapa del color del marcador (con su número; rombo en los portales), nombre en negrita casi negra ~50 % más grande, título «Portales» y barra dorada con filete rojo en la fila apuntada o marcada. Capas sin texto coplanar (barra 2 mm, chapa 4 mm, texto 6 mm encima), sin sombras.
- Pantalla completa: apuntando el cuadro, **M** (PC) o **X izquierdo** (VR), o clic/gatillo en el cuadro. PC: el cuadro se trae a 0,5 m de la cámara escalado para llenar la vista (`MapProjection.FitScale`); VR: panel vertical a 0,8 m frente a la cabeza. Se cierra con M/X, Esc o alejándose 1,5 m. Tab no se usó: ya es la vista completa del lienzo. Esc no abre el grimorio ni suelta el cursor mientras el mapa está abierto (`FocusMode.PanelView`). El cuadro dejó de ser estático.
- Portales en el mapa (`WorldZones.json` → `portals`, los 4 de `portals.json`): rombo con el color de cada portal, corrido arriba a la derecha del marcador de su estructura. Presionarlo lleva al jugador a la llegada del portal con el mismo teletransporte del cruce (`Portal.TeleportLocalPlayerHere`, refactor de `NetTeleportLocal`); evento `TowerMap.TraveledFromMap`.
- Entrenamiento movido a (16, −46) (`ValleyShape.TrainingOrigin`; aplanado, `ClearVegetation`, `TrackClear` y el catálogo lo siguen solos). La cabaña 1 (ahora «Cabaña del norte», id `cabin-north`) y el puesto de suministros se mudaron al norte: `MapStructures.CabinNorthPreferred` (−8, 46) y `SupplyStallPreferred` (10, 48). El laboratorio no se movió.
- Tests: `TowerMapTests` (portales en `portals.json`, rombos sin tapar marcadores, `SetupPortals`, leyenda dentro del pergamino, texto no coplanar, `FitScale`), `StructureSitesPinTests` (sitios nuevos, al norte y separados de sus vecinos; entrenamiento a > 12 m del laboratorio). `HarnessPlaySmoke`: un rombo por portal con su portal en la escena.
- Docs: `GAME_DESIGN.md` (controles: M / X), `GrimoireTexts` (Controles), `KNOWN_ISSUES.md` (KI-135).

## 2026-11-15 — Laboratorio v3: texto legible, posiciones del papel, entrenamiento reubicado y mapa en la torre

- Tamara probó el v2: textos ilegibles (z-fighting entre rótulo y franja de color), papel solo hacia un lado, confuso de usar, laboratorio pegado al entrenamiento base. Plan en `LABORATORIO_V3_PLAN.md`.
- Texto: nada coplanar (`LabPanelLayout`: texto 6 mm sobre la superficie, plaquita crema + ventanita oscura, tamaños fijos, rótulos ≤ 10 caracteres, 0,30 m entre controles, panel a 35°).
- Papel: variable `PaperPose` con 9 posiciones (atril hacia la pista/el jugador/arriba, piso, pista hacia izquierda/derecha/banco/piso, libre) vía `LabSealBenchMath.PaperPose`; el atril muestra un espejo con flecha cuando el papel está en otro lado.
- Uso: variables en 4 grupos numerados con ayuda en el mundo (`LabHelpStrip`), pizarra «Vas a lanzar» (`LabSealSummary`), banda de resultado, botón LANZAR grande, muescas de rango/default y reinicio por grupo. Se conservan todas las variables.
- Entrenamiento base reubicado junto a la cabaña de enfrente de la puerta de la torre (`ValleyShape.TrainingOrigin` (23, −45), `TrainingLayout`); `MapLayout.DropEntries` descarta la pose vieja del papel gigante.
- Mapa de zonas como cuadro en la pared de la torre (`TowerMap`, `MapZoneMarker`, `MapProjection`, `Data/WorldZones.json`).
- Verificado: compila, 569 tests EditMode y `HarnessPlaySmoke` en PASS (9/9 poses lanzan válido, 14 marcadores = zonas, entrenamiento a más de 12 m del banco). Sin probar a mano ni en VR.

## 2026-11-15 — Laboratorio v2: simulador de sellos, controles agarrables, viento y área nueva

- Tamara probó el v1: gafas con lag, mesa chica y fea, mal ubicada, variables poco útiles, solo −/+ y viento pobre. Plan en `LABORATORIO_V2_PLAN.md`.
- Variables = las que se controlan dibujando: `LabSealSpec` + `LabSealSynth` (trazos sintéticos reproducibles por semilla) + `LabSealBench` (lienzo de prueba, `Launch()` por `SpellCaster.Cast`). 18 variables (receta, sigilo, papel, anillo, hueco, signos, ángulo, largo, asimetría, temblor, tinta, cara, inclinación, pluma). Se conservan masa, viento y fuego como entorno; se quitan caudal de agua y multiplicador de fuerza.
- Controles: `IManipulable` + `LabControl` (perilla, deslizador, selector con muescas) arrastrables con mouse y con muñeca/mano en VR; `LabDial` y `LabStepPlate` eliminados. `LabControlBinding` los enlaza a las variables.
- Lag de gafas: precalentado de etiquetas/líneas y shaders (`ShaderWarmup`), máx. 8 etiquetas nuevas por refresco.
- Viento: `LabWindVisual` + `WindVisualMath` (estelas, hojas, polvo, chevrones, manga con banderines).
- Área nueva en la pradera sureste (~(47, −27)): plataforma, toldo, banco en U con consolas de sello y de entorno, pista con estacas, manga y fogón. `MapStructures` agrega el sitio «Laboratory» al final.
- Verificado: compila, 534 tests EditMode y `HarnessPlaySmoke` en PASS (19/19 recetas compilan con su elemento, 24/24 controles enlazados). Sin probar a mano ni en VR.

## 2026-11-15 — Laboratorio: mesa de variables, lentes de tinta y registro CSV (fases 0, 1, 2a y 2b)

- Módulo nuevo `Scripts/Lab/` (namespace `SpellSimulator.Lab`; nadie depende de él): `LabParameters` (valores de la mesa, `ValueChanged`, «por defecto»), `LabDial` + `LabStepPlate` (perillas con placas −/+ apuntando y con clic o gatillo), `LabMaterializeButton` + `LabSpawner` (caja con la masa elegida, zona de viento `LabWindZone`, parche de fuego), `LabResetButton`, `InkLenses` + `LabOverlay` (las lentes muestran en el mundo masa, velocidad, viento, generación del fuego y parámetros del hechizo), `LabMeasurements` (distancia de la caja, área quemada, alcance y duración del hechizo), `LabSessionLog` + `LabCsvWriter` (registro CSV de la sesión), `LabCounters`; lógica pura `LabVariableDef`/`LabCatalog`, `LabParameterMath`, `LabCsv`, `LabMetricsMath`, `LabOverlayMath` con 4 archivos de test.
- API genérica en módulos existentes: `Movable.All/Mass/Body`, `GrassFire.SetTuning/SampleBurning/IsFireproofAt` y getters, y `PlayerInteractor` ya no «usa» la herramienta de la mano al apretar una placa con clic.
- Generador: `Editor/Map/MapLab.cs` arma la mesa en `Map/Lab` (mesa de estampas + 5,5 m al este, mundo ~(35.5, −16.5)), con nombres estables para `MapLayout`, anclas de caja/viento delante (~(40.5, −15)) y fuego a ~10 m sobre pasto (~(45, −20)). `MapStructures` reserva la zona (`LabArea`, sin mover ningún sitio actual) y `FlammableSetup` excluye lo `Lab*`. **Hay que correr `MapBuilder.Build()`.**
- Harness: bloque «Laboratorio» en `HarnessPlaySmoke` (perilla 10 → 25 kg, cajas de 25 y 600 kg, viento, reinicio que borra y conserva valores, `SetTuning` y «por defecto», ≥ 4 filas en `Logs/harness-lab-registro.csv`, lentes); el smoke ahora corre varios frames.
- Docs: GAME_DESIGN, ARCHITECTURE, STRUCTURE, DECISIONS (ADR-038), KNOWN_ISSUES (KI-125 a KI-129), HARNESS, LABORATORIO_PLAN.


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
