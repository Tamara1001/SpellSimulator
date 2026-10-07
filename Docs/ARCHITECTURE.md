---
tags: [doc]
---
# Arquitectura

> Estado al 2026-10-07 (reestructura del pipeline). Si cambiás un contrato, actualizá este archivo.

## Pipeline de un hechizo

```mermaid
flowchart LR
    PIR[PlayerInputReader] --> PI[PlayerInteractor]
    PI -- IUsable --> PT[PhysicalTool<br/>Pen / Eraser]
    PT -- raycast → UV --> Tool[PenTool / EraserTool]
    Tool --> DC[DrawingCanvas<br/>Session + RenderTexture]
    PT -- secundario: Cast --> SC[SpellCaster]
    SC --> CL[StrokeCleaner]
    CL --> SP[SealParser<br/>RingDetector + StrokeGrouper<br/>→ ParsedSeal]
    SP --> RC[SentisGlyphClassifier<br/>TensorRasterizer · batch · TTA×8 · softmax]
    RC --> AB[GlyphAstBuilder<br/>+ GlyphDictionary]
    AB --> COMP[SpellCompiler<br/>SemanticRules · SpellQuality · SpellDirection]
    COMP --> PLAN[PlanResolver<br/>lectura + plan: chorro, abanico,<br/>vortice, orbe, succion]
    PLAN --> IR[[SpellIR + SpellPlan]]
    IR --> OUT[SpellOutputController]
    OUT --> MAN[SpellManifestation<br/>pistas de particulas por primitiva]
    OUT --> FLD[MagicField<br/>fuerzas sobre Rigidbodies]
    SC -. DiagnosticBus .-> UI[DiagnosticUIController]
```

Todo entre `SpellCaster` y `SpellIR` es **lógica pura** (estática, testeada) excepto el clasificador.

## Módulos (`Assets/_Project/Scripts/<Modulo>/`, namespace `SpellSimulator.<Modulo>`)

| Módulo | Clases | Responsabilidad |
|---|---|---|
| `Core` | `Stroke`, `DrawingSession`, `GeometryUtils`, `StrokeCleaner`, `InkReservoir` | Datos de trazo y geometría 2D. Ángulos en grados matemáticos con Y arriba (como la referencia). |
| `Canvas` | `DrawingCanvas` | Dueño de su `Session` (UV) y de una `RenderTexture`. `DrawAt`, `Redraw`, `Clear`, `GetUVFromWorldPosition`, `UvToIsotropic`, `SurfaceNormalLocal`. |
| `Input.Tools` | `IDrawingTool`, `PenTool`, `EraserTool`, `RagTool` | Editan la sesión del lienzo desde UV. Pen con estabilizador exponencial; Eraser con radio en metros. |
| `Parsing` | `RingDetector`, `RingInfo`, `StrokeGrouper`, `SymbolCandidate`, `RingLayers`, `SealParser`, `ParsedSeal` | Geometría del sello: ajuste de círculo (Kasa), cobertura angular → anillo cerrado/abierto, capas radiales, candidatos con métricas (`sizeNorm`, `radiusNorm`, `angleDeg`, orientación, forma). |
| `Recognition` | `TensorRasterizer`, `GlyphClassifier` (puro, testeable; lee clase **y pose**), `SentisGlyphClassifier` (componente), `IGlyphClassifier`, `GlyphPrediction` (incluye `RotationDeg`), `ClassificationMath`, `PoseMath` | Rasterizado idéntico al de entrenamiento, inferencia por lotes, TTA de 8 rotaciones, softmax. |
| `Glyphs` | `GlyphDictionary`, `GlyphEntry`, `GlyphSemantic`, `GlyphDictionaryAsset` | Diccionario como **datos** (JSON de la referencia) → rol, elemento, tamaño de referencia, capas permitidas, semántica, plantillas. |
| `Compiler` | `GlyphAST`, `GlyphAstBuilder`, `GlyphWarnings`, `SpellCompiler`, `SemanticRules`, `SpellQuality`, `SpellDirection`, `SpellIR`, `SpellPlan`, `PlanResolver` | Port de `compiler/` de la referencia, incluida la lectura (orientación por pose) y el plan (R‑05 a R‑21). Region, weave y orb todavía aportan solo potencia. |
| `Casting` | `SpellCaster`, `SealActivation`, `SealReader` (lectura guiada: prueba agrupaciones —afinidad, fusión de la capa media, sigilo sin partes sueltas, radial— y elige la que el modelo entiende) | **Único punto de entrada** para lanzar: auto-activación al cerrar el anillo, fases del sello, orquesta el pipeline, diagnósticos (incluye la lectura de cada signo: lado y hacia dónde apunta), eventos `SpellCompiled` / `SealPhaseChanged`. `SealRecord` graba cada sello lanzado para reproducirlo como fixture. |
| `Output` | `SpellOutputController`, `SpellManifestation` (pistas de partículas + tiempos R‑01), `MagicField` (física por arrastre al flujo), `ElementLook` (SO por elemento), `SealFrame`, `SealFeedback`, `SealAnnotations` + `SealAnnotationLayout` (qué se leyó, escrito sobre el papel) | Prefab por elemento, sobre el lienzo, orientado por `direction` (papel → mundo), parámetros al VFX Graph. |
| `Player` | `PlatformDetector`, `PlayerInputReader`, `PlayerMovement`, `PlayerLook`, `PlayerInteractor` (recarga de tinta), `InkSupply`, `Haptics` | Rig PC/VR, input por código, agarrar/soltar/usar por mano. |
| `World` | `PhysicalTool` (lápiz, pincel, goma, trapo; tinta; lee un `ToolDefinition`), `ToolDefinition` (SO), `PhysicalCanvas`, `Inkwell`, `Respawnable` | Objetos agarrables/usables. |
| `Interfaces` | `IInteractable`, `IUsable`, `HandType`, `IInkUser`, `IInkSource` | Contratos jugador ↔ objetos. |
| `Debug` (`DebugTools`) | `DiagnosticBus`, `DiagnosticUIController` | Tensor, top‑3 y log del compilador en la UI. |

