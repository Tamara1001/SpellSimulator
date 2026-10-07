---
tags: [doc]
---
# Estructura

```
SpellSimulator/
├── CLAUDE.md / AGENTS.md         # reglas para agentes
├── README.md                     # presentación del proyecto
├── Docs/                         # ← vault de Obsidian (esta documentación)
│   └── Graph/                    #   notas generadas por Graphify (ignorado en git)
├── graphify-out/                 # grafo de código (ignorado en git)
├── Assets/
│   ├── _Project/                 # ★ TODO lo propio vive acá
│   │   ├── Data/                 #   glyph-recognizer.onnx(+.data), glyph-class-to-idx.json,
│   │   │   ├── Dictionary/       #   sigils/*.json, signs/*.json (copiados de la referencia)
│   │   │   └── GlyphDictionary.asset
│   │   ├── Editor/               #   SpellSimulator.Editor.asmdef — harness, sync del diccionario, chequeos
│   │   ├── Materials/            #   Canvas, BrushMaterial, Pen, Eraser, VRPointer, Binarize(.shader)
│   │   ├── Prefabs/
│   │   │   ├── Objects/          #   PaperCanvas, Pen, Eraser, Tool
│   │   │   ├── Player/           #   Player (PCRIG + VRRIG)
│   │   │   ├── System/           #   SystemManagers, Canvas (UI), camaras, luz, EventSystem
│   │   │   └── VFX/              #   Fire (+Fire_VFX.vfx), Air, Earth, Light, Water, FireSpell_VFX.vfx
│   │   ├── Scenes/Escena.unity   #   unica escena (Build index 0)
│   │   ├── Scripts/              #   SpellSimulator.Runtime.asmdef
│   │   │   ├── Canvas/           #   DrawingCanvas
│   │   │   ├── Casting/          #   SpellCaster (punto de entrada del pipeline)
│   │   │   ├── Compiler/         #   GlyphAST, GlyphAstBuilder, SpellCompiler, SemanticRules, SpellQuality, SpellDirection, SpellIR
│   │   │   ├── Core/             #   DrawingData (Stroke, DrawingSession), GeometryUtils, StrokeCleaner
│   │   │   ├── Debug/            #   DiagnosticBus, DiagnosticUIController   (namespace DebugTools)
│   │   │   ├── Glyphs/           #   GlyphDictionary, GlyphEntry, GlyphDictionaryAsset
│   │   │   ├── Input/Tools/      #   IDrawingTool, PenTool, EraserTool
│   │   │   ├── Interfaces/       #   IInteractable, IUsable, HandType
│   │   │   ├── Output/           #   SpellOutputController
│   │   │   ├── Parsing/          #   RingDetector, StrokeGrouper, SymbolCandidate, SealParser
│   │   │   ├── Player/           #   PlatformDetector, PlayerInputReader, PlayerMovement, PlayerLook, PlayerInteractor
│   │   │   ├── Recognition/      #   TensorRasterizer, SentisGlyphClassifier, GlyphPrediction (+ClassificationMath)
│   │   │   └── World/            #   PhysicalTool, PhysicalCanvas
│   │   └── Tests/EditMode/       #   SpellSimulator.Tests.EditMode.asmdef — Parsing, Recognition, Compiler, modelo
│   ├── Settings/                 # URP assets (PC_RPAsset, renderers, volume profiles)
│   ├── XR/, XRI/                 # settings de OpenXR / XR Interaction Toolkit (generados)
│   ├── Samples/                  # samples importados (XRI Starter Assets, VFX, SRP Core) — no editar
│   ├── TextMesh Pro/, TutorialInfo/, _Recovery/
│   └── InputSystem_Actions.inputactions  # default de Unity, NO usado (input por código)
├── Packages/manifest.json
└── ProjectSettings/
```

## Dónde va cada cosa nueva
| Tipo | Lugar |
|---|---|
| Script runtime | `Assets/_Project/Scripts/<Modulo>/` (crear módulo nuevo = carpeta + namespace) |
| Sandbox (Flammable, ragdolls, fuerzas) | `Scripts/Sandbox/` (namespace `SpellSimulator.Sandbox`) |
| Herramienta de editor | `Assets/_Project/Editor/` |
| Test | `Assets/_Project/Tests/EditMode/` (o `PlayMode/`) |
| VFX Graph / prefab de efecto | `Assets/_Project/Prefabs/VFX/` |
| Modelos 3D / texturas | `Assets/_Project/Art/{Models,Textures}/` |
| Datos (JSON, ScriptableObjects) | `Assets/_Project/Data/` |
