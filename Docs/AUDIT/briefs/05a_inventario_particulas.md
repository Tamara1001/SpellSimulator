# Brief 05a: inventario de partículas para el presupuesto en Quest (VFX-02), SOLO LECTURA

Proyecto: SpellSimulator (Unity 6 URP, target PC y Meta Quest). Lee `CLAUDE.md`. **No edites código.** Escribí el resultado en `Docs/AUDIT/briefs/05a_resultado.md`.

## Contexto (informe `Docs/AUDIT/02_modelado_vfx.md`, VFX-02)
Los efectos mágicos saturan partículas y fill-rate en Meta Quest. Decisión de Tamara: un **presupuesto por perfil de calidad** (Quest bajo, PC alto), reutilizando el sistema de calidad existente (`Assets/_Project/Scripts/Rendering/GraphicsQuality.cs`).

## Hacé
1. Leé `Rendering/GraphicsQuality.cs`: qué perfiles existen, cómo se detecta Quest/VR (`PlatformDetector.IsVR`) y qué parámetros ya ajusta.
2. Inventariá dónde se **crean** sistemas de partículas en runtime: `Output/ElementVfx.cs`, `Output/ElementLook.cs`, `Reactions/BurnEffect.cs`, `Reactions/WaterEffects.cs`, `Output/Bodies/WaterSpell.cs`, `World/CrystalShards.cs`, `Lab/LabWindVisual.cs` y cualquier otro que encuentres con grep (`ParticleSystem`, `maxParticles`, `rateOverTime`, `emission`).
3. Armá una tabla: archivo:línea | efecto | `maxParticles` | emisión (rate/burst) | tamaño | blending (aditivo/alpha, gran overdraw) | ¿hay tope por escena?
4. Proponé **números concretos**: multiplicador de presupuesto por perfil (por ejemplo Quest 0.35, PC bajo 0.6, PC alto 1.0) para `maxParticles` y `rateOverTime`, y un **tope global de partículas vivas** por perfil. Señalá los 3 efectos más caros en overdraw.
5. Decí dónde conviene aplicar el multiplicador (un único punto en `GraphicsQuality` + una función estática que los efectos llaman al crearse) y qué archivos habría que tocar.

## Devolvé (≤5 líneas)
Ruta del resultado, los 3 efectos más caros y el multiplicador propuesto por perfil.
