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
- [ ] **Auto-activación al cerrar el anillo** (canon). El secundario del lápiz pasa a ser deshacer (pregunta abierta en GAME_DESIGN).
- [ ] Validación con dibujos reales; ajuste del agrupado y los umbrales (KI-020); fixtures de sesiones reales.
- [ ] Dibujo por **contacto de la punta** en VR (además del rayo) + hápticos de fricción.
- [ ] Feedback diegético en el papel: brillo de "preparado", trazo reconocido resaltado y advertencias.
- [ ] Trapo (limpiar el lienzo) (KI-021). `ToolDefinition` como SO; pincel.
- [ ] **Tinta como recurso**: depósito por herramienta, tinteros como objetos y recarga con `R` / contacto.

### M3 — El hechizo tiene forma y fuerza
*Objetivo: cada combinación de signos se ve y se comporta distinto.*
- [ ] Leer `animation-spec.md` de la referencia y portar `compiler/reading` + `compiler/plan` (KI-023).
- [ ] `SpellManifestation` por familia: aura, column, dispersion, levitation, pull, convergence, region, orb.
- [ ] VFX Graph de los 5 elementos con las mismas propiedades exposed; look estilizado WHA.
- [ ] Tamaño físico del sello (radio en metros) ⇒ escala y caudal (ley extensiva).
- [ ] `MagicField` + `MagicWorld` (ticks) con un primer consumidor: empujar Rigidbodies.

### M4 — El mundo reacciona
*Objetivo: el sandbox que justifica el simulador.*
- [ ] `MaterialProfile` + `ReactionTable` (SO). `ElementReactive`.
- [ ] Fuego: `Flammable`, propagación, apagado por agua y avivado por viento, carbonización.
- [ ] Fractura: herramienta de pre-fractura + swap a fragmentos (mesas, paredes, rocas). Pools.
- [ ] Agua: lago estilizado con ripples por GPU, splashes, flotación y evaporación. Pull atrae agua.
- [ ] Suelo: cráteres (decals) y rocas emergentes.
- [ ] Dummies de prueba (ragdoll) y barriles, cajas y estantes destructibles.
- [ ] Reinicio del mundo.

### M5 — La torre y la pradera
*Objetivo: el escenario final del modo Libre.*
- [ ] Blockout del mapa: torre (bibliotecas, mesas, atriles), pradera, lago y montañas de borde.
- [ ] Arte estilizado (toon/acuarela), iluminación y post-proceso. Presupuesto Quest.
- [ ] Papeles fijos en atriles y paredes; papeles agarrables S/M/L.
- [ ] Sonido diegético (pluma, viento, fuego, agua).

### M6 — Aprender magia: libro-menú, libros y sellos
*Objetivo: progresión y herramientas avanzadas.*
- [ ] Libro-menú diegético: glifos descubiertos con animación de trazo, opciones y reinicio.
- [ ] Libros de la biblioteca que descubren glifos; descubrimiento por experimentación; guardado local.
- [ ] Estampas S/M/L: cargar desde el libro y estampar (anillo abierto ⇒ se sella a mano).
- [ ] Contraptions: triggers `RingClosed`, `SplitJoin` (sello partido), `Impact` (lanzables), `Pressure`.
- [ ] Borrado por agua (mojar rompe sellos) y por fuego (quemar el papel).
- [ ] Superficies inscribibles generales (decals en paredes y mesas) y piedra de rayar.
- [ ] Semántica para los 6 glifos sin entrada.

### M7 — Pulido y entrega del modo Libre
- [ ] Build Quest estable, perfilado (inferencia TTA ×8 vs ×1, VFX, física).
- [ ] Tutorial ligero (notas en la torre o el primer libro).
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
