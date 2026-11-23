# Brief 11: agua e inundación restantes (FIS-04, FIS-06, FIS-07)
Proyecto: SpellSimulator (Unity 6 URP, PC y Quest). Lee `CLAUDE.md` y `Docs/CODESTYLE.md`. Solo editás scripts C# (y tests) dentro de `Assets/_Project/`. Nunca `.meta`, `.unity`, `.prefab`, `.asset`, `.mat`. No podés compilar: código conservador. Cero allocations en Update/FixedUpdate/LateUpdate. No hagas commits. Lógica pura nueva: función estática + test EditMode en `Assets/_Project/Tests/EditMode/`.
Archivos permitidos: los de agua/inundación (`WaterVolume.cs`, `FloodZone.cs`, `WaterMath.cs`, `WaterEffects.cs` solo si hace falta) y sus tests.
Leé las secciones FIS-04, FIS-06 y FIS-07 de `Docs/AUDIT/03_fisicas.md`, verificá cada una en el código real y aplicá la alternativa más simple y segura. Si no es un bug real o implica comportamiento distinto, no lo cambies y explicá. FIS-03 ya está arreglado (contador de colliders por cuerpo): no lo rehagas.
## Devolvé (≤5 líneas)
Por hallazgo: arreglado / no era bug / pospuesto, y archivos tocados.
