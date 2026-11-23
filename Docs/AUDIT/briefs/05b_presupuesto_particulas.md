# Brief 05b: presupuesto de partículas por perfil de calidad (VFX-02), IMPLEMENTACIÓN

**Prerrequisito:** `Docs/AUDIT/briefs/05a_resultado.md` existe y Tamara aprobó los números. Los valores finales de los multiplicadores los define ese archivo (si dice otra cosa que este brief, manda el archivo aprobado).

Proyecto: SpellSimulator (Unity 6 URP, target PC y Meta Quest). Lee `CLAUDE.md` y `Docs/CODESTYLE.md`. Solo editás dentro de `Assets/_Project/`. Nunca `.meta`, `.unity`, `.prefab`, `.asset`, `.mat`. No podés compilar. Cero allocations en `Update/FixedUpdate/LateUpdate`.

## Hacé
1. En `Assets/_Project/Scripts/Rendering/GraphicsQuality.cs` (o una clase estática nueva `ParticleBudget` en la misma carpeta) exponé `ParticleBudget.Scale` (multiplicador 0..1 según el perfil actual) y, si el informe lo pide, un tope global de partículas vivas. La función que calcula el multiplicador a partir del perfil debe ser **estática y pura**, con constantes nombradas.
2. Aplicá el multiplicador en **un único punto por efecto**, al crear el `ParticleSystem` (sobre `main.maxParticles` y `emission.rateOverTime`/bursts), en los archivos listados en el informe 05a. No toques los efectos que el informe marca como baratos.
3. Que el multiplicador se relea si cambia el perfil de calidad en runtime (si ya existe un evento de cambio de calidad en `GraphicsQuality`, suscribite; si no, aplicá al crear y listalo como limitación).
4. Test EditMode en `Assets/_Project/Tests/EditMode/` para la función pura: cada perfil devuelve el valor esperado, y el resultado queda entre 0 y 1.
5. No cambies el aspecto de los efectos en perfil alto (multiplicador 1.0 = comportamiento actual).

## Devolvé (≤5 líneas)
Archivos tocados, clase y constantes nuevas, nombre del test, y limitaciones (por ejemplo cambio de calidad en caliente).
