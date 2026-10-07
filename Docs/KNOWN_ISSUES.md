---
tags: [doc, bugs]
---
# Known Issues y Deuda Técnica

Formato: `KI-NNN` · severidad (🔴 alta, 🟠 media, 🟢 baja) · estado (`abierto` / `en curso` / `resuelto <fecha>`).
Al resolver uno, no lo borres: marcalo resuelto y anotá el commit.

## Pipeline de hechizos

### KI-001
🔴 `abierto` — **Se usan logits como probabilidades.** El ONNX devuelve `class_logits` sin softmax
(`export_onnx.py` de la referencia). `SentisInferenceEngine.RunInference` toma `probs[bestIdx]` como confianza y
`SpellCompiler` la compara con `MinPrimaryConfidence = 0.45` y la usa en las fórmulas de `quality`/`duration`.
El umbral y los parámetros resultantes no tienen sentido. **Fix:** aplicar softmax (estable, restando el máximo)
antes del argmax. Lógica pura → test.

### KI-002
🟠 `abierto` — **El hechizo spawnea en la posición de `SystemManagers`**, no en el lienzo:
`outputController?.SpawnSpellEffect(spell, this.transform.position)`. Debería usar el `DrawingCanvas` casteado
(posición + normal como dirección). Requiere pasar el lienzo a `ProcessVectorSession`.

### KI-003
🟠 `abierto` — **Deltas semánticos no coinciden con la referencia** aunque el comentario dice que son copiados.
Los sigilos tampoco aplican su `semantic`. Ver tabla en [[GLYPHS]]. Decidir: portar exacto o mantener valores
propios (y entonces corregir el comentario). Ideal: mover el diccionario a un `ScriptableObject`/JSON por glifo.

### KI-004
🟠 `abierto` — **Anillo sin sigilo ⇒ hechizo inválido silencioso.** Si `ring.Found` pero `grouped.Sigil` está vacío,
`ast.Ring.Sigil == null` y `CompileAST` devuelve `ast_empty`. Además si un signo queda dentro del 55% del radio se toma
como parte del sigilo. Agregar diagnósticos al `DiagnosticBus`.

### KI-005
🟠 `abierto` — **`active` siempre `true`.** `ProcessVectorSession` llama `CompileAST(ast, true)`, así que no existe el estado
*preparado* (anillo abierto). `RingDetector` sólo reporta `Found`, no `Closed` vs `Open` (tolera cierre hasta 40%).

### KI-006
🟢 `abierto` — Rotación manual de signos "al norte" en `SentisInferenceEngine` puede ser redundante: el modelo se
entrenó con rotation augmentation. Medir accuracy con y sin rotación antes de decidir.

### KI-007
🟢 `abierto` — `StrokeCleaner`, `RingDetector`, `TensorRasterizer`, `LogTopPredictions` alocan (listas, arrays, lambda en
`Array.Sort`, strings). No es en `Update` (sólo al castear), así que es aceptable, pero `RunInference` crea un
`float[9216]` y una `Texture2D` debug por glifo.

### KI-008
🟢 `abierto` — `SpellCompiler`: campo muerto `classIndexToGlyphName`, `<summary>` de `Compile` pegado sobre ese campo,
parser JSON casero con `Replace/Split` (frágil). Usar `JsonUtility` con wrapper o `Newtonsoft` (ya viene con Unity).

## Jugador / VR

### KI-009
🟠 `abierto` — `PhysicalTool.OnSecondaryUseStart` busca el motor con `FindFirstObjectByType` en cada cast y hardcodea
`CleanSession(…, 0.005f, 3)` (duplicado con `MouseInputHandler`). Centralizar en un servicio de cast.

### KI-010
🟢 `abierto` — `PlayerLook.Awake` bloquea el cursor aunque `PlatformDetector` ya lo hace según plataforma (orden de
Awake no garantizado). Viola la regla "lógica de plataforma sólo en PlatformDetector".

### KI-011
🟢 `abierto` — `PlayerInteractor`: rayo VR rotado 90° "por pedido" en código (`Quaternion.AngleAxis(90, right)`) —
compensación de la orientación del controlador. Mejor resolverlo con un transform hijo "Aim". Crea material con
`Shader.Find("Sprites/Default")` en runtime (puede no estar en build).

### KI-012
🟢 `abierto` — `PlayerInteractor` sólo guarda **un** `IUsable` (`currentUsable`); al agarrar un segundo usable se pisa.

### KI-013
🟢 `abierto` — `EraserTool.eraseRadius` es en UV: en lienzos escalados no‑cuadrados el radio efectivo varía.
`DrawingCanvas` también llama `GetComponent<MeshFilter>()` en cada `GetUVFromWorldPosition` (cachear).

## Proyecto / repo

### KI-014
🟢 `abierto` — `MouseInputHandler` es legacy (flujo de mouse directo + HUD inactivo) y mantiene su propia
`DrawingSession` separada de la del lienzo. Sigue en `SystemManagers` y en la escena. Decidir si se elimina.

### KI-015
🟢 `abierto` — Carpetas VFX duplicadas: `_Project/VFX/` (Fire) y `_Project/Prefabs/VFX/` (resto + `FireSpell_VFX.vfx`).
`_Project/DebugCanvasCapture.png` y `Assets/_Recovery/0.unity` son residuos. `Assets/Editor/CheckURP.cs` y
`VFXDiagnostics.cs` no tienen `[MenuItem]` (sólo invocables por MCP/código).

### KI-016
🟢 `resuelto 2026-10-07` — Sin asmdefs ni tests. Se agregaron `SpellSimulator.Runtime`, `SpellSimulator.Tests.EditMode`
y tests de la lógica pura de `Core`. Ver [[HARNESS]].

### KI-017
🟢 `abierto` — El README describe una estructura (`Assets/Scripts`, `Scenes/PrototypePC.unity`, `Assets/Models`) que no
coincide con la real. Se actualizó la sección y se apunta a [[STRUCTURE]].
