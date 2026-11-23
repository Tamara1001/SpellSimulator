# Brief 06: físicas medias (FIS-05, FIS-12, FIS-13)

Proyecto: SpellSimulator (Unity 6 URP, C#). Lee `CLAUDE.md` y `Docs/CODESTYLE.md`. Solo editás dentro de `Assets/_Project/`. Nunca `.meta`, `.unity`, `.prefab`, `.asset`, `.mat`. No podés compilar: código conservador. Cero allocations en `Update/FixedUpdate/LateUpdate`. No hagas commits. No toques `MagicField.cs` líneas de viento/enfriamiento (ya arreglado) ni `WaterVolume/FloodZone/DamageModel/ImpactDamage/NetDuel` (otra tarea).

## Qué arreglar
Leé las secciones completas **FIS-05, FIS-12 y FIS-13** de `Docs/AUDIT/03_fisicas.md` (descripción y alternativas) y verificá cada una leyendo el código real antes de tocarlo (los números de línea pueden haber cambiado):
- **FIS-05** `Combat/Health.cs` y `Reactions/MagicWorld.cs` (~96-105).
- **FIS-12** `Output/Bodies/EarthShapes.cs` (~41-45, 186-191): fuga de referencias/estado.
- **FIS-13** `Output/MagicField.cs` (~20-22) y `Reactions/MagicWorld.cs` (~27-28): limitación silenciosa (probablemente buffers de capacidad fija que descartan sin avisar). Si la solución es agrandar o avisar con un `Debug.LogWarning` limitado (una vez), elegí lo mínimo.
Aplicá la **alternativa más simple y segura** de cada una. Si un hallazgo no es un bug real, no lo cambies y explicá por qué. Lógica pura nueva → función estática + test EditMode en `Assets/_Project/Tests/EditMode/`.

## Devolvé (≤5 líneas)
Por hallazgo: arreglado / no era bug / pospuesto, y los archivos tocados.
