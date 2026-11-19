---
tags: [doc, ui, estetica]
---
# UI y estética de carteles (borrador)

Objetivo: que carteles, pizarras, HUD y libros se sientan parte del mundo (*Witch Hat Atelier*, low-poly cartoon), no rectángulos negros con Liberation Sans.

## Tipografía
| Uso | Fuente | Dónde se aplica |
|---|---|---|
| Títulos de cartel (tiendas, carteles de práctica) | **Lora** | `MapRestocker` y `CanvasDesigns.Practice` (`FontBuilder.LoadBookFont`). Caveat Brush quedó generada, sin uso. |
| HUD, menú, rótulos, pizarras, plaquetas, laboratorio, mapa de la torre | **Nunito** | Fuente **por defecto de TMP** (todo texto sin fuente explícita) y `:root` de `SpellUI.uss` (HUD y menú, UI Toolkit). |
| Grimorio y lector de libros | **Lora** | `GrimoireTexts.MakeText` (TMP 3D) y `.reader` en `SpellUI.uss`. |

Assets: TTF en `Assets/_Project/Art/Fonts/` (OFL incluidas); assets TMP en `Assets/_Project/Resources/Fonts/` (generados por `SpellSimulator/Fuentes/Generar tipografias`, `FontBuilder.cs`, con Liberation Sans de respaldo para glifos que falten). Acceso en runtime: `UI/UiFonts.cs`. El panel de debug (F3) sigue en Liberation Sans a propósito.
Las fuentes de Google son variables: Unity usa la instancia Regular y sintetiza la negrita.
**Si regenerás las fuentes, reconstruí el mapa** (`MapBuilder.Build()`): los GUID de los assets cambian.

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

## Qué usa cada fuente (estado 2026-11-19)
- **Nunito** (`Resources/Fonts/Nunito SDF`, fuente por defecto de TMP y de `SpellUI.uss`): HUD, menús, plaquetas, pizarras y rótulos.
- **Lora** (`Resources/Fonts/Lora SDF`; `UiFonts.Book`, `FontBuilder.LoadBookFont`): libros, grimorio, lector y títulos de cartel de los puestos.
- **Caveat Brush**: generada pero sin uso (se probó en carteles y se descartó por mezclarse con Lora).
- Se regeneran con el menú `SpellSimulator/Fuentes/Generar tipografias` (`Editor/FontBuilder.cs`); **regenerar cambia los GUID**: después correr `MapBuilder.Build()` o los carteles pierden la fuente. Los atlas son estáticos: si falta un carácter, agregarlo a `FontBuilder.Characters`.
- Aviso conocido en consola: «The character used for Ellipsis is not available in font asset [Nunito SDF]» (ver KI-142).

## Pendiente
- Revisar a mano el HUD y el lector de libros con Nunito/Lora (la migración se verificó por código y consola, no a ojo).
- Carteles del resto del Archivo (rótulos de nichos) con Caveat Brush.
