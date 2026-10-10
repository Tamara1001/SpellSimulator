---
tags: [doc, diseño, lore]
---
# Sistema mágico (Witch Hat Atelier → SpellSimulator)

Investigación del sistema mágico de *Witch Hat Atelier* (Kamome Shirahama) y cómo se traduce a reglas jugables.
Fuentes al final. Canon detallado y fórmulas: `wha-spell-simulator/docs/ground-truth.md` (ver [[REFERENCE]]).

## 1. El canon en una página

- **La gran regla:** para hacer magia sólo se necesitan **tinta de conjuro** y conocimiento de **sellos**. Cualquiera
  puede hacerla. Por eso los brujos la mantienen en secreto: a quienes no la conocen (los "unknowing") se les borra la
  memoria si la descubren.
- **Tinta de conjuro:** se procesa del *woodcruor*, la savia de los árboles de plata (Silverwood). Sólo los sellos
  dibujados con esa tinta se activan.
- **El sello** tiene tres partes:
  - **Sigilo** (centro): define el **elemento**. Hay cinco familias: fuego, agua, tierra, viento y luz, con variantes
    (cristal es tierra; aeroform, wind-directs-air y wind-underfoot son viento).
  - **Signos** (alrededor): definen la **forma, el tamaño y la dirección** de la magia (columna, levitación, atracción,
    dispersión, región...).
  - **Anillo**: el círculo que encierra todo. **El hechizo se lanza en el momento en que se cierra el anillo.** Un
    anillo abierto es un sello "preparado".
- **Prolijidad:** un sello torpe o chico es más débil o falla. Un sello impreciso puede producir algo inesperado.
- **Romper el sello** (borrarlo, mojarlo, cortar el papel) detiene la magia.
- **Contraptions:** objetos con un sello dibujado que funcionan solos. Son la única forma en que los "unknowing" usan
  magia. Ejemplos:
  - *Sylph Shoes*: el sello está **partido entre los dos zapatos** y se completa al juntarlos, lo que hace flotar.
  - *Floatglow lamp*: una esfera de luz flotante.
  - Ventanas de teletransporte, con un sello por destino.
- **Magia prohibida:** dibujar sellos sobre el cuerpo humano (la única excepción es el borrado de memoria). Los
  *Brimhats* la practican; los *Knights Moralis* la persiguen.
- **Herramientas:** plumas y varitas de tinta (*Ink Wand*), pluma de agua (*Water Pen*), sellos y estampas.

## 2. Hechizos canónicos útiles como referencia de diseño

| Hechizo (canon) | Composición aproximada | Comportamiento | En nuestro juego |
|---|---|---|---|
| Watershot | agua + columnas opuestas hacia adentro | Chorro vertical concentrado | Proyectil / cañón de agua |
| Flame shot | fuego + columna que cruza el sello | Disparo plano dirigido | Proyectil de fuego |
| Pyreball | fuego + levitaciones opuestas | Bola de fuego suspendida sobre el papel | Fuego "en bandeja", granada |
| Floatglow lamp | luz + levitación | Esfera de luz flotante | Lámpara colocable (contraption) |
| Skysoaring | viento abajo + levitación que cruza el sello | Empuje en la dirección de la flecha | Planear / impulsarse con un papel grande |
| Sylph Shoes | viento + levitación, sello partido | Flotar al juntar los pies | Contraption de dos piezas |
| Grasping wind | viento + pull | Atrae lo que sea del mismo elemento | Atraer objetos y aire |
| Crystal shard | cristal + columnas hacia afuera | Erupción radial de cristales | Explosión de esquirlas |
| Wall Breaker | tierra + fuerza | Rompe paredes | Destrucción de estructuras |
| Rainbringer | agua + región | Lluvia localizada | Apagar fuego, mojar sellos |
| Vapor Bubble | agua/aire | Burbuja de vapor | Escudo / ocultarse |

