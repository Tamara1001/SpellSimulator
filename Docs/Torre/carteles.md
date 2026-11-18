# Carteles de la torre-tutorial (v2, fuente de verdad)

Reglas: **máx. 2 líneas, ≤ 28 caracteres visibles por línea**, una acción principal por cartel, nombres del juego (viento, lienzo, plantilla, flecha de apunte, pestillo). Nunca «sello» ni «rayo». Alto de ojos: Δy = 1,60 m sobre el piso. Tarjeta 1,30 × 0,50 m. `TextWrappingModes.Normal`, `fontSizeMax` 0,62, margen interno 0,06 m. Cada cartel va ANTES de lo que explica, sobre el camino (círculo amarillo del plano).
Controles confirmados (Docs/GAME_DESIGN.md): E agarrar/soltar/dejar flotando · Q arrojar · F dar vuelta · R recargar tinta · T grimorio · clic derecho + mouse rotar · Tab vista de lienzo · C agacharse · Espacio saltar.

| # | Piso | Línea 1 | Línea 2 | Dónde (ángulo, radio) · mira a |
|---:|---|---|---|---|
| 1 | Terraza | `<b>E</b> · Saltear tutorial` | `Ir directo al Nexo` | 98°, r 7,4 (junto a la palanca) · mira al INICIO |
| 2 | Terraza | `<b>WASD</b> · Caminar` | `<b>Mouse</b> · Mirar` | 72°, r 6,8 · mira al INICIO |
| 3 | Terraza | `<b>C</b> · Agacharse` | `Pasá bajo la viga` | 42°, r 6,6 · mira hacia la viga (25°) |
| 4 | Terraza | `<b>Espacio</b> · Saltar` | `Pasá sobre el muro` | 344°, r 6,6 · mira hacia el muro (325°) |
| 5 | Terraza | `Reja cerrada` | `Completá agacharse y saltar` | 212°, r 5,5 · mira a la reja (195°) |
| 6 | Piso 2 | `Opcional · practicá` | `<b>E</b> agarrar · <b>E</b> flotar` | 8°, r 4,4 · mira a la llegada (0°) |
| 7* | Piso 2 | `1 · Dejalo flotando` | — | sobre el marco 1 (60°, r 3,1) |
| 8* | Piso 2 | `2 · Ponelo de canto` | `<b>Clic der.</b> + mouse` | sobre el marco 2 (110°, r 3,1) |
| 9* | Piso 2 | `3 · <b>Q</b> arrojá la caja` | `a la diana` | junto a la diana (315°) |
| 10 | Piso 2 | `<b>E</b> · Mover las cajas` | `Despejá la bajada` | 216°, r 5,35 · mira a la pila (198°) |
| 11 | Piso 1 | `<b>E</b> · Tomá la pluma` | `<b>R</b> · Recargar tinta` | 338°, r 5,3 · mira a la mesa (320°) |
| 12 | Piso 1 | `<b>E</b> · Leé el Tomo I` | `Es el viento` | 300°, r 5,3 · mira al libro (283°) |
| 13 | Piso 1 | `Calcá el viento azul` | `<b>E</b> y apuntá a la cortina` | 252°, r 5,3 · mira al lienzo (237°) |
| 14 | Planta baja | `Dibujá viento en el lienzo` | `y apuntá al pestillo` | 285°, r 5,2 · mira al lienzo (262°) |

\* Los 7–9 son mini-rótulos de estación opcional (tarjeta 0,9 × 0,28 m); no llevan número en el plano.

Decisiones de Tamara: la planta baja enseña **una sola** solución (viento guiado; agua y tierra funcionan sin cartel). El cartel 5 solo indica estado, no repite controles.
Cambios respecto de la v1: se quita «T abre tu grimorio» (no aporta al objetivo), «sello» pasa a «viento/plantilla» y «rayo azul» a «flecha de apunte» (se usará ese nombre en pantalla y en docs).

Posiciones de estaciones (fuente de verdad, coinciden con los planos `0N_*.svg`): terraza: viga 25°, muro 325°, reja 195°, valla 115°. Piso 2: llegada 0°, pila 198°, práctica en el disco central (mesa 20° r 2,5; marcos 60° y 110° r 3,1; diana 315°). Piso 1: pluma 320°, libro 283°, lienzo 237° (r 3,7), cortina 195°. Planta baja: libro 320°, lienzo 262° (r 3,0), pestillo 256° (r 5,85), puerta 270°.