Dependencias (sin ciclos): `Core` ← `Parsing` ← `Compiler` → `Recognition`, `Glyphs`; `Casting` usa todo; `World` → `Casting`; nada depende de `World`/`Player` salvo `World`.

## Contratos clave

- `SpellCaster.OnStrokeFinished(canvas)`: al terminar un trazo de tinta evalúa el sello y, si el anillo quedó cerrado,
  **lanza** (canon). `OnSealEdited(canvas)` tras borrar o deshacer (nunca lanza). Fases en `SealActivation`:
  `Drawing → Prepared (anillo abierto) → Spent (lanzado)`; reabrir el anillo rearma el sello. Evento `SealPhaseChanged`.
- `SpellCaster.Cast(DrawingCanvas) → SpellIR` (+ evento `SpellCompiled`): compila y lanza sin mirar la fase.
- `IGlyphClassifier.Classify(IReadOnlyList<DrawingSession>) → GlyphPrediction[]` (probabilidades por candidato).
- `SealParser.Parse(DrawingSession) → ParsedSeal`; orden de candidatos: sigilo central primero, luego signos.
- `GlyphAstBuilder.Build(ParsedSeal, predicciones, GlyphDictionary) → GlyphAST`.
- `SpellCompiler.Compile(GlyphAST, float time) → SpellIR`.
- `SpellOutputController.SpawnSpellEffect(SpellIR, DrawingCanvas, Vector2 ringCenterUV) → SpellManifestation`.
- `SpellIR.plan` (SpellPlan) y `SpellIR.sealRadius` (m, lo completa `SpellCaster`).
- `DrawingCanvas.Redraw()` repinta desde `Session` (**no** la borra); `Clear()` borra todo.
- `PlatformDetector.IsVR` / `IsLookCaptured` — único lugar con lógica de plataforma.

## De la tinta al mundo (M3)

1. **Orientación de cada signo**: la cabeza de pose del modelo (`angle`) se calibra al iniciar con las plantillas del
   diccionario (`GlyphClassifier.CalibratePose`). Da la rotación de cada glifo respecto de su plantilla (signos: abajo,
   apuntando arriba). Sin pose, se asume "hacia adentro" con confianza baja. Zona muerta de 12° hacia la clase canónica
   más cercana (adentro, afuera o tangencial).
2. **Ley vectorial de columnas (R‑05)**: cada signo aporta posición, largo y orientación ⇒ presupuesto `S`, empuje
   lateral `P`, convergencia `C` y circulación `Γ`.
   - `aim = (P, max(C,0))` es el chorro.
   - `max(-C,0)` es el abanico.
   - `Γ` es el vórtice.
   Levitación y pull usan la misma álgebra con presupuestos propios (orbe y succión); la convergencia es una lente.