## 3. Leyes físicas (resumen del canon de la referencia)

Están detalladas en `ground-truth.md`. Las que más impactan el diseño 3D son estas:

1. **Forma intensiva, tamaño extensivo.** La forma de los signos (largos relativos al anillo) define la *velocidad*;
   el **área del anillo** define el *caudal*. Un sello el doble de grande con la misma forma tiene la misma velocidad
   y cuatro veces el caudal y la fuerza total.
   ⇒ **El tamaño físico del papel, del sello o de la estampa importa en el mundo 3D.** Es la razón de ser de los
   papeles y sellos de distintos tamaños.
2. **Pared del disco:** la magia afecta todas las direcciones *excepto debajo del sello*. Sale por la cara con tinta.
3. **Ráfaga por defecto:** un anillo cerrado sin signos emite una onda hemisférica (*aura*).
4. **Columna (motor):** los signos se suman como vectores. Si son opuestos y apuntan adentro forman un chorro
   vertical; si uno es más largo, el chorro se inclina hacia donde apunta ese signo largo; si apuntan afuera, la magia
   erupciona radialmente; en molinete, forman un vórtice.
5. **Región (válvula):** no aporta fuerza; recorta *dónde* puede existir la magia (cercos, canales, cortinas).
6. **Levitación (par de fuerzas):** acopla el sello con los *cuerpos*. Sostiene una masa a cierta altura (resorte) o
   empuja el soporte (con viento: el papel o la persona vuela).
7. **Pull (acople ambiental):** atrae materia **del mismo elemento** que ya existe en el entorno (agua del lago,
   fuego de una fogata), nunca la propia manifestación.
8. **Convergencia (lente):** comprime la manifestación hacia una línea o un punto.
9. **Orbe (contenedor):** encierra la magia en una vasija con gravedad local.
10. **Calidad:** la prolijidad multiplica la potencia. Por debajo de un umbral, el sello "chisporrotea" (sólo la ráfaga).

## 4. Traducción a reglas de juego

Leyes del §3 implementadas en M3 (`PlanResolver` + `SpellManifestation` + `MagicField`): ráfaga, columna (chorro,
abanico, vórtice), levitación (orbe), pull (succión) y convergencia (lente). Desde la octava tanda también crush (Wall Breaker con tierra), collection, billowing, repetition, weave y región (ver `GAME_DESIGN.md`, «Signos y sus efectos»). Falta orbe-vasija.
Desde M4 el pull respeta la ley 7: solo atrae materia de su elemento (piedra con tierra, lo que arde con fuego, agua
del estanque con agua).

| Canon | Regla en SpellSimulator | Estado |
|---|---|---|
| Tinta de conjuro | Tinta = **recurso** en ambos modos. Cada herramienta tiene un depósito; se recarga con tinteros (objetos que se pueden llevar y mover). En PC, `R` recarga desde el tintero que tengas encima. | ✅ M2 |
| Cerrar el anillo activa | Al levantar el lápiz, si el anillo quedó cerrado, el hechizo **se lanza solo**. Anillo abierto = preparado (se ve el brillo, pero no se lanza). | ✅ M2 |
| Prolijidad | `quality`, `stability` y `neatness` modulan la potencia y la duración. Un sello muy desprolijo hace sólo la ráfaga. | Parcial (calidad modula densidad y acople; falta el "chisporroteo") |
| Tamaño extensivo | El radio del anillo **en metros** escala el caudal y la fuerza en el mundo. | ✅ M3 (`MagicField`: el área escala el acople, la forma la velocidad) |
| Romper el sello | La goma borra trazos y el **trapo** borra el lienzo entero. El **agua** de un hechizo que moja un sello lo mancha y lo rompe. Cortar o quemar el papel también lo rompe. | Parcial (M4: papel empapado o en llamas pierde el sello; falta cortar) |
| Contraptions | Sellos en objetos del mundo (paredes, piedras, mesas, papeles fijos) con disparadores: cerrar el anillo, juntar dos mitades, impacto o pisarlo. | Impacto y presión implementados (`SealTrigger`); falta juntar dos mitades |
| Sellos partidos | Dos medias lunas en dos objetos; al alinearlos (distancia y ángulo) el sello se completa. | Implementado (`SplitSealHalf`) |
| Sellos lanzables | Un sello en un papel o una piedra queda en reposo y se activa al impactar. | Diseño |
| Estampas | Una estampa vacía copia un glifo del libro-menú y lo estampa en cualquier superficie. El tamaño de la estampa = tamaño del sello. | Diseño |
| Elementos | Fuego, agua, viento, tierra y luz, con variantes por sigilo. | ✅ Compilador + look por elemento (M3) |
| Magia prohibida | No se incluye por ahora (decisión 2026-10-07). | — |

