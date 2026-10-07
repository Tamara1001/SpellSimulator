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
