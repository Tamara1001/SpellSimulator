---
tags: [doc, roadmap]
---
# Roadmap

Sin fecha fija. **Prioridad: un modo Libre sólido para la evaluación de Simuladores**; el Duelo viene después.
PC y VR avanzan en paralelo en cada hito. Cada hito termina **jugable** y verificado (ver [[HARNESS]]).
Estados: ✅ hecho · 🔄 en curso · ⬜ pendiente. `KI-xxx` → [[KNOWN_ISSUES]]. Diseño: [[GAME_DESIGN]] · [[SYSTEMS_DESIGN]].

## Hecho
- ✅ **M0 — Base técnica**: canvas GPU, herramientas físicas, rig PC/VR, harness, docs.
- ✅ **M1 — Pipeline mágico confiable** (2026-10-07): Parsing/Recognition/Glyphs/Compiler/Casting, port fiel de la
  referencia, modelo GlyphStudent, anillo abierto/cerrado, 44 tests.

---

## 🧙 MODO LIBRE (objetivo de la evaluación)

### M2 — El trazo se siente bien 🔄
*Objetivo: dibujar y lanzar en PC y VR sin fricción.*
- [x] **Auto-activación al cerrar el anillo** (canon). El secundario de las herramientas es deshacer. (2026-10-07)
- [x] Rechazar círculos que forman parte de un glifo como anillo (KI-032).
- [x] Primera prueba manual: sigilo grande partido (resuelto con afinidad + lectura guiada) y dibujo lento (resuelto).
- [x] Fixtures de sesiones reales: `SealRecord` graba cada cast; `SealFixtureTests` los reproduce.
- [ ] Juntar fixtures de pruebas manuales (columnas a mano, signos chicos) y fijar expectativas.
- [x] Hápticos (trazo, cast, sin tinta, recarga).
- [ ] Dibujo por **contacto de la punta** en VR: implementado detrás de un toggle, falta probar en el Quest (KI-034).
- [x] Feedback en el papel: pulso de "preparado", destello al lanzar o fallar (`SealFeedback`).
- [x] Advertencias y glifos reconocidos mostrados en el papel (`SealAnnotations`: nombre, confianza, flecha por signo).
- [x] Trapo (limpiar el lienzo) (KI-021).
- [x] `ToolDefinition` como SO (`Data/Tools`: Lápiz, Pincel, Goma, Trapo); pincel con trazo ancho y más gasto de tinta.
- [x] **Tinta como recurso**: depósito en el lápiz (indicador por color), tinteros físicos, reserva personal, `R` en PC y mojar la punta en VR.

### M3 — El hechizo tiene forma y fuerza
*Objetivo: cada combinación de signos se ve y se comporta distinto.*
- [x] Pose del modelo ⇒ orientación de cada signo (KI-025).
- [x] Port de lectura + plan: columnas, levitación, pull, convergencia, dispersión, spin-up (KI-023 parcial).
- [x] `SpellManifestation`: ráfaga, chorro, abanico, vórtice, orbe y succión, con los tiempos de R‑01.
- [x] Look de los 5 elementos (`ElementLook` + shader de partículas propio, Shuriken en vez de VFX Graph).
- [x] Tamaño físico del sello (radio en metros) ⇒ escala y acople (ley extensiva).
- [x] `MagicField`: física por arrastre al flujo sobre cualquier Rigidbody, incluido el jugador.
- [ ] Region (cercos), weave y orb/vessel (KI-023).
- [ ] Look más estilizado WHA (trazos de tinta, glow, texturas pintadas); sonido por elemento.
- [ ] **Benchmark en Quest** para fijar el límite de hechizos activos (provisorio: 6).

### M4 — El mundo reacciona
*Objetivo: el sandbox que justifica el simulador.*
- [x] `MaterialProfile` + `ReactionTable` (SO). `ElementReactive` + `MagicWorld` (ticks a 15 Hz).
- [x] Fuego: arde, se propaga a vecinos, lo apaga el agua, lo aviva el viento, carboniza y consume (`ReactionModel`).
- [x] Romper el sello: papel empapado o en llamas pierde el dibujo (`SealBreaker`).
- [~] Fractura: versión simple por grilla (`Shatter`, presupuesto de 160 fragmentos). Falta pre-fractura Voronoi para mesas y paredes.
- [x] Agua: estanque estilizado (`StylizedWater`: olas + ondas analíticas), salpicaduras, flotación por material, vapor
      con fuego, lo sumergido se moja y se apaga. Pull de agua tira un chorro del estanque (`WaterVolume`). Falta el lago de M5.
- [x] Suelo: cráteres con rocas que emergen (tierra), quemaduras (fuego) y charcos que se secan (agua) (`GroundReactions`, `GroundMarks`).
- [x] Muñecos de práctica (`TrainingDummy`: tela sobre un poste con resorte, vida, se rompen y reaparecen) + daño (`Combat`).
- [ ] Barriles y estantes destructibles (con la pre-fractura de M5).
- [x] Reinicio del mundo (Backspace / Select / Y izquierdo).

