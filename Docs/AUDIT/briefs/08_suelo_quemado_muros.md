# Brief 08: suelo quemado proyectado en muros (VFX-03)

Proyecto: SpellSimulator (Unity 6 URP, target PC y Quest). Lee `CLAUDE.md` y `Docs/CODESTYLE.md`. Solo editás el/los `.shader` y, si hace falta, el script que los alimenta, dentro de `Assets/_Project/`. **Nunca** `.mat`, `.meta`, `.asset`. No podés compilar: sé conservador y comentá. No hagas commits.

## Contexto (`Docs/AUDIT/02_modelado_vfx.md`, VFX-03)
El shader `StylizedLit` (buscalo con grep en `Assets/_Project/`) tiene la variante `_GRASS_BURN` que proyecta el quemado del suelo con un mapeo planar en XZ. Aplicado a superficies verticales (muros) se estira y deforma.

## Hacé
1. Leé el informe VFX-03 y el código del shader (cómo calcula las UV del quemado y qué lo alimenta).
2. Arreglá con la alternativa más barata para Quest: atenuar/anular el efecto según la **normal** (usar `abs(normalWS.y)` para fundir a 0 en superficies cerca de verticales con un `smoothstep` y constantes nombradas), en vez de triplanar completo (más texturas = más costo).
3. No cambies nombres de propiedades ni keywords existentes (para no romper materiales). No agregues muestras de textura nuevas.
4. Debe seguir funcionando igual en suelos horizontales.

## Devolvé (≤5 líneas)
Qué cambió, constantes nuevas y cualquier cosa que requiera tocar materiales/escena (para que lo haga otra persona).
