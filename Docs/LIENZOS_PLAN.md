# Plan: lienzos unificados, pinceles, restock y auditoría de modificadores

Estado: **borrador aprobado en lo básico (2026-11-18)**. Rediseño: esperar el sí a los puntos abiertos antes de codear el Bloque A.

## Decisiones de Tamara
- Solo 2 tipos de objeto: **Lienzo** (1 página) y **Libreta** (hasta 8 hojas). Mismas reglas: agarrar, dejar flotando, girar, mover en X/Y/Z.
- Tamaños: S/M/L cuadrados y rectangulares. Libretas: **M = la actual**, una **S circular de bolsillo** y una **L rectangular grande**.
- F sigue siendo «dar vuelta». Se mantiene la idea del «lienzo que dispara hacia el otro lado» (con otro color/marco).
- Atriles: cambiar el lienzo acercándolo al slot **y** con E.
- Migrar **todos** los lienzos del mapa (tutorial, torre, laboratorio) al sistema nuevo.
- Grosor de trazo: **sin efecto** en el hechizo por ahora (solo estético/legibilidad).
- Lienzos y pinceles sacados del archivero **se reinician** al recargar.
- Laboratorio: lienzo flotante sin palo, editable (X/Y/Z, rotación, escala) y botón «limpiar efectos».
- Wiki: [Signs](https://witchhatatelier.telepedia.net/wiki/Signs_Explained), [Sigils](https://witchhatatelier.telepedia.net/wiki/Sigils_Explained), repo `cosykid/wha-spell-simulator`.

## Bloques
- **D. Modificadores × elementos** (primero). Auditoría en `Docs/MODIFICADORES_AUDITORIA.md` (Antigravity) → implementar VFX por modificador → capturas con `HarnessCapture`.
- **A. Lienzos unificados.** `CanvasSpec` (tipo, ancho, alto, páginas, cara de disparo) + reglas en clase estática con tests EditMode. `CanvasDesigns` solo coloca instancias y soportes. Atril = soporte con slot.
- **B. Pinceles.** `BrushSpec` (grosor, punta, forma); se quita el slider. Goma y trapo aparte. Verificar que el reconocimiento ONNX no empeore con trazos gruesos.
- **C. Restock.** Mueble en el archivero: pinceles + selector de lienzo/libreta (tamaño, forma, páginas).

## Dirección de disparo: propuesta (punto abierto)
Problema: el atril fijo no se gira con la muñeca, y F entre «terminé de dibujar» y «se castea» es demasiado rápido.
1. **La dirección es una propiedad del lienzo, no una acción de último momento**: `Cara de disparo = Frente | Espalda`, visible con **color de marco** (dorado = frente, azul = espalda) y una flecha en una esquina.
2. Se cambia **antes de dibujar** con un botón/perilla en el marco (E sobre el marco; en VR, tocarlo) y también con F/giro de muñeca en lienzos sostenidos.
3. En un **atril**, el slot decide la dirección: el atril se orienta (gira sobre su base con E + rueda / agarre) y el lienzo hereda esa orientación; además el botón del marco lo invierte sin mover el atril.
4. Opcional: pequeña ventana de gracia (~0,5 s) tras dibujar el último glifo para invertir con F antes del cast. Solo si 1–3 no alcanzan.

## Puntos abiertos
- ¿Aceptás la propuesta de dirección de disparo (1–3, con 4 opcional)?
- Presets exactos de S/M/L (en metros) y diámetro de la libreta circular.
