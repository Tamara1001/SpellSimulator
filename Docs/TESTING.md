---
tags: [doc, testing]
---
# Pruebas de lienzos (matriz)

Cubre los lienzos del juego en **PC**. Lo automático corre con `SpellSimulator > Harness > Run Canvas Matrix`
(resultado PASS/FAIL en `Logs/harness-canvasmatrix.txt`, unos minutos). Lo que depende de la sensación, de la vista o del
teclado/mouse reales va en la checklist manual de abajo. VR/Quest: casos escritos pero sin ejecutar hasta tener build (M7).

## Tipos de lienzo y su nombre en la escena
| Tipo | Ejemplos | Cómo se maneja |
|---|---|---|
| Agarrable (`PhysicalCanvas`, dinámico) | `Paper_S`, `Paper_M`, `GiantPaper_Field`, `Notebook_A/B`, `ImpactStone_J1/J2`, `ImpactMetal_M`, `ImpactGold_S` | E agarra y luego fija en el aire, Tab vista completa, Q arroja |
| Runa (`PhysicalCanvas` + `SealTrigger`) | `ImpactStone_S`, `ImpactStone_M` | igual que arriba + se activa al golpear |
| Flotante (`PhysicalCanvas` cinemático) | `PaperCanvas`, `Canvas_Lake1/4`, `GiantCanvas_Field` | se agarran y vuelven a flotar al reiniciar |
| Fijo | caballetes `Easel_*`, `DraftingTable_*`, `WallBoard_Library`, `WallPaper`, `DeskPaper`, `BigCanvas_Library` | solo Tab (vista completa, `SurfaceFocus`) y dibujar |
| Placa de presión | `PressurePlate` | fija; Tab y dibujar |
| Runas por material | `ImpactStone_*` (piedra), `ImpactMetal_M`, `ImpactGold_S` | todas iguales; se fusionan al tocarse armadas |
| Sello (`Stamp`) | `Stamp_S_0`, `Stamp_M_1`… | en la mano principal; cargar con el grimorio |
| Calcomanías (`Inscribable`) | piso, paredes, cajas | fuera de esta matriz (no pedido) |

## Casos automáticos (`HarnessCanvasMatrix`)
Para cada **agarrable/runa/flotante**:
1. Agarrar y soltar: cae, sin padre y no cinemático.
2. Vista completa (Tab) abre, se puede dibujar y **soltarlo con la vista abierta** lo libera (bug «quedaba pegado a la pantalla»).
3. Tab + fijar en el aire: queda flotando, sin padre, sin modo foco; volver a agarrar lo hace físico de nuevo; soltar cae.
4. Arrojado fuerte (30–60 m/s, 3 ángulos) contra el piso: no lo atraviesa.

Para cada **fijo**: Tab abre, dibujar deja trazos, salir devuelve la cámara a su pose.

Para **herramientas, sellos y movibles**: caída a 45 m/s no atraviesa el piso; los genéricos (movibles) se pueden fijar y desfijar.

### Pruebas dirigidas (Only = ...)
`SessionState "HarnessCanvasMatrix.Only"`: `RUNE` (la runa armada dispara en 3 variantes), `RUNEJOIN` (dos runas armadas se fusionan y se gastan), `SPELLS` (lanza tierra, cristal, agua y viento y revisa sus cuerpos fisicos), `CHARGE` (retardo de lanzamiento: carga, lanzamiento y cancelacion), `FOCUSRAY` (lienzos en su lugar original, agarrados desde 4 lados: el puntero en vista completa debe dibujar en ese lienzo), o nombres separados por coma para repetir solo algunos casos.

## Checklist manual (teclado y mouse reales)
Marcá cada ítem con `[x]` y la fecha/versión cuando lo pruebes.

- [ ] Agarrar una runa, Tab, dibujar un círculo, arrojar con Q: la runa cae y se puede volver a agarrar y dibujar. Con E queda flotando en el aire.
- [ ] Lo mismo fijando con E o con el clic de la rueda (queda flotando; E otra vez no lo suelta, Q lo arroja).
- [ ] Mirar un caballete / pizarra / pared sin agarrar nada: Tab encuadra de frente, el mouse es el puntero, Tab o Esc salen.
- [ ] Esc dentro de la vista completa sale de la vista y **no** abre el grimorio en ese mismo Esc.
- [ ] Cuaderno: pasar páginas con Z/X con y sin Tab; dibujar en cada página; soltar con la vista abierta.
- [ ] Runas: dibujar fuego, golpearla contra la pared y el suelo: el efecto sale sin errores en la consola.
- [ ] Dar vuelta la hoja (F) y verificar que el hechizo sale por la cara con tinta.
- [ ] Sello: cargar un glifo (Tab → «Cargar en el sello»), estampar sobre una hoja, sobre una runa y sobre un lienzo fijo.
- [ ] Mitades SplitJoin: juntarlas lado a lado, el hechizo se lanza una sola vez.
- [ ] Banco/mesa/caja: fijar en el aire (flota con vaivén), volver a agarrar, tirar al piso fuerte (no desaparece).
- [ ] Lápiz / goma lanzados muy fuerte contra el piso, el terreno y la torre: no los atraviesan.
- [ ] Soltar cualquier lienzo con la vista abierta y mover el mouse: el cursor vuelve a estar oculto y la cámara responde.

### Casos VR (sin ejecutar hasta M7)
- [ ] Agarrar hoja/runa con la mano izquierda, dibujar con la derecha, soltar (cae) y arrojar.
- [ ] Runas y placas: golpear/pisar y ver el efecto; la UI del grimorio en world-space (KI-095).
