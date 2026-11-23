# Brief 12: mapa, tareas menores (MAP-06, MAP-07, MAP-08)
Proyecto: SpellSimulator (Unity 6 URP, PC y Quest). Lee `CLAUDE.md` y `Docs/CODESTYLE.md`. Solo editás scripts C# (y tests) dentro de `Assets/_Project/`. Nunca `.meta`, `.unity`, `.prefab`, `.asset`, `.mat`. No podés compilar: código conservador. Cero allocations en Update/FixedUpdate/LateUpdate. No hagas commits. Lógica pura nueva: función estática + test EditMode en `Assets/_Project/Tests/EditMode/`.
Leé las secciones MAP-06 (CanvasPile: cooldown y tope en escena con reciclado), MAP-07 (allocations en DisplayStock) y MAP-08 (sitios fijos de peñasco/cabaña) de `Docs/AUDIT/01_mapa_ui.md`. Verificá en el código real. MAP-06 y MAP-07 son código: aplicá la alternativa más simple. MAP-08 si implica tocar escena o datos (`MapLayout`/`MapOverrides.json`), NO lo hagas: explicá qué habría que cambiar.
## Devolvé (≤5 líneas)
Por hallazgo: hecho / no aplica / pospuesto, y archivos tocados.
