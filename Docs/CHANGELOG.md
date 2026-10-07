---
tags: [doc, changelog]
---
# Changelog

Entradas más nuevas arriba. Una por tarea/commit relevante: fecha, qué, por qué, docs tocadas.

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
