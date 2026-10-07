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
│   │   ├── Data/                 #   glyph-recognizer.onnx(+.data), glyph-class-to-idx.json
│   │   ├── Editor/               #   SpellSimulator.Editor.asmdef — harness (tests runner, validador)
│   │   ├── Materials/            #   Canvas, BrushMaterial, Pen, Eraser, Binarize(.shader)
│   │   ├── Prefabs/
│   │   │   ├── Objects/          #   PaperCanvas, Pen, Eraser, Tool
│   │   │   ├── Player/           #   Player (PCRIG + VRRIG)
│   │   │   ├── System/           #   SystemManagers, Canvas (UI), cámaras, luz, EventSystem
│   │   │   └── VFX/              #   Air, Earth, Light, Water, FireSpell_VFX.vfx
│   │   ├── Scenes/Escena.unity   #   única escena (Build index 0)
│   │   ├── Scripts/              #   SpellSimulator.Runtime.asmdef
│   │   │   ├── Canvas/           #   DrawingCanvas
│   │   │   ├── Compiler/         #   SpellCompiler, SemanticRules, SpellDescriptor (SpellIR, enums)
│   │   │   ├── Core/             #   DrawingData, StrokeCleaner, RingDetector, StrokeGrouper, GeometryUtils, SpellAST
│   │   │   ├── Debug/            #   DiagnosticBus, DiagnosticUIController   (namespace DebugTools)
│   │   │   ├── Inference/        #   SentisInferenceEngine, TensorRasterizer
│   │   │   ├── Input/            #   MouseInputHandler (legacy) + Tools/ (IDrawingTool, PenTool, EraserTool)
│   │   │   ├── Interfaces/       #   IInteractable, IUsable, HandType
│   │   │   ├── Output/           #   SpellOutputController
│   │   │   ├── Player/           #   PlatformDetector, PlayerInputReader, PlayerMovement, PlayerLook, PlayerInteractor
│   │   │   └── World/            #   PhysicalTool, PhysicalCanvas
│   │   ├── Tests/EditMode/       #   SpellSimulator.Tests.EditMode.asmdef — NUnit
│   │   └── VFX/                  #   Fire.prefab, Fire_VFX.vfx  (duplica Prefabs/VFX, ver KI-015)
│   ├── Editor/                   # CheckURP, VFXDiagnostics (Assembly-CSharp-Editor, sin MenuItem)
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
