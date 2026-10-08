---
tags: [doc, adr]
---
# Decisiones (ADR)

Formato corto: contexto → decisión → consecuencias. Numeración incremental, nunca reutilizar.

## ADR-001 — Reconocimiento con el modelo de wha-spell-simulator
**2026-08 (retroactivo).** Se reutiliza el ResNet18 multi-head 96×96 entrenado en la referencia (8k+ muestras),
corriendo local con Unity Inference Engine (`GPUCompute`). No se entrena en Unity. El rasterizado de Unity debe
imitar `renderCandidateTensor` de la referencia (fondo -1, tinta +1, bbox con margen). Ver [[REFERENCE]], [[GLYPHS]].

## ADR-002 — PC y VR en paralelo, una sola escena
**2026-10-07.** Ambas plataformas se desarrollan a la vez. Un único `Player` prefab con `PCRIG` y `VRRIG`;
`PlatformDetector` activa uno al inicio y expone `IsVR`. Es el único punto con lógica de plataforma.
Consecuencia: toda feature se diseña con input abstracto (`PlayerInputReader`, `IUsable`) y se prueba en ambos.

## ADR-003 — Input por código, sin `.inputactions`
**2026-08 (retroactivo).** `PlayerInputReader` crea las `InputAction` en `Awake` con bindings PC/Gamepad/XR.
Facilita agregar bindings XR sin tocar assets. `InputSystem_Actions.inputactions` queda sin uso.

## ADR-004 — Cada lienzo es dueño de su `DrawingSession`
**2026-09 (retroactivo).** `PhysicalTool` usa `hitCanvas.Session`; permite varios lienzos independientes.
`MouseInputHandler` (legacy) mantiene una sesión propia; ver KI-014.

## ADR-005 — Harness contra el Editor vivo vía Unity MCP
**2026-10-07.** El Editor suele estar abierto, así que la verificación no usa batchmode. Se agregan asmdefs
(`SpellSimulator.Runtime/Editor/Tests.EditMode`), un runner que escribe `Logs/harness-editmode.txt` y un validador
de escena, todos invocables por menú o `Unity_RunCommand`. Ver [[HARNESS]].

## ADR-006 — Documentación como vault de Obsidian en `Docs/`
**2026-10-07.** Docs en Markdown con wikilinks dentro de `Docs/` (fuera de `Assets/`, Unity no la importa).
Graphify genera `Docs/Graph/` (ignorado en git, regenerable). `CLAUDE.md` en la raíz es el punto de entrada para agentes.

## ADR-007 — Port fiel del compilador y diccionario como datos
**2026-10-07.** El compilador de Unity era una versión propia con valores distintos. Se porta fielmente
`spellBuilder.ts`, `semanticRules.ts`, `spellQuality.ts` y `spellDirection.ts`, y el significado de cada glifo pasa a ser
**datos**: los JSON de la referencia se copian a `Data/Dictionary/` y se cargan vía `GlyphDictionaryAsset`. Consecuencia:
cambiar o agregar un glifo no toca código, y un `git pull` de la referencia + "Sync From Reference" trae sus ajustes.
La capa Plan queda para F4.

## ADR-008 — Modelo GlyphStudent con TTA y softmax
**2026-10-07.** Se reemplaza el ResNet18 (22 MB) por el GlyphStudent destilado (0.9 MB, misma I/O). Se infieren todos
los candidatos × 8 rotaciones en un solo batch y se promedia el softmax (como la referencia). Se elimina la rotación manual
de signos. Si en Quest es caro, `useRotationTta = false` reduce el costo 8×.

## ADR-009 — Reglas de la referencia: anillo obligatorio y activación por cierre
**2026-10-07.** Sin anillo el hechizo es inválido (antes se casteaba el sigilo suelto). Anillo cerrado = activo,
abierto = preparado (válido, no se lanza). Habilita la mecánica de "sellar" un hechizo preparado.

