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

## Mapa del modo Libre (M5, blockout)
Valle de ~140 m cerrado por montañas (se recorre en menos de un minuto caminando). En el centro, la **torre** sobre una
loma: planta baja **biblioteca** (estantes, mesas, escritorio con un papel apoyado que emite hacia arriba), primer piso
**estudio**, segundo piso y **terraza** almenada bajo el techo cónico violeta; los pisos se unen con rampas en espiral.
Al oeste, el **lago** con orilla de arena; al este, el **campo de práctica** (atril con papel, mesa de herramientas,
blancos y muñecos). Caminos de tierra unen el spawn (al sur de la torre) con todo. Bosque de pinos hacia las montañas.
Al oeste, una **cascada** cae de la montaña y sigue en arroyo hasta el lago. **Papeles**: atril del campo (1 m), pared
exterior de la torre mirando al campo (emite hacia afuera), escritorio de la biblioteca (emite hacia arriba) y dos
agarrables chico y mediano en la mesa de lectura.

## Aprendizaje (M6)
Se empieza conociendo **Fuego, Columna y Levitación**. Los libros de la torre enseñan el resto: *Los cuatro elementos*
(atril de la entrada), *Variantes y parientes* (escritorio), *Signos del movimiento* y *Dar forma a la magia* (mesa
del estudio) y *Signos raros* (pedestal en la terraza, premio por subir). Dibujar bien un glifo desconocido también lo
descubre. El grimorio muestra cada glifo dibujándose solo, con su explicación. El progreso se guarda.

## Mover objetos a gusto (M6)
**E** agarra en la mano (herramientas, lienzos, estampas: quedan delante de la vista y las herramientas se ven sostenidas).
**G** (mantener) es el modo "mover": sujeta lo apuntado a distancia como en Garry's Mod; la rueda lo acerca o aleja, **clic
derecho + mouse** lo gira (la vista se congela), **Shift + rueda** lo inclina y **T** lo deja flotando donde está. Sirve para
juntar las mitades de un sello partido o apilar cajas con precisión. **X** hace zoom para ver de cerca; **1–4** cambian el grosor de la
pluma (muy fino a grueso) para sellos chicos o enormes.

## Mundo inflamable (M6)
Los **pinos** y el **mobiliario de la torre** (mesas, sillas, estantes, libros, caballetes) son de madera: arden, contagian a
lo que tienen cerca y se rompen o quedan carbonizados. El **pasto** arde en una grilla de 2 m: el fuego se propaga pero **se debilita
con cada generación** (un fuego básico quema unos 200 m² y se apaga solo; uno alimentado, más), deja el suelo negro y puede encender
árboles; el agua lo apaga. La piedra de la torre no arde. Reiniciar el mundo
(Backspace o el menú del grimorio) lo devuelve todo.

## Lienzos y dibujo cómodo (M6)
Un lienzo que se suelta (E) **cae con física** y se acuesta solo con la cara hacia arriba (una mesa lo recibe). Con **G**
queda **flotando** en el aire con un vaivén suave. Con **Q** se arroja. **F** da vuelta la hoja: el hechizo sale por la **cara con tinta** (la que miraba al jugador cuando
empezó a dibujar), así que girarla cambia hacia dónde apunta. **V** fija el lienzo delante de la cámara y el mouse pasa
a ser un puntero (se congela la vista; V o E para volver). Hay cuadernos (S), lienzos grandes con marco, pergaminos
flotantes, pizarra de pared, caballetes (torre y campo) y una mesa de dibujo inclinada.

## Dibujar en el entorno (M6)
Con la pluma se puede dibujar sobre el **piso, la torre y las cajas/pesas**: al empezar el trazo se pega una calcomanía de
tinta (lienzo transparente de 1,2–1,3 m, o del tamaño del objeto) y el hechizo sale hacia afuera de esa superficie. La
calcomanía sigue al objeto si se mueve y desaparece si se rompe o al reiniciar. Un **cráter, una quemadura o un charco**
encima del sello lo rompen (menos los de su propio hechizo), igual que mojar o prender fuego el objeto.

## Objetos movibles (M6)
Cajas, bloques, pesas y baúles son `Movable`: E los lleva con un resorte (chocan con el mundo y no empujan al jugador),
la rueda los acerca o aleja y Q los arroja. Se levantan hasta 600 kg y los grandes se llevan más lejos. La torre tiene mesas de trabajo con
plumas, pinceles, gomas, trapos y tinteros, cajas y baúles.