3. **Primitivas**: una primitiva corre si su magnitud es ≥ 20% del presupuesto (filtra el ruido de pose).
4. **Manifestación**: una pista de partículas por primitiva, nacida en el **centro del anillo**, escalada por el
   **radio real del sello en metros** × `effectScale`. Tiempos: carga 0.45 s · golpe 0.32 s · cuerpo
   `clamp(duration − 1.5, 0.6, 6)` (×1.5 con fuga de dispersión) · liberación 0.76 s · resplandor 0.42 s.
5. **Cara de emisión**: papeles = `AwayFromViewer` (la magia sale por el lado opuesto a quien mira, hacia el objetivo);
   paredes y mesas, a futuro = `InkSide` (canon).
6. **Física (`MagicField`)**: cada primitiva define una velocidad de flujo y los cuerpos se aceleran **hacia** ella.
   - La forma fija la velocidad: `2 + 8·force` m/s × elemento.
   - El área del sello fija el acople (canon §2 de ground-truth).
   - La ráfaga es un cambio de velocidad acotado y el orbe es un resorte que sostiene el peso.
   - Afecta a cualquier Rigidbody, incluido el jugador.

## Reglas del sello (de la referencia)

| Regla | Detalle |
|---|---|
| Anillo obligatorio | Sin anillo → inválido (`no_ring_detected`). |
| Activo = anillo cerrado | Cobertura ≥ 98.5%, o ≥ 90% con extremos a < 0.2 r. Abierto (≥ 52%) → **preparado**: válido pero no se lanza. |
| Capas | centro ≤ 0.32 r · medio ≤ 0.66 · exterior ≤ 0.94 · borde ≤ 1.06. Cada glifo tiene `allowedLayers`. |
| Sigilo | Trazos con centro de masa ≤ 0.40 r forman un candidato. Un solo sigilo; confianza ≥ 0.62. |
| Signos | Resto de trazos agrupados por proximidad (union-find, 0.12 r). Reconocidos con confianza ≥ 0.48. |
| Parámetros | Base + semántica del sigilo + Σ(semántica del signo × influencia) + calidad. Influencia = confianza × prolijidad × tamaño × largo × capa × distancia. |
| Dirección | Suma de direcciones de signos (`position` / `inward` / `orientation`), inclinada desde la normal según `force` (máx 76°). |

## Input (PlayerInputReader)

| Acción | PC | Gamepad | VR (OpenXR) |
|---|---|---|---|
| Move | WASD | Left stick | Left thumbstick |
| Look | Mouse | Right stick | (head tracking) |
| Jump | Space | South | — |
| Interact (agarrar/soltar) | E | Right trigger | Right grip |
| UseTool (dibujar/borrar) | Click izq. | Left trigger | Right trigger |
| SecondaryUse (deshacer) | Click der. | East | A |
| Recargar tinta | R (mirando un tintero, con uno en la mano o de la reserva) | West | Mojar la punta en un tintero |
| Liberar cursor | Escape (click recaptura) | — | — |

## Escena `Escena.unity`

```
CinemachineCamera · Directional Light · EventSystem
Canvas (UI) ─ HUD (Crosshair) ─ DEBUG [DiagnosticUIController]
SystemManagers [SentisGlyphClassifier, SpellCaster, SpellOutputController]
PaperCanvas (layer 6) [DrawingCanvas (AwayFromViewer), PhysicalCanvas, SealFeedback]
TestProps (cajas, plataforma con 9 blancos, pesada) [Respawnable]
Player [PlatformDetector, PlayerInputReader, PlayerMovement, PlayerLook, PlayerInteractor, InkSupply] ─ PCRIG / VRRIG
Pen, Eraser, Rag (layer 7) [PhysicalTool → SpellCaster] ─ RaycastTool
Inkwell [Inkwell]
Floor
```

## Puntos de extensión
- **Nuevo glifo / cambiar semántica**: editar JSON (o sync desde la referencia) → `SpellSimulator/Dictionary/Rebuild Asset`. Sin tocar código.
- **Nuevo elemento**: `ElementType` + `SpellIR.ParseElement` + prefab en `SpellOutputController`.
- **Otro reconocedor** (CPU, otro modelo): implementar `IGlyphClassifier`.
- **Nueva herramienta**: `IDrawingTool` + `PhysicalTool.ToolKind`.
- **Reaccionar a hechizos** (sandbox, hápticos, sonido): suscribirse a `SpellCaster.SpellCompiled`.
- **Mostrar la lectura del sello** (overlays, tutorial, libro): `SpellCaster.SealAnalyzed` trae el `GlyphAST` además del hechizo.