## ADR-010 — Pipeline en módulos puros detrás de `SpellCaster`
**2026-10-07.** `Parsing` → `Recognition` → `Glyphs` → `Compiler` son lógica pura (salvo el clasificador, detrás de
`IGlyphClassifier`). `SpellCaster` es el único punto de entrada y emite `SpellCompiled` para que sandbox, hápticos y
sonido se enganchen sin acoplarse. `MouseInputHandler` y el HUD de botones se eliminan: PC y VR usan las herramientas físicas.

## ADR-011 — Espacio isotrópico para el reconocimiento
**2026-10-07.** Los trazos se guardan en UV del lienzo, pero el pipeline trabaja en un espacio donde el lado mayor mide 1
(`DrawingCanvas.UvToIsotropic`), para que lienzos no cuadrados no deformen anillos ni glifos.

## ADR-012 — Dos modos: Libre primero, Duelo después
**2026-10-07.** La visión final tiene dos modos: Libre (torre + pradera + lago, sandbox) y Duelo (arena, tiempo y vida).
La evaluación de Simuladores exige un modo Libre sólido; el Duelo va después, con un rival IA que juega con las mismas
reglas (pasa por `SpellCaster`) y online recién al final. Ver [[GAME_DESIGN]].

## ADR-013 — Mundo reactivo "espectacular pero acotado"
**2026-10-07.** Nada de fluidos ni terreno voxel: objetos pre-fracturados, cráteres con decals, agua estilizada con
ripples por GPU y fuego por propagación entre objetos marcados. Todo por ticks y con presupuestos, para que corra en Quest.

## ADR-014 — Estilo visual de ilustración WHA
**2026-10-07.** Toon/acuarela, líneas de tinta y paleta pastel. Fiel a la inspiración y más barato en Quest que PBR realista.

## ADR-015 — La tinta es un recurso
**2026-10-07.** En ambos modos. Cada herramienta tiene un depósito que se recarga con tinteros (objetos que se llevan
y se mueven). En PC, `R` recarga desde el tintero que tengas encima. El costo depende de la longitud del trazo en metros
y de la herramienta.

## ADR-016 — Progresión por libros, sin bloqueo de reconocimiento
**2026-10-07.** Los glifos se agregan al libro-menú al leer libros o al dibujarlos bien por primera vez. El
reconocimiento nunca se bloquea por progresión: experimentar también descubre.

## ADR-017 — Contraptions, lanzables y borrado por agua; sin magia prohibida
**2026-10-07.** Sellos en objetos con triggers (cerrar el anillo, juntar dos mitades, impacto, presión). El agua y el
fuego rompen sellos (canon). Dibujar en el propio cuerpo no se modela por ahora.

## ADR-018 — El tamaño físico del sello importa
**2026-10-07.** Por la ley extensiva del canon (`ground-truth.md` §2), el radio del anillo en metros escala el caudal y
la fuerza total, mientras que la forma de los signos define la velocidad. Justifica papeles y estampas de varios tamaños.

## ADR-019 — Partículas Shuriken + shader propio en vez de VFX Graph
**2026-10-07.** Las manifestaciones se arman con `ParticleSystem` configurados por código a partir del plan y de un
`ElementLook` (SO). El shader `SpellSimulator/SpellParticle` dibuja un disco suave procedural, aditivo o alfa, sin
texturas. Razones: VFX Graph en Quest exige compute y es más caro; Shuriken es estable en móvil y se puede generar 100%
por código (VFX Graph no). El viejo `Fire_VFX.vfx` queda sin uso.

## ADR-020 — Los papeles emiten hacia el lado opuesto a quien mira
**2026-10-07.** En el canon la magia sale por la cara con tinta, pero un papel de mano mira hacia quien dibuja, y el
hechizo le explotaría en la cara. `DrawingCanvas.EmissionSide = AwayFromViewer` en papeles (la magia sale hacia el
objetivo, como cuando los personajes giran el papel); `InkSide` queda para sellos en paredes, mesas y piedras.

## ADR-034 — Guardar hechizos: se guardan los trazos, no el `SpellIR`
**2026-10-08.** Planificación, sin código. El grimorio suma una sección **HECHIZOS** con hechizos prearmados, hechizos
aprendidos de libros de hechizos y, a futuro, hechizos dibujados por el jugador.

