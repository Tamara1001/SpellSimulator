---
tags: [doc, onboarding]
---
# Cómo continuar el proyecto desde cero (para una persona nueva y su Claude)

> Esta página es el punto de entrada. Todo lo demás está enlazado desde [[Home]]. Si algo no coincide con lo que ves, manda
> [[CLAUDE]] (reglas del repo) y el código.

## 1. Qué necesitás instalar
| Qué | Detalle |
|---|---|
| **Unity Hub + Unity 6000.3.14f1** | Exactamente esa versión (`ProjectSettings/ProjectVersion.txt`). Módulos: **Windows Build Support (IL2CPP o Mono)**, **WebGL Build Support** y **Android Build Support** (OpenJDK + SDK/NDK, para Meta Quest). |
| **Git** | El repo es `https://github.com/Tamara1001/SpellSimulator` (rama `main`). No usa Git LFS. |
| **Claude Code** (opcional pero recomendado) | El repo trae `CLAUDE.md` y `AGENTS.md` con las reglas para agentes. Leerlas antes de pedir cambios. |
| **Visual Studio / Rider** | Cualquiera de los dos; el proyecto trae `.vsconfig`. |
| **Python 3** (opcional) | Solo para scripts auxiliares (zip del build web, análisis); nada del juego lo necesita. |

No hace falta el proyecto de referencia `wha-spell-simulator`: el modelo ONNX (`Assets/_Project/Data/glyph-recognizer.onnx`) y el
diccionario ya están copiados en el repo. Solo hace falta si querés **actualizar** modelo/diccionario ([[REFERENCE]]): clonarlo en una
carpeta hermana (`../wha-spell-simulator`) y correr `SpellSimulator > Dictionary > Sync From Reference`.

## 2. Primer arranque
1. `git clone https://github.com/Tamara1001/SpellSimulator.git` y abrir la carpeta con Unity Hub (6000.3.14f1).
   El primer import regenera `Library/` (10-30 min). Es normal que `Library/`, `Logs/`, `Builds/`, `UserSettings/` y los `.csproj` no estén en el repo.
2. Abrir `Assets/_Project/Scenes/Pradera.unity` (el juego; índice 0 del build) y dar **Play**. Arranca el **menú principal**: «Modo Sandbox» entra a la torre.
   `Escena.unity` es el laboratorio viejo (sin la HUD nueva).
3. Controles: [[GAME_DESIGN]] (sección Controles). Resumen: WASD, mouse, E agarrar/usar, 1-3 herramientas, **TAB** grimorio, V vista completa,
   F dar vuelta, Q arrojar, rueda = grosor, Z/X páginas, F3 depuración.
4. Verificar que todo está sano (sección 3).

## 3. Verificar cambios (siempre antes de commitear)
Con el Editor abierto (ver [[HARNESS]] para el detalle y los trucos):
- Compilación limpia (sin `error CS`).
- **Tests EditMode**: menú `SpellSimulator > Harness > Run EditMode Tests` (hoy **161**; resultado en `Logs/harness-editmode.txt`). Si queda en `RUNNING` (la recarga de dominio lo cortó) relanzar.
- **Test de humo de Play**: menú `SpellSimulator > Harness > Run Play Smoke` (o `HarnessPlaySmoke.Run()`) (resultado en `Logs/harness-playsmoke.txt`, ~20 s, ~20 chequeos). **Agregar un chequeo por cada sistema nuevo.**
- Si cambiaste cómo se genera el mundo: `SpellSimulator.EditorTools.Map.MapBuilder.Build()` (ver sección 5).

**Sin el Editor abierto (modo batch, verificado: 161 tests en PASS)** — cerrá Unity y corré, ajustando la ruta de `Unity.exe`:
```bash
"<Hub>/Editor/6000.3.14f1/Editor/Unity.exe" -batchmode -nographics -projectPath "<ruta al repo>"   -runTests -testPlatform EditMode -assemblyNames "SpellSimulator.Tests.EditMode"   -testResults "<ruta al repo>/Logs/batch-tests.xml" -logFile "<ruta al repo>/Logs/batch-tests.txt"
```
También sirve para cualquier método estático de Editor: `-executeMethod SpellSimulator.EditorTools.ProjectSetup.FixBuildScenes` (con `-quit`).
Ojo: el batch o el build de URP reescriben solos `Assets/Settings/*_RPAsset.asset`, `UniversalRenderPipelineGlobalSettings.asset`,
`Assets/XR/Settings/OpenXRPackageSettings.asset` y `SpellSimulator.slnx` (ruido de prefiltrado/paquetes): **no los commitees** (`git checkout -- <archivo>`).

> Por qué menús y no solo MCP: sin el MCP de Unity (sección 4) todo lo de arriba se puede correr desde los menús `SpellSimulator/…` del Editor.

