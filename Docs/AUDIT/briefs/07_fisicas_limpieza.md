# Brief 07: limpieza de físicas (FIS-08, FIS-10, FIS-11, FIS-14, FIS-15)

Proyecto: SpellSimulator (Unity 6 URP, C#). Lee `CLAUDE.md` y `Docs/CODESTYLE.md`. Solo editás dentro de `Assets/_Project/`. Nunca `.meta`, `.unity`, `.prefab`, `.asset`, `.mat`. No podés compilar: código conservador. Cero allocations en `Update/FixedUpdate/LateUpdate`. No hagas commits.

## Archivos permitidos (no toques otros)
`Reactions/GrassFire.cs`, `World/Flutter.cs`, `Output/LevitationMath.cs`, `Reactions/GroundReactions.cs`, y `Output/Bodies/WindMath.cs` **solo** la parte de FIS-15. No toques `MagicField.cs` (otra tarea la modifica).

## Qué hacer
Leé las secciones **FIS-08, FIS-10, FIS-11, FIS-14, FIS-15** de `Docs/AUDIT/03_fisicas.md`. Verificá cada una en el código real. Aplicá la alternativa más simple y segura:
- Inconsistencias de constantes/comentarios: unificar con una constante nombrada.
- Código muerto: borrarlo solo si confirmás con grep que nada lo usa (incluidos tests).
- Optimización (FIS-14): solo si el cambio es local y no altera el comportamiento.
- FIS-15 (falta): si requiere diseño nuevo, NO lo implementes: dejá un comentario `// TODO(FIS-15)` y explicalo.
Cualquier hallazgo que no sea un bug real o implique comportamiento distinto: no lo cambies y explicalo.

## Devolvé (≤5 líneas)
Por hallazgo: hecho / no aplica / pospuesto, y archivos tocados.