**Qué se guarda.** La fuente de verdad son los **trazos vectoriales del lienzo** (`DrawingSession`: puntos en UV 0..1
del anillo, con orden y grosor), normalizados al centro y radio del anillo. El `SpellIR` **no** se persiste como verdad: sólo
se cachea un resumen de lectura (elemento, forma, potencia, versión del compilador) para mostrar la ficha sin re-compilar.
Motivo: el modelo ONNX, el diccionario y el tuning cambian. Un IR guardado quedaría desfasado y salteando el reconocimiento
rompería "lo que dibujás es lo que lanzás". Si el resumen trae otra `compilerVersion`, se recalcula al abrir la página.

**Formato y persistencia.** Un único `hechizos.json` en `Application.persistentDataPath` (igual que `descubrimientos.json`),
`JsonUtility`, con `schemaVersion` y un migrador por versión. Cada entrada: `id` (GUID), `name`, `createdAt`, `origin`
(`Preset | Book | Player`), `strokes[]`, `summary`. Escritura atómica: `.tmp` y después reemplazar el archivo. Si el
archivo está corrupto, se renombra a `.bak`, se loguea por `DiagnosticBus` y se arranca vacío.
Los **prearmados y los de libros son data del proyecto** (`Assets/_Project/Data/Spells/*.json`, mismo esquema y
`origin` fijo). Al save del jugador sólo van sus hechizos y los `id` desbloqueados por libros (no se copian los trazos).

**Nombrar, editar, borrar.** "Guardar" sale del lienzo con un anillo cerrado y válido (cast en seco por el pipeline
normal, sin lanzar VFX). El nombre por defecto se arma con el resumen ("Fuego · proyectil") y se renombra desde la
página del grimorio: teclado en PC; en VR, teclado virtual del sistema o lista de nombres sugeridos si no hay. "Editar"
= cargar los trazos en un lienzo, retocar y re-guardar (sobrescribe o "guardar como"). Borrar pide confirmación en la
página. Prearmados y de libros son de sólo lectura (se pueden copiar como hechizo propio).

**Cómo se re-lanza.** Nunca un cast directo desde datos: **todo pasa por `SpellCaster.Cast(DrawingCanvas)`**. Se
reutiliza el camino de las estampas (`Stamp`/`StampPattern`, M6): el hechizo guardado se carga en una **estampa-hechizo**
que imprime los trazos en el lienzo con el anillo abierto, y el jugador lo cierra a mano para activar (mantiene la
regla de activación por cierre, ADR-009). La página del grimorio muestra un **re-dibujo animado** de los trazos (como las
páginas de glifos) sólo como vista; no lanza. Usos por carga: los mismos 5 de las estampas.

**Límites.** Máx. 32 hechizos del jugador, 64 trazos y 4096 puntos por hechizo (simplificados con `StrokeCleaner`
al guardar), nombre ≤ 24 caracteres. Al llegar al tope, "guardar" avisa en la HUD y no sobrescribe nada solo.

**PC/VR.** Los datos son iguales (UV normalizado, independiente del tamaño del lienzo y del dispositivo). Sólo cambia
la entrada del nombre y el gesto de "guardar"; la bifurcación va por `PlatformDetector.IsVR`. El archivo es portable
entre PC y Quest.

**Consecuencias.** (+) Un solo esquema para los tres orígenes; si mejora el reconocedor, mejoran los hechizos viejos.
(−) Un hechizo guardado puede leerse distinto tras cambiar el modelo: la ficha lo marca como "cambió" al detectar un
resumen distinto. (−) Re-lanzar cuesta lo mismo que dibujar con estampa (inferencia completa), aceptable.

**Fases.** H1: `SpellRecord` + `SpellLibrary` puros (serializar, validar límites, migrar) con tests EditMode.
H2: sección HECHIZOS del grimorio leyendo `Data/Spells/*.json` (prearmados, vista con re-dibujo animado).
H3: estampa-hechizo (cargar un registro en `Stamp`) + chequeo en `HarnessPlaySmoke`. H4: libros de hechizos que
desbloquean `id`. H5: guardar desde el lienzo, renombrar y borrar (PC), luego entrada de nombre en VR.

