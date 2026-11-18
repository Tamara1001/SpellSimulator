# Puzzles del tutorial (v2)

Cada piso: **un** obstáculo obligatorio, una solución guiada y qué detecta el juego. Todo es obligatorio salvo la palanca de salteo y la práctica opcional del piso 2.

## Terraza (movilidad)
- **Obstáculos:** viga baja (25°) y muro bajo (325°) de borde a borde; reja mágica (195°) sobre la bajada.
- **Solución:** caminar por el balcón en sentido horario; agacharse (C) bajo la viga; saltar (Espacio) el muro. Los dos faroles testigo se encienden y la reja se apaga.
- **Detecta:** `TowerTutorial.WatchTerrace` con `TerraceRules`. Verificado con `HarnessTerraceReach`.
- **Riesgos:** quedar «agachado» al saltar (el estado prone dura 0,35 s: revisar `PlayerInputReader`); altura de cabeza en VR.

## Piso 2 (mover objetos)
- **Obstáculo:** pila de cajas y barril sobre el descanso (198°, junto a la bajada).
- **Solución:** E agarrar/soltar, Q arrojar; despejar el paso a la rampa.
- **Detecta:** la pila deja de tapar el descanso (`TutorialRules`, ya implementado). Opcional: lienzo flotando en el marco 1, de canto en el marco 2, caja a la diana (`TutorialExercise`); encienden su farolito, no traban nada.
- **Riesgos:** cajas al hueco o fuera del alcance; objetos del centro nunca en el camino (la práctica ocupa el disco central, el camino va por el arco sur).

## Piso 1 (objetos mágicos)
- **Obstáculo:** cortina pesada (195°) colgada de una viga, de pared a baranda.
- **Solución:** tomar pluma y tinta (mesa 320°), leer el Tomo I (283°), calcar la plantilla azul en el lienzo (atril 237°: círculo y signo de viento) y apuntar la flecha de apunte a la cortina: el viento la libera.
- **Detecta:** `TutorialCloth` con `ClothRules`; hechizo `gust` real probado con `HarnessSpellTest`.
- **Riesgos:** el hechizo se dispara al cerrar el anillo, así que el lienzo debe estar ya orientado (atril inclinado hacia la cortina, o mostrar la flecha de apunte también sobre el atril); la cortina no debe trabarse por la física.

## Planta baja (la puerta)
- **Obstáculo:** puerta trabada con tres trabas (pestillo 256°, contrapeso 284°, piedra 296°).
- **Solución guiada (la única con cartel):** leer «Los cuatro elementos» (320°), dibujar viento en el lienzo con plantilla (atril 262°) y apuntar al pestillo. Agua y tierra funcionan como atajo, sin cartel ni rótulo.
- **Detecta:** `TowerDoor.Unlock` con las trabas sueltas; el cruce del vano apaga la barrera y termina el tutorial (documentar en `MAPA_FLUJO_PLAN.md` §17).
- **Riesgos:** igual que el piso 1 (orden disparo-apunte); contrapeso y piedra no deben leerse como la solución principal.
