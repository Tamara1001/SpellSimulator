# Plan de acción: lo urgente de VFX y Físicas (+ sistema de daño PVP)

Alcance (tarjetas con etiqueta Urgente): VFX-01, VFX-02, FIS-01, FIS-02, FIS-03 y PVP-03 (solo el sistema de daño). El resto de PVP queda en urgencia 0.
Fuente de cada hallazgo: `Docs/AUDIT/02_modelado_vfx.md`, `03_fisicas.md`, `04_pvp.md`.

## Cómo se trabaja
- Antigravity (agy-jaula) **lee y edita** código en `Assets/_Project/`; **no compila**. Yo compilo y corro tests EditMode con el MCP de Unity al terminar cada tarea.
- agy-jaula admite **una corrida a la vez** (el hook se activa/desactiva por llamada), así que las tareas van en **fila**, no en paralelo. Una tarea = un brief cerrado en `Docs/AUDIT/briefs/NN_nombre.md`.
- Regla de verificación (los modelos baratos se equivocan): cada brief pide **confirmar el bug leyendo el código antes de tocarlo** y devolver ≤5 líneas. Yo reviso `git diff` y compilo.
- Lógica pura → clase estática + test EditMode (`Assets/_Project/Tests/EditMode/`). Nada de allocations en Update/FixedUpdate. Constantes con nombre.
- Nunca `.meta`, `.unity`, `.prefab`, `.asset` ni `.mat` a mano.
- Al cerrar cada tarea: línea en `Docs/CHANGELOG.md`, actualizar `KNOWN_ISSUES.md` si corresponde, commit atómico en español con trailer.

## Orden y tareas

| # | Tarea | Archivos | Brief | Verificación mía |
|---|---|---|---|---|
| 1 | **FIS-01** viento: re-acotar tras `windGain`. **FIS-02** enfriamiento: doble multiplicación por dt (confirmar primero en `ElementReactive.AddExposure` y `MagicWorld.Tick`) | `Output/MagicField.cs`, `Output/Bodies/WindMath.cs` (+ test) | corto | compilar, tests, comparar enfriamiento antes/después con `HarnessPlaySmoke` |
| 2 | **FIS-03** agua/inundación con compound colliders (+ FIS-04/06/07 si caen en los mismos archivos) | `Reactions/WaterVolume.cs`, `FloodZone.cs`, `WaterDropSink.cs` | medio | compilar, tests, cuerpo con 2 colliders en el agua |
| 3 | **PVP-03** sistema de daño: impactos físicos y quemaduras restan vida a duelistas, constantes nombradas de tuning (más bajas que un hechizo directo). Sin tocar lobby, IA ni arena | `Combat/Health.cs`, `ImpactDamage`, `NetPlayer` | medio | compilar, tests de la fórmula de daño |
| 4 | **VFX-01** portar `Binarize.shader` de CGPROGRAM a HLSL/URP con instancing estéreo, **mismo nombre de shader y propiedades** para no romper `Binarize.mat` | `Materials/Binarize.shader` | corto | compilar shader, captura en PC; prueba en Quest queda para la tarjeta "Testear en VR" |
| 5a | **VFX-02 (inventario, solo lectura)**: listar todos los sistemas de partículas de hechizos y sus `maxParticles`, tamaño y overdraw; proponer topes por perfil de calidad | `Output/ElementVfx.cs`, `ElementLook.cs`, `Reactions/BurnEffect.cs`, `WaterEffects.cs`, `World/CrystalShards.cs`, `Lab/LabWindVisual.cs` | corto | revisar la propuesta de números con Tamara |
| 5b | **VFX-02 (implementación)**: multiplicador de presupuesto en `GraphicsQuality` (Quest bajo, PC alto) aplicado al crear cada efecto | `Rendering/GraphicsQuality.cs` + los de 5a | medio | compilar; medición real en Quest queda para "Testear en VR" |

Las tareas 1, 2, 3 y 4 no comparten archivos, pero igual corren en fila por la limitación de agy-jaula. Entre tarea y tarea compilo, así un error no se arrastra.

## Qué NO entra
- Resto de PVP (kit de arena, relectura de hechizos, lobby, HUD VR, AICaster/SealDeck).
- Hallazgos de severidad media y baja.
- Cualquier cambio de escena/prefab: si hace falta, se lo pido a Tamara o lo hago por Unity MCP.

## Riesgos
- El repo está en la rama `rework-mapa` con cambios sin commitear de otras personas (ProjectSettings, escenas, PlayerInputReader…). Solo commiteo los archivos que tocan estas tareas.
- FIS-02 puede no ser un bug real (Antigravity lo dedujo leyendo código): por eso la verificación previa en el brief.
- Sin Quest conectado no puedo medir VFX-02 ni probar VFX-01 en estéreo.
