---
tags: [doc]
---
# Proyecto

**SpellSimulator** — Evaluación final de la asignatura *Simuladores*. Autora: Tamara.
Inspirado en el sistema de magia de *Witch Hat Atelier* (vía el fan-project [[REFERENCE|wha-spell-simulator]]).

## Visión
Simulador de hechicería inspirado en *Witch Hat Atelier*: **la magia se dibuja**. El jugador traza un anillo, un sigilo
en el centro y signos alrededor; una red neuronal local reconoce cada glifo, un compilador arma el hechizo y el mundo
reacciona (quema, moja, rompe, empuja). Interfaz **diegética**: libro-menú, papeles, plumas, pinceles, estampas y
tinteros físicos. Estilo de ilustración WHA. Diseño completo en [[GAME_DESIGN]].

## Modos
| Modo | Descripción | Prioridad |
|---|---|---|
| **Libre** | Torre con bibliotecas + pradera + lago, rodeados de montañas. Se aprenden glifos leyendo libros y se experimenta. Mundo reactivo. | **Evaluación de Simuladores** |
| **Duelo** | Arena con tiempo y vida; los hechizos hacen daño. Rival IA primero, online después. | Posterior |

## Plataformas
| Plataforma | Estado | Input |
|---|---|---|
| PC | Funcional | Mouse/teclado (gamepad parcial) |
| Meta Quest (OpenXR) | En progreso | Grip = agarrar, gatillo = dibujar, A = secundario |

Ambas se desarrollan **en paralelo** (ver [[DECISIONS#ADR-002]]).

## Decisiones de alcance (2026-10-07)
- Simulación **espectacular pero acotada**: pre-fracturados, cráteres con decals, agua por shader, fuego por propagación.
  Tiene que correr en Quest.
- **Tinta como recurso** en ambos modos (tinteros como objetos; `R` recarga en PC).
- Glifos **descubiertos leyendo libros**; los no descubiertos igual funcionan si se dibujan bien.
- **Contraptions**, sellos lanzables y borrado por agua: sí. Magia prohibida (dibujar en el cuerpo): no por ahora.
- Duelo con **sellos preparados de cargas limitadas**.

## Fuera de alcance (por ahora)
Online (hasta D3), entrenamiento del modelo dentro de Unity, terreno voxel, fluidos simulados.

## Glosario
| Término | Significado |
|---|---|
| **Stroke / Trazo** | Lista de puntos UV `[0,1]` entre pointer-down y pointer-up. |
| **DrawingSession** | Conjunto de trazos de un lienzo (cada `DrawingCanvas` tiene la suya). |
| **Anillo (Ring)** | Trazo grande y cerrado que delimita el hechizo. Cerrado ⇒ hechizo *activo*; abierto ⇒ *preparado*. |
| **Sigilo (Sigil)** | Glifo central: define el **elemento** (fire, water, earth…). |
| **Signo (Sign)** | Glifo exterior al centro: **modifica** el comportamiento (column, pull, levitation…). |
| **Glifo (Glyph)** | Cualquier símbolo reconocible por el modelo (sigilo o signo). Ver [[GLYPHS]]. |
| **GlyphAST** | Sello reconocido: anillo, sigilo primario, signos, desconocidos, advertencias y métricas. |
| **SpellIR** | Representación intermedia compilada (struct): parámetros finales que consume el VFX. |
| **Cast** | Acción secundaria con el lápiz: `SpellCaster` limpia, parsea, reconoce, compila y lanza. |
| **Preparado** | Hechizo válido con el anillo abierto: no se lanza hasta sellarlo. |
