---
tags: [doc, plan]
---
# Plan de la octava tanda (decisiones de Ale, 2026-10-08)

Orden acordado (bloque 1 hecho, resto pendiente): **1 Controles → 2 Tinta/tinteros/HUD → 3 Hechizos → 4 Runas → 5 Cinturón/grimorio → 6 Mundo y portales → 7 Multijugador.**
Cada bloque: plan corto, implementación, harness (tests + `HarnessPlaySmoke` + matriz de lienzos), commit y push. Los números son
valores iniciales en datos/SO, ajustables tras probar a mano.

## 1. Controles (en curso)
| Acción | Antes | Ahora |
|---|---|---|
| Agarrar | E | **E** (sin nada agarrado) |
| Dejar en el aire | T / clic de la rueda | **E** con algo agarrado (la rueda sigue valiendo) |
| Arrojar | Q | **Q** (ya no existe «soltar»; para dejar algo se arroja o se fija) |
| Grimorio en la mano | Tab | **T** |
| Grimorio a pantalla completa / menú | B | **Esc** (el menú de pausa vive dentro del grimorio; B se elimina) |
| Vista completa del lienzo | V | **Tab** |
| Dar vuelta la hoja | F (también en vista completa) | **F**, **no** disponible en vista completa |
| Rotar objeto | clic der. + mouse (congela la vista) | más sensible y **se puede rotar mientras se camina** |
Fuente de verdad: `GAME_DESIGN.md#controles`; repetir en `Learning/GrimoireTexts.cs` y `PlayerInputReader`.

## 2. Tinta, tinteros y HUD (HECHO 2026-10-09, falta la prueba a mano)
- El gasto por metro se **normaliza al tamaño del lienzo** (un lienzo grande ya no se agota enseguida); el HUD muestra la reserva (%) y los cartuchos.
- **Cartuchos:** slot propio en el HUD con contador (no ocupa los 3 slots de mano). 1 cartucho = 5 usos; **R** gasta un uso y recarga la pluma por completo.
- **Tinteros** de dos familias bien distinguibles (color/forma/etiqueta): **fijos** (zonas del mundo, no equipables, inagotables o de recarga lenta) y **portátiles** (se llevan; dan cartuchos).
- **Tintas especiales** (tinteros de color propio, consumen más por metro): **poderosa** (aumenta mucho la potencia del hechizo) y **resistente** (el hechizo funciona en el agua). Se distinguen por el color del tintero y del trazo.
- **Retardo de lanzamiento:** entre 0,5 y 2 s según el tamaño del hechizo; el anillo **brilla del color del elemento** (fuego rojo, agua celeste…); se puede mover el lienzo y apuntar; si se borra el anillo, se cancela. La dirección se toma al final.

## 3. Hechizos
- **Piedra:** crea **una roca** (tamaño = tamaño del anillo; la tinta poderosa la agranda y pesa más) que al terminar de formarse **cae al suelo**. Es un objeto físico: sirve de **defensa** (muro), **ataque** (arrojarla/lanzarla con modificadores) y **movilidad** (escalón para subir). Se puede mover, apilar y romper sellos. **Desaparece con el tiempo.**
- **Cosas en el aire (fijadas con E):** a **1 min** se caen solas; las rocas desaparecen a los **2 min** (aplica a toda cosa que quedó volando).
- **Agua:** chorro/caudal que empuja objetos y al jugador (partículas con colisión); moja, apaga fuego y rompe sellos (ya existe); **charcos** que escurren por la pendiente; **llena recipientes/zonas** y hace flotar objetos livianos; reacciona con cristal (**hielo**).
- **Viento:** cono de fuerza (arrastre ∝ velocidad²/masa; los livianos vuelan más) que empuja objetos y jugador; aviva y propaga el fuego por el pasto según la dirección; **corriente vertical** para planear/elevarse; dispersa vapor y apaga llamas chicas. **Futuro:** zapatos de viento (doble salto / bota cohete) a lo *Witch Hat Atelier*.
- **Cristal:** **esfera** alrededor del lienzo, radio = tamaño del anillo × factor (valor en datos); lo que toca queda cristalizado: rígido y quieto un rato (pared/puente).
- **Hojas doradas:** cara de emisión **invertida** (el hechizo sale por el dorso sin dar vuelta); objeto raro en repisas/cofres.