**Riesgos.** Cambios del modelo que alteran hechizos guardados (mitigado con `compilerVersion` y aviso); escalado de
trazos al estampar en lienzos de otro tamaño (el anillo manda, no el lienzo); entrada de texto en Quest; save corrupto
o escrito a medias (escritura atómica + `.bak`); alocaciones al imprimir muchos trazos (hacerlo fuera de `Update`).

## ADR-033 — Inventario de dos manos: barra de 3 slots + una mano secundaria que arrastra
**2026-10-07.** Reemplaza ADR-032 en el inventario. La mano principal (derecha o izquierda, `PlayerSettings.MainHandRight`) tiene 3
slots (1-2-3) para lo que se usa (lápiz, goma, estampas); lo agarrado con E (lienzo, caja, el grimorio) va a la mano secundaria y se
ARRASTRA en el mundo con `ObjectMover` (nunca se guarda). Así un lienzo agarrado se puede rotar, dar vuelta, dibujar y fijar a la
pantalla (V), y un cuaderno pasa de página con la rueda. La rueda sin nada agarrado cambia el grosor. La HUD es UI Toolkit
(`Scripts/UI`), temática de pergamino, con miniaturas renderizadas de cada objeto.

## ADR-032 — Dos modos de manipular: agarrar (E) y mover (G)
**2026-10-07.** Llevar un objeto en la mano (E) no permite rotarlo ni acomodarlo, y sin eso es imposible unir dos mitades de un
sello. Se separan: **E** = agarrar en la mano (herramientas, lienzos, estampas; las manos cuelgan de la cámara) y **G** = mover a
distancia estilo Garry's Mod (`ObjectMover`: rueda, rotar con clic derecho + mouse, T para dejarlo flotando). Se reorganizó todo el
mapa de teclas por grupos (movimiento, manos, mover objetos, dibujar, menú). V queda solo para el lienzo y B para el grimorio.

## ADR-030 — El fuego se propaga por una grilla de pasto y por objetos reactivos
**2026-10-07.** Quemar "la casa entera" no es simular cada brizna: el pasto es una grilla de 2 m (`GrassFire`, 70x70) con
automata celular (ardiendo, quemado, inflamable) que enciende lo reactivo cercano (pinos, mobiliario); los objetos que
arden encienden la celda de abajo. Es barato (sin allocations, llamas con pool de 36) y reutiliza `ElementReactive`.
Los pinos y el mobiliario de la torre son `ElementReactive` (madera); la piedra de la torre no arde.

## ADR-031 — Soltar = caer; flotar es una accion aparte (G)
**2026-10-07.** Reemplaza ADR-028 en lo que respecta al comportamiento por defecto: soltar un lienzo lo deja caer con fisica
(aterriza cara arriba) y **G** lo deja flotando con un vaiven suave. El grimorio pasa a ser un menu (hub) que no se cierra
al alejarse y se puede fijar a la camara con V.

## ADR-029 — Dibujar en el entorno con calcomanías, no con UV del mesh
**2026-10-07.** El piso, la torre y las cajas no tienen UV útiles ni forma plana. Al empezar un trazo sobre una
`Inscribable` se crea un lienzo transparente (quad en la capa Canvas, shader `InkDecal`) pegado a la superficie. Así el
pipeline de lectura/compilación es el mismo de siempre (`DrawingCanvas`), la calcomanía sigue al objeto si se mueve, y el
sello se rompe con las mismas reglas del papel (agua, fuego, fractura) más las marcas del suelo. Máximo 10 a la vez.

## ADR-028 — Un lienzo suelto flota o se apoya; arrojarlo es otra acción
**2026-10-07.** Caer boca abajo hacía incómodo dibujar siempre. Soltar apoya el lienzo en una superficie cercana o lo deja
flotando; la física solo entra al arrojarlo (Q) y entonces se acuesta con la cara hacia arriba. Modo de escritorio (V):
lienzo fijo a la cámara y mouse como puntero (`FocusMode`), solo en PC.

