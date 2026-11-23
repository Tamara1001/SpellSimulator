# Brief 10: viñeta y post-procesado adaptados a VR (VFX-07)

Proyecto: SpellSimulator (Unity 6 URP, target PC y Quest). Lee `CLAUDE.md` y `Docs/CODESTYLE.md`. Solo editás scripts C# dentro de `Assets/_Project/`. **Nunca** `.asset` (el Volume Profile), `.unity`, `.prefab`, `.meta`: el profile lo toca Tamara/yo por Unity MCP. No podés compilar. Lógica de plataforma solo con `PlatformDetector.IsVR`. No hagas commits.

## Contexto (`Docs/AUDIT/02_modelado_vfx.md`, VFX-07)
El Volume Profile global aplica viñeta y post-procesado pensados para pantalla plana. En VR la viñeta fija incomoda (mareo) y los efectos de pantalla completa (DoF, motion blur, viñeta, grain, lens distortion, chromatic aberration) cuestan mucho en Quest.

## Hacé
1. Leé el informe VFX-07 y buscá con grep qué script gestiona el post-procesado/Volume (por ejemplo `GraphicsQuality.cs`, `PostFx*`, `Volume`) y cómo se aplica por plataforma.
2. Implementá en C#, **en runtime**, que cuando `PlatformDetector.IsVR` sea verdadero se desactiven en el Volume (por `TryGet` de cada override y `active = false`) Vignette, MotionBlur, DepthOfField, FilmGrain, LensDistortion y ChromaticAberration; Bloom y Tonemapping/ColorAdjustments se mantienen. Sin allocations por frame (hacerlo una sola vez al arrancar y al cambiar el perfil de calidad).
3. La decisión (qué efectos quedan según plataforma) va en una **función estática pura** con test EditMode en `Assets/_Project/Tests/EditMode/`.
4. En PC el comportamiento debe quedar idéntico al actual.

## Devolvé (≤5 líneas)
Archivos tocados, lista de efectos desactivados en VR, nombre del test.
