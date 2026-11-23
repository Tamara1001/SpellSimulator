# Brief 03: sistema de daño en duelo (PVP-03)

Proyecto: SpellSimulator (Unity 6 URP, C#, Netcode). Lee `CLAUDE.md`, `Docs/CODESTYLE.md` y `Docs/KNOWN_ISSUES.md` antes de editar. Idioma: español en comentarios, identificadores en inglés. Solo editás dentro de `Assets/_Project/`. Nunca `.meta`, `.unity`, `.prefab`, `.asset`, `.mat`. No podés compilar: escribí código conservador. Cero allocations en `Update/FixedUpdate/LateUpdate`.

## Contexto (informe `Docs/AUDIT/04_pvp.md`, PVP-03)
Los duelistas son inmunes a los impactos físicos (`ImpactDamage`) y a las quemaduras: solo reciben daño de proyectiles mágicos directos. Decisión de Tamara: **todo daña, con constantes de tuning nombradas y más bajas que un hechizo directo**. Alcance mínimo: que exista el sistema de daño. NO tocar lobby, IA rival, arena, kit de dibujo ni HUD.

## Hacé
1. Leé `Assets/_Project/Scripts/Combat/Health.cs`, el componente `ImpactDamage` (buscalo con grep), `Reactions/BurnEffect.cs` / `GrassFire.cs` (cómo se aplica el fuego) y `Assets/_Project/Scripts/Net/NetPlayer.cs`. Averiguá por qué los duelistas son inmunes (filtro por tag/layer, bandera, falta de componente, etc.) y citá el lugar exacto.
2. Hacé que impactos físicos de objetos con suficiente energía y la exposición al fuego resten vida a un duelista a través de **la misma ruta que ya usa el daño mágico** (no inventes una segunda vía de daño). En red, el daño lo decide el servidor (mirá cómo lo hace el daño mágico y seguí ese patrón).
3. Constantes de tuning con nombre y comentario, en un solo lugar (por ejemplo una clase estática `DuelDamageTuning`): factor de daño por impacto, velocidad/energía mínima para dañar, daño de fuego por segundo, tope de daño por impacto. Valores: más bajos que un hechizo directo; usá los valores del daño mágico existente como referencia y citá cuál usaste.
4. La fórmula (impacto → daño) va en una **función estática pura** con test EditMode en `Assets/_Project/Tests/EditMode/`: por debajo del umbral no daña, escala con la energía, respeta el tope.
5. Si algo no se puede resolver sin tocar escenas/prefabs (por ejemplo un componente faltante en el prefab `NetPlayer`), NO lo edites: dejalo anotado en la respuesta.

## Devolvé (≤5 líneas)
Causa de la inmunidad, archivos tocados, nombre de las constantes y del test, y lo que quedó pendiente por prefab/escena.