## 4. Runas
- **Una sola lógica para todas** (se eliminan las «mitades» sueltas del piso): cada runa funciona por su cuenta.
- **Tipos:** **piedra** (1–2 usos, común), **metal** (3–5 usos, pesada, resiste agua/fuego), **oro** (8+ usos, cooldown corto, rara, brillo propio).
- **Unir:** al tocarse dos runas **armadas** sus dibujos se **fusionan en un solo hechizo**; la más grande manda el tamaño. Es una regla general, no un tipo de runa.
- **Desgaste visible:** la runa se **agrieta** a medida que gasta usos; una gastada se ve gastada → el jugador sabe que hay que **reciclarla** en vez de borrarla y reescribirla (hoy una gastada se reutiliza al borrarla; con el reciclaje cambia: ver bloque 6).

## 5. Cinturón y grimorio
- **Cinturón:** objeto que agrega **2 slots chicos laterales** a la barra (5 en total); valores en un SO.
- **Grimorio:** si el jugador se aleja **más de 12 m** desaparece y vuelve al inventario.

## 6. Mundo, reposición y portales
- **Reposición de lienzos y runas (anti-agotamiento en PvP):** puntos de reaparición fijos (mesas/cofres) con enfriamiento y límite por jugador; **estaciones de reciclaje** (llevar runas gastadas/lienzos y recibir otros: economía cerrada); **tope global** de objetos sueltos (los más viejos se borran); en **Duelo** cada jugador empieza con un mazo fijo.
- **Portales:** ya colocados en el mapa; se activan **dibujando un sello complejo fijo** (receta de glifos) en su placa y viajan al portal par. Cada uno pide una combinación distinta, que se aprende en libros.
- **Estructuras** (cuevas, cabañas, montañas, torre de vigilancia, otras pequeñas): puntos de reaparición y tinteros fijos, coberturas y alturas para PvP, lugares de aprendizaje (libros y glifos), terminales de portales. Se generan desde `MapBuilder` (respetando `MapLayout`).

## 7. Multijugador y PvP (planificación)
- **Netcode for GameObjects + Relay.** Servidor autoritativo para hechizos y física; los clientes predicen movimiento y trazos.
- Antes de codear: separar estado autoritativo de presentación (ya previsto en D3), definir qué se replica (trazos, eventos de cast, objetos físicos agarrados, tinta) y presupuesto de ancho de banda. Empezar con **cooperativo sandbox** para validar la red y después PvP/Duelo.
- Todo lo de bloques 2–6 se escribe pensando en esto: reglas de dominio puras, eventos en vez de referencias cruzadas, límites por jugador y datos en SO.

## Pendientes agregados (a futuro)
- **Más hechizos prearmados** (hoy 7 en `Data/Spells`) y **una forma de descubrirlos todos** (libros, estructuras del mapa, experimentación); revisar que cada uno tenga su camino de descubrimiento.
- **Crear y guardar hechizos propios** desde lo dibujado (ADR-034: `SpellLibrary`, guardar, renombrar, borrar; M6b H1 y H5), con entrada de nombre en PC primero y VR después.
- **Menú de pausa dentro del grimorio** (Esc abre el grimorio a pantalla completa, sin las opciones de pausa todavía).

## Preguntas abiertas
- Cantidades exactas (cooldowns, duraciones, radios): se fijan al implementar cada bloque y se afinan probando a mano.
- Tinta resistente/poderosa: cuánto más gastan por metro (propuesta inicial ×2).