## 4. Claude Code + Unity MCP (opcional)
El proyecto usa el paquete `com.unity.ai.assistant` (MCP de Unity). Con él, Claude Code puede ejecutar código del Editor
(`Unity_RunCommand`), compilar, correr tests y sacar capturas. Cómo conectarlo y los trucos que cuestan redescubrir están en
**[[HARNESS]] → «Trucos del MCP de Unity»** (parámetros `Code`/`Title`, esperar la compilación con un loop de `sleep`, builds con `delayCall`,
CRLF, etc.). Resumen de reglas duras de [[CLAUDE]]:
- Solo se edita código propio en `Assets/_Project/`. **Nunca** `.unity/.prefab/.asset/.meta` a mano: se cambian vía Unity (MCP o menús).
- Todo cast pasa por `SpellCaster.Cast`; el significado de los glifos es data; cero allocations en `Update`; PC y VR en paralelo (`PlatformDetector.IsVR`).
- Commits en español, atómicos, con trailer `Co-Authored-By`; documentar lo mínimo útil (CHANGELOG siempre; KNOWN_ISSUES si aplica).

## 5. El mundo se genera por código (importante)
`MapBuilder.Build()` (`Editor/Map/`) **regenera la escena Pradera entera** (terreno, torre, props, zonas de práctica…). No se edita la escena a mano para
cosas que el generador crea. Dos mecanismos para no perder trabajo:
- **`MapLayout`** (`Editor/Map/MapLayout.cs`): si movés objetos a mano, al empezar el siguiente Build se detectan y se guardan en
  `Assets/_Project/Data/MapOverrides.json`, y se reaplican al terminar. Menús `SpellSimulator/Map/Capturar mi layout manual` y `Borrar overrides del layout`.
  La línea base vive en `Logs/MapBaseline.json` (no versionada): en un clon nuevo el primer Build la crea y recién desde ahí detecta cambios manuales.
- Los setups de editor (`InteractionSetup`, `FlammableSetup`, `UISetup`, `MovablesSetup`, `ElementVfxSetup`, `GrimoireSetup`) son **idempotentes**: se pueden volver a correr.
  `SpellSimulator > Learning > Setup Grimoire` hay que correrlo si cambiás el grimorio o sus datos.

## 6. Builds
- **Windows**: build normal; smoke test del exe **sin** `-nographics` (la calibración del modelo necesita GPU), buscar «Modo: PC» y «Pose calibrada: True» en el log.
- **WebGL (itch.io)**: `SpellSimulator/Build/WebGL (itch.io)` (release, Gzip, sin hilos) o `WebGL (diagnostico)`. Salida en `Builds/WebGL`
  (ignorada por git); resultado en `Logs/build-webgl.txt`. Tarda 10-20 min la primera vez. Comprimir `Builds/WebGL` **con `index.html` en la raíz del zip** y subirlo a itch.io
  (tipo HTML). En WebGL el reconocimiento corre en CPU (no hay compute shaders). Ver KI-103/KI-104.
- **Quest (Android/OpenXR)**: todavía sin build estable (M7). La lógica de plataforma va solo por `PlatformDetector.IsVR`.

## 7. Cosas que sorprenden
- Al lanzar tests/Play con la escena modificada Unity puede abrir el cartel «guardar escena» y colgar el MCP: los harness ya la guardan solos; si aparece, aceptá cualquier opción.
- No editar scripts con el Editor en Play (recarga el dominio y rompe el estado).
- Los `.cs`/`.md`/`.shader`/`.uxml`/`.uss` usan CRLF (`.gitattributes`); si una edición automática «no encuentra el texto», casi siempre es un final de línea.
- `HarnessSeals` dibuja sellos en UV **cuadrado**: sobre lienzos rectangulares el sello sale deformado y el hechizo da inválido.
- El grafo de código (Graphify, `graphify-out/`, `Docs/Graph/`) no se versiona; regenerarlo con `graphify update .` solo si hace falta.
- Los sellos reales grabados en pruebas manuales están en `persistentDataPath/Sellos`; los útiles se copian a `Assets/_Project/Tests/Fixtures` (ver su README).

## 8. Dónde seguir
1. [[STATUS]] — estado en una página.
2. [[ROADMAP]] → «Siguiente (backlog priorizado)».
3. [[KNOWN_ISSUES]] — bugs y deuda (los últimos KI-097…KI-105 son de la prueba manual más reciente).
4. [[PLAN_SEXTA_TANDA]] — desglose del último feedback de pruebas (sexta y séptima tanda) con lo que se hizo y por quién.
5. [[DECISIONS]] — ADR (ADR-033: inventario de dos manos; ADR-034: guardar hechizos).

La persona que diseñó y probó el proyecto es **Tamara D'Angelo** (@Tamara1001): sus pruebas manuales son la fuente de verdad de qué está bien o mal.