## ADR-027 — La magia sale por la cara con tinta; dar vuelta la hoja es la puntería
**2026-10-07.** Reemplaza ADR-020. Con `AwayFromViewer` el hechizo salía hacia un lado que no dependía del dibujo, algo
que se sentía arbitrario. Ahora todos los lienzos emiten por la cara con tinta (la que miraba al jugador al empezar a
dibujar) y **F** da vuelta la hoja para apuntar (en VR, girar la mano), como cuando los personajes del canon giran el papel.

## ADR-026 — El grimorio aparece en el mundo, no pegado a la cámara
**2026-10-07.** El libro-menú se abre flotando frente al jugador (fijo en el mundo) en lugar de seguir a la cámara o
vivir en la HUD: así se apunta a sus páginas con la misma mira/rayo que a cualquier objeto y funciona igual en PC y en
VR. Las acciones instantáneas (pasar página, leer, botones) son `IPressable` y usan la tecla de agarrar. Se cierra con
Tab o alejándose. En VR, más adelante, puede ir a la muñeca izquierda.

## ADR-025 — Audio por eventos, sintetizado + CC0
**2026-10-07.** Los sistemas no reproducen sonidos: publican eventos y `GameAudio` los traduce a cues de `AudioLibrary`
(datos). Así cambiar un sonido es cambiar un clip, y el audio se puede apagar o reemplazar sin tocar la lógica. Fuentes:
sintetizados por código (`Tools/Audio/synth_audio.py`, determinista: magia, fuego, agua, viento, música ambiental) y
paquetes CC0 de Kenney (RPG Audio, Impact Sounds, Interface Sounds: pasos, golpes, tela, libros, interfaz). Importación
Vorbis mono (`AudioImportRules`): música en streaming, loops comprimidos, efectos cortos descomprimidos.

## ADR-024 — Mapa generado por código, low-poly y laboratorio aparte
**2026-10-07.** El mapa del modo Libre (`Pradera.unity`) lo genera `MapBuilder` (editor) a partir de `ValleyShape` (puro,
con tests): terreno low-poly con colores por triángulo, pinos y rocas facetados, torre con rampas. Sin assets externos:
se itera cambiando números, es determinista y liviano para Quest (~45k triángulos, un material con color por vértice).
El estilo visual sale del shader `StylizedLit` (bandas de luz, sombras, luces puntuales, niebla). La escena `Escena` queda
como laboratorio de pruebas. Pisos unidos por **rampas en espiral**, no escaleras: el jugador es un Rigidbody.

## ADR-023 — Marcas de suelo con quads, no con decals de URP
**2026-10-07.** Cráteres, quemaduras y charcos son quads con un shader procedural (`GroundMark`) apoyados sobre el
suelo, en vez del Decal Projector de URP: no requiere activar el renderer feature (configuración del proyecto), cuesta
poco en Quest y alcanza para un piso plano. Si el terreno de M5 es irregular, migrar a decals.

## ADR-022 — Mundo reactivo por ticks y fractura por grilla
**2026-10-07.** Las reacciones (fuego, agua, viento, impacto) no corren por frame ni por colisión: `MagicWorld` hace ticks
a 15 Hz y consulta la misma geometría que la física (`MagicField.ExposureAt`). Las reglas son lógica pura
(`ReactionModel`) y los números son datos (`MaterialProfile`, `ReactionTable`). La fractura empieza como una grilla de
bloques con el material del objeto (estilizado, barato, sin assets); la pre-fractura Voronoi queda para mesas y paredes
de M5. El calor acumulado tiene tope (2× la ignición): sin él, lo que ardía mucho se volvía inapagable.

## ADR-021 — Física por arrastre al flujo
**2026-10-07.** `MagicField` no aplica aceleraciones libres: cada primitiva define una velocidad de flujo y los cuerpos
se aceleran hacia ella. La forma de los signos fija la velocidad y el área del sello, el acople (canon: intensivo vs.
extensivo). Evita lanzamientos absurdos y hace que un sello grande "empuje más masa", no "más rápido".
