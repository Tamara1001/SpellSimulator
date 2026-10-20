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
│   │   │   ├── Lore/glifos.json  #   nombres y explicaciones en español, glifos iniciales y libros
│   │   │   ├── PostProcess.asset #   VolumeProfile (bloom, tonemapping, color, viñeta) de Pradera
│   │   │   └── GlyphDictionary.asset
│   │   ├── Editor/               #   SpellSimulator.Editor.asmdef — harness, sync del diccionario, chequeos
│   │   │   └── Map/              #   MapBuilder, TowerBuilder, MapDetails, LowPolyMesh (generador del mapa)
│   │   ├── Materials/            #   Canvas, BrushMaterial, Pen, Eraser, Rag, Inkwell, Crate, VRPointer, SpellParticle(.shader) + Additive/Alpha, StylizedWater(.shader) + Water, GroundMark(.shader) + GroundMark, StylizedLit(.shader) + Stylized, Waterfall(.shader) + Waterfall, Sky, Stone, Cloth, Binarize(.shader)
│   │   ├── Prefabs/
│   │   │   ├── Objects/          #   PaperCanvas, Pen, Eraser, Tool
│   │   │   ├── Player/           #   Player (PCRIG + VRRIG)
│   │   │   ├── System/           #   SystemManagers, Canvas (UI), camaras, luz, EventSystem
│   │   │   └── VFX/              #   Fire (+Fire_VFX.vfx), Air, Earth, Light, Water, FireSpell_VFX.vfx
│   │   ├── Scenes/               #   Pradera.unity (mapa del modo Libre, build index 0) y Escena.unity (laboratorio de pruebas)
│   │   ├── Audio/                #   Synth/ (generado por Tools/Audio/synth_audio.py), Kenney/{RPG,Impact,Interface} (CC0, con License.txt)
│   │   ├── Art/Generated/        #   mallas generadas por MapBuilder (terreno, pinos, rocas, torre, muebles); no editar a mano
│   │   ├── Scripts/              #   SpellSimulator.Runtime.asmdef
│   │   │   ├── Learning/         #   GlyphLore, DiscoveryState, Discovery, LibraryBook, Grimoire, GrimoireButton
│   │   │   ├── Audio/            #   AudioLibrary, AudioDirector, GameAudio, AmbientEmitter, FootstepAudio, CollisionAudio
│   │   │   ├── Canvas/           #   DrawingCanvas
│   │   │   ├── Casting/          #   SpellCaster, SealActivation, SealReader, SealRecord, StampPattern
│   │   │   ├── Combat/           #   Health, DamageTable, DamageModel, ImpactDamage, DamageNumbers
│   │   │   ├── Compiler/         #   GlyphAST, GlyphAstBuilder, SpellCompiler, SemanticRules, SpellQuality, SpellDirection, SpellIR, SpellPlan, PlanResolver
│   │   │   ├── Core/             #   DrawingData (Stroke, DrawingSession), GeometryUtils, StrokeCleaner, InkReservoir
│   │   │   ├── Debug/            #   DiagnosticBus, DiagnosticUIController   (namespace DebugTools)
│   │   │   ├── Glyphs/           #   GlyphDictionary, GlyphEntry, GlyphDictionaryAsset
│   │   │   ├── Input/Tools/      #   IDrawingTool, PenTool, EraserTool, RagTool
│   │   │   ├── Interfaces/       #   IInteractable, IUsable, HandType
│   │   │   ├── Output/           #   SpellOutputController, SpellManifestation, MagicField, ElementLook, SealFrame, SealFeedback; Bodies/EarthShapes (muros, puentes, estacas), RockSpell, RockMath
│   │   │   ├── Parsing/          #   RingDetector, StrokeGrouper, SymbolCandidate, SealParser
│   │   │   ├── Player/           #   PlatformDetector, PlayerInputReader, PlayerMovement, PlayerLook, PlayerInteractor, ObjectMover, FocusMode, InkSupply, Haptics
│   │   │   ├── Reactions/        #   ReactionModel, MaterialProfile, ReactionTable, ElementReactive, MagicWorld, BurnEffect, Shatter, WorldResetter, WaterVolume, WaterEffects, GroundSurface, GroundReactions, GroundMarks, RockRise, MarkFade
│   │   │   ├── Recognition/      #   TensorRasterizer, GlyphClassifier, SentisGlyphClassifier, GlyphPrediction (+ClassificationMath), PoseMath
│   │   │   └── World/            #   PhysicalTool, ToolDefinition, PhysicalCanvas, Inkwell, Respawnable, PinnedBody, SealBreaker, TrainingDummy, Stamp, FaceCamera, SealTrigger, Movable, Inscribable, InscribedDecal, SplitSealHalf, Breakable/Indestructible, MeshCut, MeshBreaker, Flutter, KeepUpright; Map/ValleyShape
│   │   ├── Tests/EditMode/       #   SpellSimulator.Tests.EditMode.asmdef — Parsing, Recognition, Compiler, modelo
│   │   └── Tests/Fixtures/       #   Sellos reales grabados (SealRecord .json) para SealFixtureTests
│   ├── Settings/                 # URP assets (PC_RPAsset, renderers, volume profiles)
│   ├── XR/, XRI/                 # settings de OpenXR / XR Interaction Toolkit (generados)
│   ├── Samples/                  # samples importados (XRI Starter Assets; los usa el prefab Player) — no editar
│   ├── TextMesh Pro/
│   └── InputSystem_Actions.inputactions  # default de Unity, NO usado (input por código)
├── Packages/manifest.json
└── ProjectSettings/
```

## Red (multijugador)
- `Scripts/Net/`: `NetSession` (sala por Relay/IP directa), `NetPlayer` (avatar de red), `NetHud`, `ClientNetworkTransform`.
- `Scripts/Net/World/`: `NetWorld` (mundo compartido), `NetBody`, `NetBodyMath`, `NetCanvasSync` (trazos), `NetCast` + `SpellIRCodec` (lanzamiento), `NetSpawn` (cuerpos creados en juego), `NetDuel` + `DuelArena` (Duelo en red y su arena); reglas puras en `Scripts/Combat/DuelMatch.cs`.
- `Scripts/Net/Bots/`: `BotOptions`, `NetBot` (jugadores automaticos; ver HARNESS).
- `Editor/`: `NetSetup` (avatar de red), `HarnessNetSmoke`, `HarnessBots`, `BotBuild`.

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
