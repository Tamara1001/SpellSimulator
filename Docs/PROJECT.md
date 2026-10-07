---
tags: [doc]
---
# Proyecto

**SpellSimulator** — Evaluación final de la asignatura *Simuladores*. Autora: Tamara.
Inspirado en el sistema de magia de *Witch Hat Atelier* (vía el fan-project [[REFERENCE|wha-spell-simulator]]).

## Visión
Sandbox de hechicería donde la magia **se dibuja**: el jugador traza un anillo, un sigilo en el centro y
signos alrededor; una red neuronal local reconoce cada glifo, un compilador arma el hechizo y el entorno
reacciona con físicas (fuego que quema, viento que empuja, ragdolls que vuelan). Foco en maestría mecánica
e interactividad emergente, no en RPG. Interfaz **diegética** (lienzo, lápiz, goma, grimorio físicos).

## Plataformas
| Plataforma | Estado | Input |
|---|---|---|
| PC | Funcional (FPS, agarrar con E, dibujar con click izq., castear con click der.) | Mouse/teclado, gamepad |
| Meta Quest (OpenXR) | En progreso (head tracking, manos, agarrar, dibujar) | Grip = agarrar, Trigger = dibujar, A = cast/undo |

Ambas se desarrollan **en paralelo**; toda feature debe funcionar en las dos (ver [[DECISIONS#ADR-002]]).

## Alcance MVP
- Un anillo, un sigilo primario, N signos exteriores.
- Elementos con VFX: Fuego (VFX Graph), Agua, Aire, Tierra, Luz (prefabs).
- Parámetros compilados (`force`, `spread`, `focus`, `range`, `gravity`, `duration`…) alimentan el VFX Graph.
- Sandbox: Rigidbodies, fuerzas radiales, inflamables, ragdolls.

## Fuera de alcance (por ahora)
Multiplayer, IA enemiga, múltiples anillos/sigilos, entrenamiento del modelo dentro de Unity.

## Glosario
| Término | Significado |
|---|---|
| **Stroke / Trazo** | Lista de puntos UV `[0,1]` entre pointer-down y pointer-up. |
| **DrawingSession** | Conjunto de trazos de un lienzo (cada `DrawingCanvas` tiene la suya). |
| **Anillo (Ring)** | Trazo grande y cerrado que delimita el hechizo. Cerrado ⇒ hechizo *activo*; abierto ⇒ *preparado*. |
| **Sigilo (Sigil)** | Glifo central: define el **elemento** (fire, water, earth…). |
| **Signo (Sign)** | Glifo exterior al centro: **modifica** el comportamiento (column, pull, levitation…). |
| **Glifo (Glyph)** | Cualquier símbolo reconocible por el modelo (sigilo o signo). Ver [[GLYPHS]]. |
| **SpellAST** | Árbol con anillo → sigilo + signos, con índice de clase y confianza. |
| **SpellIR** | Representación intermedia compilada (struct): parámetros finales que consume el VFX. |
| **Cast** | Acción secundaria con el lápiz: limpia trazos, infiere, compila y lanza. |
