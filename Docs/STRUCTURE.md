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
│   │   │   ├── Elements/         #   ElementLook: Fire, Water, Wind, Earth, Light
│   │   │   ├── Materials/        #   MaterialProfile: Madera, Papel, Piedra, Tela (+ ReactionTable.asset en Data/)
│   │   │   ├── Tools/            #   ToolDefinition: Lapiz, Pincel, Goma, Trapo
│   │   │   └── GlyphDictionary.asset
│   │   ├── Editor/               #   SpellSimulator.Editor.asmdef — harness, sync del diccionario, chequeos
│   │   ├── Materials/            #   Canvas, BrushMaterial, Pen, Eraser, Rag, Inkwell, Crate, VRPointer, SpellParticle(.shader) + Additive/Alpha, StylizedWater(.shader) + Water, GroundMark(.shader) + GroundMark, Stone, Cloth, Binarize(.shader)
│   │   ├── Prefabs/
│   │   │   ├── Objects/          #   PaperCanvas, Pen, Eraser, Tool
│   │   │   ├── Player/           #   Player (PCRIG + VRRIG)
│   │   │   ├── System/           #   SystemManagers, Canvas (UI), camaras, luz, EventSystem
│   │   │   └── VFX/              #   Fire (+Fire_VFX.vfx), Air, Earth, Light, Water, FireSpell_VFX.vfx
│   │   ├── Scenes/Escena.unity   #   unica escena (Build index 0)
│   │   ├── Scripts/              #   SpellSimulator.Runtime.asmdef
│   │   │   ├── Canvas/           #   DrawingCanvas
│   │   │   ├── Casting/          #   SpellCaster, SealActivation, SealReader, SealRecord
│   │   │   ├── Combat/           #   Health, DamageTable, DamageModel, ImpactDamage, DamageNumbers
│   │   │   ├── Compiler/         #   GlyphAST, GlyphAstBuilder, SpellCompiler, SemanticRules, SpellQuality, SpellDirection, SpellIR, SpellPlan, PlanResolver
│   │   │   ├── Core/             #   DrawingData (Stroke, DrawingSession), GeometryUtils, StrokeCleaner, InkReservoir
│   │   │   ├── Debug/            #   DiagnosticBus, DiagnosticUIController   (namespace DebugTools)
│   │   │   ├── Glyphs/           #   GlyphDictionary, GlyphEntry, GlyphDictionaryAsset
│   │   │   ├── Input/Tools/      #   IDrawingTool, PenTool, EraserTool, RagTool
│   │   │   ├── Interfaces/       #   IInteractable, IUsable, HandType
│   │   │   ├── Output/           #   SpellOutputController, SpellManifestation, MagicField, ElementLook, SealFrame, SealFeedback
│   │   │   ├── Parsing/          #   RingDetector, StrokeGrouper, SymbolCandidate, SealParser
│   │   │   ├── Player/           #   PlatformDetector, PlayerInputReader, PlayerMovement, PlayerLook, PlayerInteractor, InkSupply, Haptics
│   │   │   ├── Reactions/        #   ReactionModel, MaterialProfile, ReactionTable, ElementReactive, MagicWorld, BurnEffect, Shatter, WorldResetter, WaterVolume, WaterEffects, GroundSurface, GroundReactions, GroundMarks, RockRise, MarkFade
│   │   │   ├── Recognition/      #   TensorRasterizer, GlyphClassifier, SentisGlyphClassifier, GlyphPrediction (+ClassificationMath), PoseMath
│   │   │   └── World/            #   PhysicalTool, ToolDefinition, PhysicalCanvas, Inkwell, Respawnable, SealBreaker, TrainingDummy
│   │   ├── Tests/EditMode/       #   SpellSimulator.Tests.EditMode.asmdef — Parsing, Recognition, Compiler, modelo
│   │   └── Tests/Fixtures/       #   Sellos reales grabados (SealRecord .json) para SealFixtureTests
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
| Mundo reactivo (materiales, fuego, fractura) | `Scripts/Reactions/` (namespace `SpellSimulator.Reactions`) |
| Herramienta de editor | `Assets/_Project/Editor/` |
| Test | `Assets/_Project/Tests/EditMode/` (o `PlayMode/`) |
| VFX Graph / prefab de efecto | `Assets/_Project/Prefabs/VFX/` |
| Modelos 3D / texturas | `Assets/_Project/Art/{Models,Textures}/` |
| Datos (JSON, ScriptableObjects) | `Assets/_Project/Data/` |
