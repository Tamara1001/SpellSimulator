---
tags: [doc, pruebas, mapa]
---
# Prueba manual del rework del mapa (paso 12)

> Lista para recorrer el modo Libre a mano en **PC** (teclado y mouse) y después en **Quest**. Cubre lo que el harness
> no puede ver: legibilidad, sensaciones, distancias reales y rendimiento. Todo lo de acá pasó EditMode (709) y
> `HarnessPlaySmoke` el 2026-11-27, pero **nada se probó a mano**. Anotá lo que falle en [KNOWN_ISSUES](KNOWN_ISSUES.md)
> (KI-149) y tildá acá. Plan y decisiones: [MAPA_FLUJO_PLAN](MAPA_FLUJO_PLAN.md).

**Antes de empezar:** en el grimorio, «Reiniciar progreso» (así se ve el recorrido desde cero) y entrar a Modo Sandbox con
«Hacer el tutorial».

## 1. Torre-tutorial (T1-T4)
- [ ] Terraza: moverse, agacharse bajo la viga y saltar el muro apagan la reja; la palanca roja saltea al Nexo.
- [ ] Piso 2: correr la pila de cajas; la baranda del hueco no deja caer.
- [ ] Piso 1: el papel con la plantilla fantasma y el rayo azul apuntan bien al sostenerlo; la tela se suelta con viento.
- [ ] Planta baja: pestillo, contrapeso y piedra destraban la puerta.
- [ ] Faroles de nivel: se prenden al agarrar una pluma en cada sala.

## 2. Nexo
- [ ] Poste con flechas (Torre, Laboratorio, Entrenamiento, Archivo, Lago): se leen desde el camino y apuntan bien. ¿Tapa «Archivo» a «Lago»?
- [ ] Tablero del mapa: con el progreso en cero late la **torre**; tras leer un tomo de la torre laten **Entrenamiento y Archivo**.
- [ ] Tab sobre el tablero abre la vista completa; los rombos de portal no teletransportan.

## 3. Archivo
- [ ] Poste junto a la entrada (Agua, Fuego, Tierra, Viento, Nexo): legible al entrar.
- [ ] Cada atril tiene su farolito: se **prende al leer** el libro (con sonido) y queda prendido al volver a entrar a la partida.
- [ ] Tras leer un libro del Archivo, en el tablero laten los **4 campos**.
- [ ] **Portal del Archivo** (al sur, entre el arenero y el rincón de movimiento): escribir en su placa la identidad del portal del estudio
      (cristal + colección + región) abre la ruta; cruzarlo lleva frente a la torre. Desde el estudio, escribir **agua + flotar +
      convergencia** vuelve al Archivo. **Medir si el agua se lee como viento** (KI-105).
- [ ] El libro «Caminos del valle» (círculo de piedras) anota los tres portales en el grimorio.

## 4. Campos de elemento
- [ ] Cada campo tiene su estandarte del color del elemento, en el borde, mirando al camino. Se **enciende al leer** el libro de
      hechizos de ese campo (agua, fuego, tierra, viento).
- [ ] Tras leer un libro de hechizos, en el tablero laten los sitios del late game.

## 5. Vida en modo Libre (paso V)
- [ ] Pararse en el pasto en llamas o recibir un hechizo de otro lienzo resta vida; con poca vida aparece el aviso.
- [ ] Al morir se reaparece en el Nexo con el aviso «Te desmayaste»; la vida vuelve llena.
- [ ] Tras ~6 s sin daño la vida se recupera sola.
- [ ] Palanca «Sin daño» del laboratorio (junto a la llegada desde el Nexo): E la prende (farolito verde) y ya no hay daño;
      sigue prendida al reiniciar el juego; E otra vez la apaga.
- [ ] ¿Los hechizos propios lastiman al jugador? (No deberían.) ¿Hace falta una barra de vida en el HUD?

## 6. Circuito exterior (paso 11b)
- [ ] Recorrer la vuelta entera: viento → santuario → círculo → atalaya → puesto → cabaña del norte → cueva → cabaña del bosque → torre.
      ¿Se entiende por dónde seguir en los dos huecos sin camino (junto a la atalaya y frente a la cueva)?
- [ ] La subida de la cabaña del bosque a la torre se camina sin trabarse.
- [ ] No quedaron pinos, rocas ni props encima del camino.

## 7. Quest
- [ ] Repetir 2 a 6 con el visor: legibilidad de flechas, palanca y tablero con el rayo; agarrar libros y lienzos.
- [ ] Rendimiento en el Archivo y en el Nexo (faroles, estandartes y postes son mallas sin luz real; medir con el perfilador).
