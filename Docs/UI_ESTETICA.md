---
tags: [doc, ui, estetica]
---
# UI y estética de carteles (borrador)

Objetivo: que carteles, pizarras, HUD y libros se sientan parte del mundo (*Witch Hat Atelier*, low-poly cartoon), no rectángulos negros con Liberation Sans.

## Tipografía
| Uso | Fuente | Estado |
|---|---|---|
| Carteles, rótulos de puestos, pizarras | **Caveat Brush** (OFL, Google Fonts) | Hecha: `Assets/_Project/Art/Fonts/CaveatBrush-Regular.ttf` + asset TMP `CaveatBrush SDF.asset` (generado con `SpellSimulator/Fuentes/Generar tipografias`, `FontBuilder.cs`). Ya la usan los dos puestos del Archivo. |
| Cuerpo y lecturas largas (HUD, libros, grimorio) | **Nunito** (OFL) o Lora | Pendiente: decidir y generar con el mismo `FontBuilder`. En VR conviene trazo grueso. |
| Títulos solemnes (portadas de libros, torre) | IM Fell English o Cormorant SC | Opcional. |

Juego de caracteres del asset: ASCII, acentos, ñ, ¿ ¡ y los signos `· × − « » – — … °`. Si un texto usa otro carácter, agregarlo a `FontBuilder.Characters` y regenerar.

## Paleta
- Madera cálida `#8F6138` (puestos), oscura `#5C3B24`, tablón claro `#AD804D`.
- Crema para texto `#F2E6C4`; latón para títulos `#DBA83F`; pizarra verde `#294238` con tiza crema.
- Acentos de toldo: rojo teja `#B83D47` (plumas), azul `#3D66B3` (lienzos). **Un acento por zona**, para orientarse.

## Reglas
- Texto sobre tablillas de madera, pizarras o pergamino; nunca sobre negro plano.
- Rótulos de dos líneas: nombre arriba, medida/dato abajo, más chico.
- Interacción directa sobre objetos reales (tarros, pilas, libros) antes que botones.
- Los carteles se arman con `TextMeshPro` en mundo; el tamaño mínimo legible a 1,5 m es ~6 cm de altura de letra.

## Pendiente
- Elegir fuente de cuerpo y migrar HUD / grimorio / libros (cambia assets TMP de todo el proyecto).
- Versión de «tienda» para el resto del Archivo (rótulos de nichos).
