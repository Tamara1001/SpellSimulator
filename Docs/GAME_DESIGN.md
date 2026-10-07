---
tags: [doc, diseño]
---
# Game Design Document

> Visión final del simulador. Lo que todavía no está decidido figura en *Preguntas abiertas* al final.
> Plan de implementación en [[ROADMAP]]; diseño técnico en [[SYSTEMS_DESIGN]]; reglas mágicas en [[MAGIC_SYSTEM]].

## Pilares
1. **La magia se dibuja.** Todo hechizo sale de un trazo físico. No hay botones de "lanzar fuego".
2. **El trazo importa.** Tamaño, prolijidad, posición y dirección de cada signo cambian el resultado (canon de WHA).
3. **El mundo responde.** Cada hechizo deja huella: quema, moja, rompe, empuja.
4. **Diegético.** Menús, inventario y aprendizaje viven en objetos (el libro, los papeles, los tinteros), no en HUD.
5. **PC y VR por igual.** Toda mecánica tiene que sentirse bien con mouse y teclado y con controles de VR.
6. **Estilo ilustración WHA.** Toon/acuarela, líneas de tinta, colores pastel y magia con trazo y brillo. Barato en Quest.

## Modos

### Modo Libre (prioridad para la evaluación)
Simulador puro, sin fallar ni morir. Se aprende, se experimenta y se rompe todo.

- **Mapa** (muy chico, una sola escena):
  - **La torre**: varios pisos con bibliotecas, mesas de trabajo, atriles con papeles fijos, tinteros, estantes de
    herramientas y sellos, y una ventana o balcón.
  - **La pradera**: alrededor de la torre, con pasto, rocas, árboles y objetos de prueba (barriles, cajas, dummies).
  - **El lago**: agua interactiva. El pull la atrae, el fuego la evapora y el viento hace olas.
  - **Montañas**: borde natural del mapa (fondo, no transitable).
- **Progresión por descubrimiento**: arrancás con el anillo, un sigilo (fuego) y 1‑2 signos. Leer los libros de la
  biblioteca agrega glifos al libro-menú, con explicación y animación de trazo. Un glifo sin descubrir **igual
  funciona** si lo dibujás bien: al hacerlo se "descubre" y se agrega al libro.
- **Reinicio**: un objeto o acción del libro restaura el mundo (mesas, paredes, rocas).

### Modo Duelo (fase posterior)
Mismo sistema mágico, en una arena de combate.

- **Arena**: un escenario circular con coberturas destructibles, alturas y una fuente de agua.
- **Reglas**: contador de tiempo y vida. Los hechizos hacen daño según el elemento, la fuerza, el caudal y el impacto.
  Gana quien deja al otro en 0 o tiene más vida al terminar el tiempo.
- **Recursos**: la tinta (un tintero que se agota; recargar lleva tiempo y te expone) y los **sellos preparados con
  cargas limitadas**, armados antes del duelo como un mazo. Lo demás se dibuja en vivo.
- **Contrajuego**: el agua moja y rompe los sellos rivales, el fuego quema sus papeles, la tierra levanta muros y el
  viento desvía proyectiles.
- **Rival**: primero una **IA** que dibuja sellos a partir de las plantillas del diccionario (dificultad = velocidad +
  ruido en el trazo + táctica). El online viene después: la arquitectura se separa desde ya para no impedirlo.

## Objetos (catálogo)

| Objeto | Mano | Uso primario | Uso secundario | Notas |
|---|---|---|---|---|
| **Libro-menú (grimorio)** | Izq. | Abrir y hojear | Volver a la portada | Glifos descubiertos, opciones del juego, modo y reinicio. Las páginas son superficies: se pueden cargar sellos desde ellas. |
| **Papel** (chico, mediano, grande) | Izq. o fijo | Superficie de dibujo | — | El tamaño es el radio máximo del anillo ⇒ caudal (ley extensiva). Algunos están fijos en atriles o paredes; otros se agarran. |
| **Lápiz / pluma** | Der. | Dibujar (gasta tinta) | Deshacer el último trazo | Trazo fino y preciso. |
| **Pincel** | Der. | Dibujar trazo grueso | Deshacer | Más rápido para anillos grandes; gasta más tinta y es menos preciso. |
| **Piedra de rayar** | Der. | Grabar en piedra o madera | — | Sellos permanentes (contraptions de piedra). No gasta tinta, pero es lento. |
| **Goma** | Der. | Borrar trazos | — | Borra trazo a trazo. |
| **Trapo** | Der. | Borrar el lienzo entero | — | Pasarlo sobre la superficie la limpia. |
| **Tintero** | Cualquiera | Recargar la herramienta (mojar la punta) | — | Se lleva encima. En PC, `R` recarga desde el tintero cargado. Se puede tirar y romper. |
| **Estampa / sello vacío** (tamaños S, M y L) | Der. | Estampar en una superficie | Cargar: apretarla contra un glifo del libro | Un sello por estampa. El tamaño de la estampa es el tamaño del sello. |
| **Objetos lanzables** (piedra, papel plegado) | Der. | Lanzar | — | Si tienen un sello, se activan al impactar. |
| **Mesas, sillas, estantes, paredes** | — | — | — | Destructibles, inflamables y superficies inscribibles. |

