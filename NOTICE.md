# Aviso de origen y licencia

**SpellSimulator** es una obra derivada de **[wha-spell-simulator](https://github.com/cosykid/wha-spell-simulator)**
de **cosykid (Langkee Hong)**, un proyecto fan de *Witch Hat Atelier* publicado bajo la
**GNU General Public License v3.0** ([demo en línea](https://wha-spell-simulator.vercel.app/)).
Este repositorio se distribuye bajo la misma licencia (ver [`LICENSE`](LICENSE)).

## Qué viene del proyecto original
| Qué | Dónde está acá |
|---|---|
| Modelo de reconocimiento de glifos (GlyphStudent, ONNX) y mapa de clases | `Assets/_Project/Data/glyph-recognizer.onnx*`, `glyph-class-to-idx.json` |
| Diccionario de sigilos y signos | `Assets/_Project/Data/Dictionary/` |
| Lógica del parser (anillos, capas, agrupación, ensamblado) y del compilador de hechizos, portada de TypeScript a C# | `Assets/_Project/Scripts/Parsing`, `Recognition`, `Compiler` |

El mapa archivo por archivo de qué es fiel y qué se reinterpretó está en [`Docs/REFERENCE.md`](Docs/REFERENCE.md).

## Cambios y aportes de este proyecto (GPL v3, sección 5)
Respecto del original, este proyecto es una reescritura en **Unity 6 (C#)** con los siguientes cambios principales,
realizados desde agosto de 2026:
- Port del parser y del compilador de TypeScript a C#.
- Lienzo de dibujo en GPU, interacción en primera persona para PC y VR, inventario, tinta, grimorio y sellos.
- Mundo 3D reactivo con físicas (fuego, viento, tierra, agua), portales y reciclaje de objetos.
- Multijugador con Netcode for GameObjects y Relay, y modo duelo.
- Batería de tests, bots de red y herramientas de editor.

Los cambios están registrados en [`Docs/CHANGELOG.md`](Docs/CHANGELOG.md).

## Otros materiales
- Sonidos: paquetes de [Kenney](https://kenney.nl) (CC0) y audio sintetizado por scripts propios (`Tools/Audio`).
- *Witch Hat Atelier* y sus nombres, símbolos y marcas pertenecen a sus titulares. Este proyecto es no oficial, hecho por
  fans, y no está afiliado, respaldado ni patrocinado por sus creadores ni editores.

## Autoría de este repositorio
Copyright (C) 2026 Tamara D'Angelo y Alejo Warner, sobre el código y los aportes propios de este repositorio, licenciados bajo GPL v3.
