# Brief 09: culling por distancia de las ondas de agua (VFX-05)

Proyecto: SpellSimulator (Unity 6 URP, target PC y Quest). Lee `CLAUDE.md` y `Docs/CODESTYLE.md`. Solo editás el shader `StylizedWater` (buscalo con grep en `Assets/_Project/`) dentro de `Assets/_Project/`. **Nunca** `.mat`, `.meta`, `.asset`. No podés compilar: sé conservador y comentá. No hagas commits.

## Contexto (`Docs/AUDIT/02_modelado_vfx.md`, VFX-05; VFX-06 queda fuera de esta tarea)
El shader calcula un bucle estático de 16 ondas por fragmento/vértice sin importar la distancia a la cámara: costo desperdiciado lejos y en Quest.

## Hacé
1. Leé el shader y localizá el bucle de ondas.
2. Reducí el costo con distancia: calculá `float d = distance(_WorldSpaceCameraPos, positionWS)` y elegí cuántas ondas calcular por tramos (por ejemplo 16 cerca, 8 media, 4 lejos) con constantes nombradas, evitando saltos visibles (atenuar la amplitud de las ondas que se apagan, no cortarlas de golpe). Preferí que el bucle tenga cota constante en tiempo de compilación para no perder el unroll en móvil (por ejemplo, usar `[branch]` por tramo con tres bucles de cota fija, o multiplicar por un peso 0 en las ondas sobrantes).
3. No cambies nombres de propiedades ni keywords existentes. Soporte estéreo existente intacto.
4. Con la cámara cerca el resultado debe ser idéntico al actual.

## Devolvé (≤5 líneas)
Qué cambió, tramos y distancias elegidas, y riesgos visuales.