## 5. Elementos × mundo (matriz de reacciones)

| Elemento | Sobre materiales | Sobre otros elementos | Sobre sellos |
|---|---|---|---|
| **Fuego** | Enciende lo inflamable (madera, papel, tela, libros). Quema y carboniza. Calienta el agua y la evapora. | Lo apaga el agua. El viento lo aviva y lo propaga. | **Quema el papel** ⇒ el sello se rompe. |
| **Agua** | Moja (no se quema mientras está mojado) y empuja. Llena huecos. | Apaga el fuego. Se evapora con el fuego. El pull atrae el agua del lago. | **Mancha la tinta** ⇒ rompe el sello. |
| **Viento** | Empuja Rigidbodies y levanta objetos livianos. Mueve el agua (olas). | Aviva o dispersa el fuego. Desvía proyectiles. | Puede volar un papel suelto. |
| **Tierra / cristal** | Rompe y fractura: rocas, paredes, mesas. Levanta el suelo (cráteres, pilares). Esquirlas. | Bloquea el agua y el fuego (muro). | Rompe la superficie si es frágil. |
| **Luz** | Ilumina, revela y ciega. Calienta poco. | Atraviesa el agua. | — |

La matriz final es un **asset de datos** (ver [[SYSTEMS_DESIGN]]), no código.

## Fuentes
- [Witch Hat Atelier — Wikipedia](https://en.wikipedia.org/wiki/Witch_Hat_Atelier)
- [Magic — Witch Hat Atelier Wiki](https://witch-hat-atelier.fandom.com/wiki/Magic) · [Conjuring Ink](https://witch-hat-atelier.fandom.com/wiki/Conjuring_Ink) · [Contraption](https://witch-hat-atelier.fandom.com/wiki/Contraption) · [List of Items](https://witch-hat-atelier.fandom.com/wiki/List_of_Items)
- [Sylph Shoes](https://witch-hat-atelier.fandom.com/wiki/Sylph_Shoes) · [Skysoaring Seal](https://witch-hat-atelier.fandom.com/wiki/Skysoaring_Seal) · [Wall Breaker Seal](https://witch-hat-atelier.fandom.com/wiki/Wall_Breaker_Seal) · [Floatglow Lamp Seal](https://witchhatatelier.telepedia.net/wiki/Floatglow_Lamp_Seal) · [Signs Explained](https://witchhatatelier.telepedia.net/wiki/Signs_Explained)
- [Witch Hat Atelier's Magic System, Explained — GameRant](https://gamerant.com/witch-hat-atelier-magic-system-explained/) · [FandomWire](https://fandomwire.com/witch-hat-atelier-power-system-explained-what-are-magic-glyphs-how-do-they-work/) · [MangaCraft](https://mangacraft.substack.com/p/witch-hat-atelier-unique-magic-systems)
- `wha-spell-simulator/docs/ground-truth.md`, `play-rules.md`, `animation-spec.md` (canon formalizado con reglas R‑xx).
