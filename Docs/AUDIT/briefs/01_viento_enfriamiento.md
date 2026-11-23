# Brief 01: viento (FIS-01) y enfriamiento (FIS-02)

Proyecto: SpellSimulator (Unity 6 URP, C#). Lee `CLAUDE.md` y `Docs/CODESTYLE.md` antes de editar. Idioma: español en comentarios, identificadores en inglés. Solo editás dentro de `Assets/_Project/`. Nunca `.meta`, `.unity`, `.prefab`, `.asset`, `.mat`. No podés compilar: escribí código conservador.

## Contexto (informe `Docs/AUDIT/03_fisicas.md`)
- **FIS-01** `Assets/_Project/Scripts/Output/MagicField.cs` (`WindDrag`, ~línea 470): `WindMath.Drag(...)` devuelve una aceleración acotada a `MaxAcceleration` (45 m/s²), pero después se multiplica por `Mathf.Min(strength, 2f) * windGain` (windGain hasta ~5.2) y no se vuelve a acotar. El tope real puede llegar a ~10x.
- **FIS-02** mismo archivo (`ApplyCool`, ~línea 339): llama `r.AddExposure(chill, Time.fixedDeltaTime * envelope)`. Se sospecha que `ElementReactive.AddExposure` acumula en `pending` y `MagicWorld.Tick` / `ReactionModel.Step` ya multiplica por `dt`, o sea que se multiplica por dt dos veces y el enfriamiento queda ínfimo.

## Hacé
1. **Verificá FIS-02 antes de tocar nada**: leé `Assets/_Project/Scripts/Reactions/ElementReactive.cs`, `MagicWorld.cs` y `ReactionModel.cs` y mirá cómo calientan `ApplyHeat`/similares en `MagicField.cs` (cómo pasan el segundo parámetro de `AddExposure`). Si el segundo parámetro es un **peso/intensidad** (no un dt) y los otros efectos pasan `envelope` sin dt, corregí `ApplyCool` para que sea coherente. Si NO es un bug real, no lo cambies y explicá por qué en la respuesta.
2. **FIS-01**: después de multiplicar por `windGain`, volvé a acotar la magnitud con una constante nombrada nueva (por ejemplo `MaxWindAcceleration`) con comentario de por qué. Poné la lógica de acotado en una función **estática y pura** dentro de `WindMath.cs` (por ejemplo `WindMath.ClampAcceleration(Vector3 a, float max)`), sin allocations.
3. Agregá un test EditMode en `Assets/_Project/Tests/EditMode/` (mirá cómo están los tests existentes de `WindMath`) que cubra el acotado: vector corto no cambia, vector largo queda con magnitud = tope, vector cero devuelve cero.

## Devolvé (≤5 líneas)
Archivos tocados, si FIS-02 era bug real (sí/no y por qué), nombre de la constante nueva y del test.
