---
tags: [doc, roadmap]
---
# Roadmap

Sin fecha de entrega fija. Dos tracks **en paralelo**: 🧙 Hechizos (pipeline + sandbox) y 🥽 VR/Plataforma.
Estados: ✅ hecho · 🔄 en curso · ⬜ pendiente. `KI-xxx` → [[KNOWN_ISSUES]].

## Hecho
- ✅ F0 Setup · F1 Canvas GPU + input · F2 Herramientas físicas, dual wielding, UI diagnóstico.
- ✅ VR básico: head tracking, manos, agarrar y dibujar.
- ✅ Harness: asmdefs, tests, runner, validadores, docs, Graphify.
- ✅ **F3 Pipeline confiable** (2026-10-07): reestructura Parsing/Recognition/Glyphs/Compiler/Casting, port fiel del
  compilador, diccionario como datos, modelo GlyphStudent + TTA + softmax, anillo abierto/cerrado, spawn sobre el lienzo,
  aspecto isotrópico, 44 tests (incl. end‑to‑end con el modelo).

## 🧙 Track Hechizos

### F3.5 — Validación con dibujos reales 🔄
- [ ] Probar en PC y VR con trazos a mano: anillo abierto/cerrado, sigilos, 1‑4 signos.
- [ ] Ajustar `StrokeGrouper.SigilRadiusMax` / `ClusterGapRatio` y umbrales del anillo si hace falta (KI-020).
- [ ] Capturar sesiones reales como fixtures de test (serializar `DrawingSession` a JSON).
- [ ] Limpiar lienzo completo desde el juego (KI-021).
- [ ] UI diegética del estado (preparado/activo, advertencias) en el lienzo, no sólo en el panel DEBUG.

### F4 — Plan y VFX parametrizados
- [ ] Leer `docs/animation-spec.md` de la referencia y portar `compiler/reading` + `compiler/plan` (KI-023).
- [ ] VFX Graph para Agua, Aire, Tierra, Luz con las mismas propiedades exposed que Fuego.
- [ ] Una representación 3D por familia de manifestación (aura, column, pull/vórtice, levitation, convergence…).

### F5 — Sandbox reactivo
- [ ] Suscribirse a `SpellCaster.SpellCompiled`: fuerzas radiales/centrípetas sobre Rigidbodies según `force`, `range`, `direction`.
- [ ] `Flammable` + propagación de fuego; viento que empuja.
- [ ] Dummies ragdoll.

### F6 — Grimorio y sellos
- [ ] Grimorio físico con glifos descubiertos (usar `GlyphEntry.TemplateStrokes`) y animación de tinta.
- [ ] Sellos en blanco: estampar una plantilla en un lienzo (anillo abierto ⇒ preparado, se sella a mano).
- [ ] Dar semántica a los 6 glifos sin entrada (cool, empower, entwine, focus, gather, orb).

## 🥽 Track VR / Plataforma

### V1 — Interacción VR sólida 🔄
- [x] Rayo VR con offset serializado, material de puntero como asset, usable por mano, cursor sólo en PlatformDetector.
- [ ] Verificar en Quest el nuevo `QueryTriggerInteraction.Collide` sobre el lienzo sostenido.
- [ ] Dibujo por contacto físico de la punta (además del rayo).

### V2 — Feel e inmersión
- [ ] Hápticos: fricción de tinta + pulso al compilar (vía `SpellCompiled`).
- [ ] Lienzo escalable en tiempo real (el pipeline ya es isotrópico).
- [ ] Cast/Undo en B/Y.

### V3 — Build Quest
- [ ] Perfilar inferencia en dispositivo: GPUCompute vs CPU, TTA ×8 vs ×1 (`useRotationTta`).
- [ ] Build Android estable + checklist de rendimiento.

## Limpieza
- [ ] Decidir `_Recovery/0.unity` y `DebugCanvasCapture.png` (KI-024).
- [ ] PlayMode tests automáticos del cast (KI-026).
