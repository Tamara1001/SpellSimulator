---
tags: [doc, harness]
---
# Harness — cómo verificar cambios

El Editor de Unity suele estar **abierto** mientras se trabaja, así que no se usa `-batchmode`
(Unity no permite dos instancias sobre el mismo proyecto). La verificación se hace contra el Editor vivo
vía **Unity MCP** (`com.unity.ai.assistant`).

## Checklist de verificación (en orden)

1. **Compilación**: después de editar `.cs`, forzar import y leer consola.
   - MCP `Unity_RunCommand` con `AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport)`.
   - `Unity_ManageEditor GetState` → esperar `IsCompiling: false`.
   - `Unity_ReadConsole Types=[Error,Warning]` → **0 errores**. Warnings nuevos: justificar o arreglar.
2. **Tests EditMode** (lógica pura):
   - Menú `SpellSimulator > Harness > Run EditMode Tests`, o por MCP:
     `SpellSimulator.EditorTools.HarnessTestRunner.RunEditMode();`
   - Resultado en `Logs/harness-editmode.txt` (primera línea `PASS`/`FAIL passed=N failed=M …`)
     y en consola con prefijo `[Harness] EditMode:`. Es asíncrono: leer el archivo unos segundos después.
3. **Validación de escena**: `SpellSimulator > Harness > Validate Open Scene` o
   `SpellSimulator.EditorTools.HarnessSceneValidator.Validate()` (devuelve el reporte).
   Chequea singletons del pipeline y referencias serializadas nulas en componentes `SpellSimulator.*`.
4. **Play Mode** (si el cambio es de runtime): `Unity_ManageEditor Play`, ejercitar, leer logs
   (`[Compilador]`, `[VFX Output]`, `[Sigilo]`, `[PlayerInteractor]`…), `Stop`. Captura opcional con `Unity_Camera_Capture`.
5. **VR**: no se puede verificar desde el agente. Dejar en el resumen qué probar en el Quest.

## Assemblies

| asmdef | Ruta | Contenido |
|---|---|---|
| `SpellSimulator.Runtime` | `Assets/_Project/Scripts/` | Todo el código de juego. |
| `SpellSimulator.Editor` | `Assets/_Project/Editor/` | Herramientas del harness (menú `SpellSimulator/…`). |
| `SpellSimulator.Tests.EditMode` | `Assets/_Project/Tests/EditMode/` | Tests NUnit de lógica pura. |

Al agregar un `using` de un paquete nuevo en runtime, sumar su asmdef a `references` de `SpellSimulator.Runtime.asmdef`.

## Escribir tests
- Lógica pura (`Core`, `TensorRasterizer`, fórmulas del compilador) → EditMode, sin escena.
- Fábricas de trazos sintéticos en `TestStrokes` (círculos, líneas, sesiones).
- Nombre: `<Clase>Tests`, método `Condicion_ResultadoEsperado`.
- PlayMode tests: todavía no hay asmdef; crear `Tests/PlayMode/` cuando haga falta (ej. `SpellCompiler` como MonoBehaviour).

## Graphify
Grafo de conocimiento del código (AST, sin LLM) para navegar dependencias y la vista de Obsidian.

```bash
graphify update .                           # re-extrae (incremental) → graphify-out/graph.json
graphify export obsidian --dir Docs/Graph   # notas + canvas para Obsidian
graphify query "¿cómo llega un trazo al SpellCompiler?"
graphify path "PhysicalTool" "SpellOutputController"
graphify god-nodes                          # hubs arquitectónicos
```
- `.graphifyignore` limita el grafo a `Assets/_Project/` y `Docs/` (excluye paquetes, samples, Library).
- `graphify-out/` y `Docs/Graph/` están en `.gitignore`: se regeneran localmente.
- Hay hooks git (`graphify hook install`, locales en `.git/hooks/`) que **re-generan `graph.json` en cada commit/checkout**.
  La vista de Obsidian no se regenera sola: correr el `export obsidian` después.
- Los nombres de comunidades se escribieron a mano en `graphify-out/.graphify_labels.json` (el auto-labeling por LLM
  no tiene backend configurado). Si un re-cluster los vuelve `Community N`, re-etiquetar y re-exportar con
  `--labels graphify-out/.graphify_labels.json`.
- Colores por comunidad en la Graph view: `Docs/.obsidian/graph.json` (grupos por `tag:#community/...`).
