---
tags: [doc, roadmap]
---
# Roadmap

Sin fecha de entrega fija. Dos tracks **en paralelo**: 🧙 Hechizos (pipeline + sandbox) y 🥽 VR/Plataforma.
Estados: ✅ hecho · 🔄 en curso · ⬜ pendiente. Referencias `KI-xxx` → [[KNOWN_ISSUES]].

## Hecho
- ✅ F0 Setup: Unity 6, URP, estructura `_Project/`.
- ✅ F1 Canvas GPU + input PC (mouse).
- ✅ F2 Herramientas físicas (lápiz/goma), lienzo agarrable, dual wielding, UI de diagnóstico.
- ✅ Reconocimiento base: Sentis + ResNet18, StrokeCleaner, RingDetector, StrokeGrouper, AST, compilador MVP, VFX Fuego.
- ✅ VR básico: head tracking, manos, agarrar y dibujar en VR.
- ✅ Harness: asmdefs, tests EditMode de `Core`, runner + validador de escena, docs, Graphify.

## 🧙 Track Hechizos

### F3 — Pipeline confiable 🔄
- [ ] Softmax sobre `class_logits` y recalibrar `MinPrimaryConfidence` (KI-001).
- [ ] Spawnear en el lienzo con dirección = normal del lienzo (KI-002).
- [ ] Estado preparado vs activo: anillo cerrado/abierto en `RingDetector` (KI-005).
- [ ] Diagnósticos claros para anillo sin sigilo / glifo sin rol (KI-004).
- [ ] Decidir deltas: portar exactos de la referencia + semántica de sigilos (KI-003). Diccionario como datos.
- [ ] Servicio de cast único (sin `FindFirstObjectByType`, sin constantes duplicadas) (KI-009).
- [ ] Tests del compilador (fórmulas + clasificación sigilo/signo).

### F4 — VFX parametrizados
- [ ] VFX Graph para Agua, Aire, Tierra, Luz con las mismas propiedades exposed que Fuego.
- [ ] Manifestaciones: Aura, Column, Levitation, Convergence con comportamiento visual distinto.
- [ ] Portar `directionMode`/`manifestation` por signo y `SpellField` (campo de fuerzas) de la referencia.

### F5 — Sandbox reactivo
- [ ] Proyectiles/efectos con colisión física (Rigidbody, fuerzas radiales, centrípetas).
- [ ] `Flammable` + propagación de fuego; viento que empuja objetos.
- [ ] Dummies ragdoll.

### F6 — Grimorio y sellos
- [ ] Grimorio físico (mano izquierda) con glifos descubiertos y animación de tinta.
- [ ] Sellos en blanco: copiar un glifo y estamparlo en un lienzo.

## 🥽 Track VR / Plataforma

### V1 — Interacción VR sólida 🔄
- [ ] Reemplazar la rotación de 90° del rayo por un transform "Aim" (KI-011).
- [ ] Soportar múltiples `IUsable` / mano izquierda usable (KI-012).
- [ ] Cursor/lock sólo en `PlatformDetector` (KI-010).
- [ ] Dibujo por contacto físico de la punta (además del rayo) para sensación real de lápiz.

### V2 — Feel e inmersión
- [ ] Hápticos: fricción de tinta + pulso al compilar.
- [ ] Lienzo escalable en tiempo real sin deformar trazos.
- [ ] Cast/Undo en botones B/Y.

### V3 — Build Quest
- [ ] Perfilado en dispositivo (Inference GPUCompute vs CPU en Quest, VFX Graph en móvil).
- [ ] Build Android estable + checklist de rendimiento.

## Limpieza (cuando haya hueco)
- [ ] Decidir destino de `MouseInputHandler` y HUD legacy (KI-014).
- [ ] Unificar carpetas VFX y borrar residuos (KI-015).