## Grimorio como menú (M6)
Tab lo abre; no se cierra al alejarte. La portada es un menú: **Glifos** (índice de lo descubierto, con detalle de cada uno,
anterior/siguiente), **Controles**, **Sellos y estampas** y **Opciones**. Se aprieta apuntando + E. **V** lo fija a la cámara a
pantalla casi completa (clic con el mouse). El borde de abajo se agarra con E para llevarlo. Empezás conociendo los 8
sigilos y los signos columna, levitación, dispersión y atracción (para copiarlos con estampas).

## Sellos partidos (M6)
Junto a las piedras de impacto hay dos **mitades de sello** de piedra. Dibujá cada parte del sello en una mitad (por ejemplo,
medio anillo y el sigilo en una, el resto en la otra) y juntalas **lado a lado**, con la tinta hacia el mismo lado: tras 0,4 s los
trazos se unen en un solo sello y se lanza. Las mitades se borran al lanzar. Una mitad sola nunca lanza.

## Sellos en objetos (M6)
Un objeto con `SealTrigger` no lanza el sello al cerrar el anillo: lo deja **armado** (el papel pulsa igual que uno
preparado) y se lanza cuando algo lo activa. En el campo de práctica, junto a la mesa de estampas: dos **piedras de
impacto** (se arrojan; el golpe fuerte, desde 3 m/s, lanza el sello) y una **placa de presión** con una **pesa** (lo pesado
apoyado más de 0,25 s la activa; también el jugador). Tras lanzarse, el sello queda gastado: se borra y se vuelve a dibujar. La placa es **permanente**: se re-arma sola a los
3 s y aguanta 5 usos (con la pesa encima dispara las 5 veces seguidas).

## Estampas (M6)
Tres tamaños en la mesa del campo de práctica (S 8 cm, M 20 cm, L 34 cm) y una M en el escritorio de la biblioteca. Se
cargan desde la página de un glifo del grimorio (5 usos por carga). Un **sigilo** se estampa con su anillo **abierto**:
el sello queda preparado y se lanza al cerrarlo con la pluma (el tamaño de la estampa es el tamaño del sello y su
potencia). Un **signo** se estampa del tamaño del anillo que ya hay en el papel, apuntando a su centro.

## Audio (M5)
Música ambiental suave (pads + campanitas), viento y pájaros de fondo, cascada que se oye al acercarse. Cada acción
suena: pluma/pincel al dibujar, goma, trapo, tintero, campanita de sello **preparado**, lanzamiento distinto por
elemento, chisporroteo si el sello falla, fuego que arde, vapor al apagarse, salpicaduras, roturas de madera y piedra,
rocas que emergen, pasos según el piso (pasto, madera, piedra). Créditos: sonidos de Kenney (kenney.nl), CC0.

## Controles

Esquema (ADR-032): una tecla por idea, las parecidas juntas. Todo se puede repasar en el grimorio (Tab → Controles).

| Grupo | Acción | PC | VR |
|---|---|---|---|
| Moverse | Caminar / mirar | WASD + mouse | Stick izquierdo + cabeza |
| | Correr | Shift | Click del stick izquierdo |
| | Agacharse (mantener) | C o Ctrl | — |
| | Tumbarse (alterna) | Z | — |
| | Saltar | Espacio | — |
| | Zoom (mantener) | X | — |
| Manos | Agarrar / soltar (herramientas, lienzos, estampas) | E | Grip |
| | Arrojar | Q | B |
| | Dejar el lienzo flotando | T | — |
| | Dar vuelta la hoja | F | Girar la mano / click del stick derecho |
| | Lienzo a pantalla (dibujar con el mouse) | V | — |
| Mover objetos | Sujetar a distancia (mantener) | G | — |
| | Acercar / alejar | Rueda | — |
| | Rotar | Clic derecho + mouse | — |
| | Inclinar sobre el eje de la mira | Shift + rueda | — |
| | Dejarlo flotando donde está | T mientras lo sostenés | — |
| Dibujar | Dibujar / estampar / usar | Clic izquierdo | Gatillo |
| | Deshacer | Clic derecho | A |
| | Grosor de la pluma | 1, 2, 3, 4 | — |
| | Recargar tinta | R | Mojar en un tintero |
| Menú | Grimorio | Tab (a pantalla: B) | Botón de menú izquierdo |
| | Botones del grimorio | Apuntar + clic izquierdo | Apuntar + gatillo |
| | Cargar una estampa | Estampa en mano + glifo abierto + "Cargar en la estampa" | igual con el gatillo |
| | Panel de depuración | F3 | — |
| | Reiniciar el mundo | Retroceso | Y |
| | Liberar el cursor | Escape | — |

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