## Flujo de un hechizo (jugador)
1. Agarrar un papel (o acercarse a uno fijo) y una herramienta con tinta.
2. Dibujar el **sigilo** al centro y los **signos** alrededor (o estampar un sello del libro).
3. **Cerrar el anillo** ⇒ el hechizo se lanza en la dirección de la cara con tinta.
   Si el anillo queda abierto, el sello está **preparado**: brilla y espera a que lo cierres.
4. El mundo reacciona. Los diagnósticos (en el papel o en el libro) explican qué se reconoció.

## Controles

| Acción | PC | VR |
|---|---|---|
| Moverse / mirar | WASD + mouse | Stick izquierdo + cabeza |
| Agarrar / soltar | E | Grip |
| Usar herramienta (dibujar, estampar, borrar) | Click izquierdo | Gatillo |
| Secundario (deshacer, cargar estampa) | Click derecho | A |
| Recargar tinta | R | Mojar la punta en un tintero |
| Abrir el libro-menú | Tab | Mirar la muñeca izquierda o agarrar el libro del cinturón |
| Lanzar | Soltar mientras te movés / Q | Soltar con velocidad (física) |
| Liberar cursor | Escape | — |
| Reiniciar el mundo (Libre) | Backspace (gamepad: Select) | Y (control izquierdo) |

## Decisiones (2026-10-07)
1. **Auto-activación**: el hechizo se lanza solo al **cerrar el anillo**. El secundario de las herramientas es deshacer.
   Consecuencia de diseño (canon): *el anillo se cierra al final*. Si el anillo ya estaba cerrado y el sello era
   inválido, cada trazo nuevo vuelve a intentar el lanzamiento. Un sello lanzado queda **gastado** hasta que se borre
   o se vuelva a abrir el anillo.
2. **Hechizos activos simultáneos**: se define midiendo rendimiento en Quest (tarea de M3/M7). Valor provisorio: 6.
3. **El personaje recibe efectos** en el modo Libre: empujones, viento y salpicaduras. No recibe daño. Para probar
   el daño hay **muñecos de práctica** (tela, arden; vuelven a aparecer a los 4 s).
4. **Sellos grabados en piedra** (definido):
   - Son permanentes y se reactivan tocándolos (secundario sobre el sello) o con su trigger.
   - Cooldown = `max(5 s, 2 × duración del hechizo)`.
   - **Durabilidad de 5 usos**: cada uso agrieta la piedra (visual) y al quinto se parte.
   - Un golpe de tierra o una explosión fuerte también la rompe.
   - Grabar es lento (≈ 3× el tiempo de dibujar) pero no gasta tinta.
5. **Duelo** (valores iniciales, todo en un SO de balance):
   - Vida 100 por jugador; ronda de **3 minutos**; al mejor de 3 rondas.
   - Tinta: depósito de 100 por herramienta y regeneración pasiva de 2/s. Dos tinteros en la arena recargan
     completo en 1.5 s, pero te dejan expuesto.
   - Mazo: **3 sellos preparados, 1 carga cada uno**, elegidos antes de la partida. Estamparlos es casi instantáneo.
   - Daño de referencia:
     - Impacto directo: 8–30, según `force × caudal × quality`.
     - Quemadura: 4/s mientras dura.
     - Aplastamiento con tierra o fragmentos: hasta 25.
     - Agua y viento: casi no dañan, pero empujan, derriban y rompen sellos rivales.
   - Arena circular simétrica de ~24 m de diámetro, con 4 coberturas destructibles y una fuente central.
6. **Audio**: música (ambiental en Libre, más tensa en Duelo) + SFX diegéticos (pluma, tinta, fuego, agua, viento, roturas).
7. **Tutorial**: sí, pero más adelante (después de M6).

## Preguntas abiertas
- Ninguna bloqueante. Los números del Duelo se ajustan jugando (D1).