### M5 — La torre y la pradera
*Objetivo: el escenario final del modo Libre.*
- [x] Blockout del mapa (escena `Pradera`, generada por `MapBuilder`): valle con montañas, lago, pinos, rocas, caminos,
      torre de 3 pisos con rampas (biblioteca, estudio, terraza), campo de práctica, cascada + arroyo, pasto y flores,
      puerta, velas, motas mágicas, atril.
- [~] Arte estilizado: low-poly con sombreado plano (`StylizedLit`: bandas, sombras, luces puntuales, niebla), cielo y
      ambiente, cascada (`Waterfall`), post-proceso (bloom, tonemapping neutral, color, viñeta). Falta medir en Quest
      (~76k triángulos) y decidir qué post-proceso queda en VR (KI-049).
- [x] Papeles fijos (atril del campo, pared exterior de la torre, escritorio) y agarrables S/M/L (mesa de lectura + atril).
- [x] Audio (módulo `Audio`): música ambiental, viento, cascada, pájaros; pluma, goma, trapo, tinta, sello preparado,
      lanzamiento por elemento, fallo, fuego, vapor, salpicaduras, roturas, golpes, pasos por superficie. Sintetizado
      (`Tools/Audio/synth_audio.py`) + Kenney CC0. Falta: mezcla fina, música con más variedad (KI-051).

### M6 — Aprender magia: libro-menú, libros y sellos
*Objetivo: progresión y herramientas avanzadas.*
- [x] Libro-menú diegético (`Grimoire`): flota frente al jugador; portada, una página por glifo descubierto (se dibuja
      solo + nombre y explicación), opciones (reiniciar el mundo, olvidar lo aprendido). Páginas con E / grip.
- [x] 5 libros en la torre (`LibraryBook`) que enseñan glifos; descubrimiento por experimentación (confianza ≥ 0.8);
      guardado local (`descubrimientos.json`). Textos en `Data/Lore/glifos.json`.
- [x] Estampas S/M/L (`Stamp` + `StampPattern`): se cargan del grimorio (secundario apuntando al libro, 5 usos), el
      sigilo se estampa con anillo abierto (preparado, se cierra a mano) y los signos se ajustan al anillo y apuntan adentro.
- [x] Contraptions parciales: `SealTrigger` con `Impact` (piedra arrojada) y `Pressure` (placa con peso); el anillo cerrado deja el sello *armado*.
- [x] Sello permanente (piedra de rayar): `SealTrigger` con usos y enfriamiento; la placa de presión tiene 5 usos y 3 s.
- [x] Contraptions: `SplitJoin` (`SplitSealHalf` + `SplitJoinMath`): dos mitades que se juntan lado a lado fusionan sus trazos y lanzan el hechizo.
- [x] Tanda de la prueba manual: correr y agacharse, objetos movibles (E, rueda, Q), lienzos que flotan o se apoyan, dar vuelta
      la hoja (F), emisión por la cara con tinta, modo de escritorio (V), sellos en el piso/torre/cajas, cuadernos, caballetes
      y demás lienzos, mesas de trabajo con herramientas nuevas, cielo, grimorio movible con flechas y páginas de ayuda, agua.
- [x] Borrado por agua (mojar rompe sellos) y por fuego (quemar el papel) — hecho en M4 (`SealBreaker`).
- [ ] Superficies inscribibles generales (decals en paredes y mesas) y piedra de rayar (sellos permanentes, cooldown y 5 usos).
- [ ] Semántica para los 6 glifos sin entrada.

### M7 — Pulido y entrega del modo Libre
- [ ] Build Quest estable, perfilado (inferencia TTA ×8 vs ×1, VFX, física).
- [ ] Tutorial (después de M6): notas en la torre o el primer libro. Enseña a "cerrar el anillo al final".
- [ ] PlayMode tests del cast (KI-026). Documentación final de la evaluación.

---

## ⚔️ MODO DUELO (post-evaluación)

### D1 — Arena y reglas
- [ ] Escena de arena con coberturas destructibles y una fuente de agua.
- [ ] `GameMode` Duelo: timer, `Health`/`DamageReceiver`, tabla de daño (SO), victoria y derrota.
- [ ] Tinta limitada con recarga expuesta. Mazo de sellos preparados con cargas.

### D2 — Rival IA
- [ ] `AICaster`: dibuja `SealTemplate` con velocidad y ruido (dificultad) y pasa por el mismo `SpellCaster`.
- [ ] Tácticas: cubrirse, contrarrestar (agua contra sellos rivales), presionar.

### D3 — Online
- [ ] Separar el estado autoritativo de la presentación; sincronizar trazos y eventos de cast (Netcode + Relay).

---

## Limpieza transversal
- [ ] Decidir `_Recovery/0.unity` y `DebugCanvasCapture.png` (KI-024).
- [ ] Aprovechar las cabezas de pose del modelo (KI-025).
